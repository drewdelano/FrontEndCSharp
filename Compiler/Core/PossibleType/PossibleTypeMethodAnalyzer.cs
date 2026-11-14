using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.Read;
using FrontEndCSharp.RuntimeOverrides;
using static Compiler.PossibleType.PossibleTypeInstructionFlow;

namespace Compiler.PossibleType
{
    internal static class PossibleTypeMethodAnalyzer
    {
        internal static LinearResult Analyze(
            MethodDecoder decoder,
            IReadOnlyDictionary<MethodInfo, FlowValue> returns,
            IReadOnlyList<FlowValue> arguments,
            IReadOnlyDictionary<FieldLocation, FlowValue> fields,
            IReadOnlyDictionary<ArrayLocation, FlowValue> arrayElements,
            IReadOnlyDictionary<MethodBase, HashSet<Type>> exceptions,
            Func<MethodBase, bool> hasRuntimeOverride,
            Func<MethodInfo, IReadOnlyCollection<Type>> getRuntimeOverrideDependencyReturnTypes,
            Func<MethodInfo, IReadOnlyCollection<Type>> getRuntimeOverrideAwaitableResultTypes,
            Func<MethodBase, IReadOnlyList<ByRefParameterTypes>> getByRefParameterTypes,
            bool inferTaskFactoryResults,
            bool collectReceivers)
        {
            var instructions = decoder.InstructionBlocks.SelectMany(block => block.Instructions).ToArray();
            if (instructions.Length == 0)
            {
                return new LinearResult();
            }
            var offsetIndexes = new Dictionary<int, int>(instructions.Length);
            for (var index = 0; index < instructions.Length; index++)
            {
                offsetIndexes.Add(instructions[index].ILOffset, index);
            }
            var initialLocals = decoder.MethodBody?.LocalVariables
                .Select(local => local.LocalType.IsValueType
                    ? ConcreteDeclaredValue(local.LocalType)
                    : FlowValue.ForType(typeof(KnownNull)))
                .ToArray() ?? [];
            var tracksArgumentStorage = instructions.Any(instruction =>
                TryGetArgumentIndex(instruction.OpCode, instruction.Operand, out var index)
                && (instruction.OpCode.Name!.StartsWith("starg", StringComparison.Ordinal)
                    || instruction.OpCode.Name.StartsWith("ldarga", StringComparison.Ordinal)
                    || IsByRefArgument(decoder.MethodBase, index)));
            var tracksByRefWrites = decoder.MethodBase.GetParameters()
                .Any(parameter => parameter.ParameterType.IsByRef);
            var tracksFieldReferences = instructions.Any(instruction =>
                instruction.OpCode == OpCodes.Ldflda || instruction.OpCode == OpCodes.Ldsflda);
            var tracksArrayReferences = instructions.Any(instruction =>
                instruction.OpCode == OpCodes.Ldelema);
            var initialArguments = tracksArgumentStorage
                ? arguments.Select(value => value.Clone()).ToArray()
                : arguments as FlowValue[] ?? arguments.ToArray();
            var states = new Dictionary<int, FlowState>
            {
                [0] = new FlowState(
                    [],
                    initialLocals,
                    tracksArgumentStorage ? initialArguments : null,
                    byRefWrites: tracksByRefWrites ? new Dictionary<int, FlowValue>() : null,
                    fields: tracksFieldReferences
                        ? new Dictionary<FieldLocation, FlowValue>()
                        : null,
                    arrayElements: tracksArrayReferences
                        ? new Dictionary<ArrayLocation, FlowValue>()
                        : null)
            };
            var pending = new Queue<int>();
            pending.Enqueue(0);
            var result = new LinearResult();
            var stateMerger = new PossibleTypeStateMerger(states, pending);
            var storage = new PossibleTypeFlowStorage(
                decoder,
                fields,
                arrayElements,
                result);
            var exceptionFlow = new PossibleTypeExceptionFlow(
                decoder,
                exceptions,
                hasRuntimeOverride,
                offsetIndexes,
                result,
                stateMerger);
            while (pending.Count > 0)
            {
                var instructionIndex = pending.Dequeue();
                var state = states[instructionIndex].Clone();
                var stack = state.Stack;
                var locals = state.Locals;
                var currentArguments = state.Arguments ?? initialArguments;
                var currentByRefWrites = state.ByRefWrites;
                var currentFields = state.Fields;
                var currentArrayElements = state.ArrayElements;
                var instruction = instructions[instructionIndex];
                result.ReachableOffsets.Add(instruction.ILOffset);
                var opcode = instruction.OpCode;
                var instructionExceptions = GetRuntimeExceptionTypes(opcode);
                FlowState exceptionalCallState = null;
                if (opcode == OpCodes.Newobj && instruction.Operand is ConstructorInfo constructor)
                {
                    var callArguments = PopArguments(stack, constructor.GetParameters().Length);
                    var instance = FlowValue.ForAllocation(
                        constructor.DeclaringType!,
                        decoder.MethodBase,
                        instruction.ILOffset);
                    result.Calls.Add(new PossibleTypeCallFlow(
                        constructor,
                        new[] { instance }.Concat(callArguments).ToArray()));
                    exceptionFlow.AddCalledExceptions(
                        constructor,
                        receiver: null,
                        instruction.ILOffset,
                        instructionExceptions);
                    stack.Push(instance);
                }
                else if ((opcode == OpCodes.Call || opcode == OpCodes.Callvirt) && instruction.Operand is MethodBase called)
                {
                    var callArguments = PopArguments(stack, called.GetParameters().Length);
                    FlowValue receiver = null;
                    if (!called.IsStatic)
                    {
                        receiver = Pop(stack);
                        if (opcode == OpCodes.Callvirt
                            && instructionIndex > 0
                            && instructions[instructionIndex - 1].OpCode == OpCodes.Constrained)
                        {
                            receiver = storage.LoadReferences(
                                receiver,
                                locals,
                                currentArguments,
                                currentFields,
                                currentArrayElements);
                            if (instructions[instructionIndex - 1].Operand is Type constrainedType
                                && !constrainedType.IsGenericParameter
                                && (receiver.IsEmpty || constrainedType.IsValueType))
                            {
                                receiver = FlowValue.ForType(constrainedType);
                            }
                        }
                    }
                    var calledParameters = called.GetParameters();
                    var hasByRefParameters = calledParameters.Any(parameter =>
                        parameter.ParameterType.IsByRef);
                    var propagatedCallArguments = hasByRefParameters
                        ? callArguments
                            .Select((argument, index) => calledParameters[index].ParameterType.IsByRef
                                ? storage.LoadReferences(
                                    argument,
                                    locals,
                                    currentArguments,
                                    currentFields,
                                    currentArrayElements)
                                : argument)
                            .ToArray()
                        : callArguments;
                    result.Calls.Add(new PossibleTypeCallFlow(
                        called,
                        (receiver == null ? Enumerable.Empty<FlowValue>() : new[] { receiver })
                            .Concat(propagatedCallArguments)
                            .ToArray()));
                    exceptionFlow.AddCalledExceptions(called, receiver, instruction.ILOffset, instructionExceptions);
                    var byRefWrites = hasByRefParameters
                        ? getByRefParameterTypes(called)
                        : Array.Empty<ByRefParameterTypes>();
                    if (byRefWrites.Count != 0)
                    {
                        // A callee summary describes storage only after normal completion.
                        // Keep the pre-call storage state for any exceptional successor.
                        exceptionalCallState = state.Clone();
                    }
                    foreach (var write in byRefWrites)
                    {
                        if (write.ParameterIndex < 0 || write.ParameterIndex >= callArguments.Length)
                        {
                            throw new InvalidDataException(
                                $"Method '{called}' has an invalid by-ref summary for parameter index "
                                + $"{write.ParameterIndex}.");
                        }
                        var value = new FlowValue();
                        foreach (var type in write.Types)
                        {
                            value.UnionWith(type.IsValueType
                                ? FlowValue.ForType(type)
                                : FlowValue.ForAllocation(
                                    type,
                                    decoder.MethodBase,
                                    instruction.ILOffset));
                        }
                        if (write.IncludesNull)
                        {
                            value.UnionWith(FlowValue.ForType(typeof(KnownNull)));
                        }
                        storage.StoreReferences(
                            callArguments[write.ParameterIndex],
                            value,
                            replace: write.IsOut,
                            locals,
                            currentArguments,
                            currentByRefWrites,
                            currentFields,
                            currentArrayElements);
                    }
                    if (collectReceivers
                        && called is MethodInfo virtualMethod
                        && receiver != null
                        && (opcode == OpCodes.Callvirt
                            || virtualMethod.DeclaringType == typeof(object)
                                && virtualMethod.Name == nameof(object.GetType)))
                    {
                        var callSite = new LocalVirtualCallSite(instruction.ILOffset, virtualMethod);
                        if (!result.VirtualReceivers.TryGetValue(callSite, out var receiverTypes))
                        {
                            receiverTypes = new FlowValue();
                            result.VirtualReceivers.Add(callSite, receiverTypes);
                        }
                        receiverTypes.UnionWith(receiver);
                    }
                    if (called is MethodInfo calledMethod && calledMethod.ReturnType != typeof(void))
                    {
                        if (receiver != null
                            && calledMethod.Name == nameof(Action.Invoke)
                            && typeof(MulticastDelegate).IsAssignableFrom(calledMethod.DeclaringType))
                        {
                            var delegateReturn = GetDelegateInvocationReturn(calledMethod, receiver);
                            if (!delegateReturn.IsEmpty)
                            {
                                PushCallReturn(delegateReturn);
                                goto CallComplete;
                            }
                        }
                        if (receiver != null && IsGenericEnumerableGetEnumerator(calledMethod))
                        {
                            var enumerators = receiver.Types
                                .Where(type => type.IsSZArray)
                                .Select(type => typeof(object).Assembly
                                    .GetType("System.SZGenericArrayEnumerator`1", throwOnError: true)!
                                    .MakeGenericType(type.GetElementType()!))
                                .ToArray();
                            if (enumerators.Length != 0)
                            {
                                PushCallReturn(FlowValue.ForTypes(
                                    enumerators,
                                    hasUnknownAllocation: true));
                                goto CallComplete;
                            }
                        }
                        if (receiver != null
                            && IsAwaitableResultExtractor(calledMethod)
                            && receiver.AwaitableResultTypes.Count != 0)
                        {
                            stack.Push(FlowValue.ForTypes(receiver.AwaitableResultTypes));
                            goto CallComplete;
                        }
                        var runtimeOverrideReturnTypes =
                            getRuntimeOverrideDependencyReturnTypes(calledMethod);
                        if (runtimeOverrideReturnTypes.Count != 0)
                        {
                            var runtimeOverrideReturn = new FlowValue();
                            foreach (var runtimeOverrideReturnType in runtimeOverrideReturnTypes)
                            {
                                runtimeOverrideReturn.UnionWith(FlowValue.ForAllocation(
                                    runtimeOverrideReturnType,
                                    decoder.MethodBase,
                                    instruction.ILOffset));
                            }
                            PushCallReturn(runtimeOverrideReturn);
                        }
                        else
                        {
                            var dispatchedReturns = new FlowValue();
                            if (calledMethod.Name == "get_InnerException"
                                && typeof(Exception).IsAssignableFrom(calledMethod.DeclaringType)
                                && receiver != null
                                && receiver.Types.Contains(typeof(TypeInitializationException)))
                            {
                                dispatchedReturns.UnionWith(FlowValue.ForTypes(
                                    exceptions
                                        .Where(entry => entry.Key is ConstructorInfo constructor
                                            && constructor.IsStatic)
                                        .SelectMany(entry => entry.Value),
                                    hasUnknownAllocation: true));
                            }
                            if (opcode == OpCodes.Callvirt && receiver != null)
                            {
                                var dispatchReceiverTypes = receiver.Types
                                    .Where(type => type != typeof(KnownNull)
                                        && (!type.IsInterface && !type.IsAbstract
                                            || type == typeof(Type)
                                            || typeof(MemberInfo).IsAssignableFrom(type)))
                                    .ToArray();
                                if (dispatchReceiverTypes.Length == 0)
                                {
                                    dispatchReceiverTypes = PossibleTypeGraphAnalyzer
                                        .CommonInterfaceFallbacks(calledMethod)
                                        .ToArray();
                                }
                                foreach (var receiverType in dispatchReceiverTypes)
                                {
                                    var target = GenericsHelper.GetPossiblyGenericReferenceFromMethod(
                                        PossibleTypeAnalysis.ResolveVirtualTarget(receiverType, calledMethod));
                                    if (target is not MethodInfo targetMethod)
                                    {
                                        continue;
                                    }
                                    if (returns.TryGetValue(targetMethod, out var targetReturns))
                                    {
                                        dispatchedReturns.UnionWith(targetReturns);
                                    }
                                    dispatchedReturns.UnionWith(
                                        ConcreteDeclaredValue(targetMethod.ReturnType));
                                }
                            }
                            var normalizedCalled = GenericsHelper.GetPossiblyGenericReferenceFromMethod(calledMethod);
                            if (!dispatchedReturns.IsEmpty)
                            {
                                PushCallReturn(dispatchedReturns);
                            }
                            else if (normalizedCalled is MethodInfo normalizedMethod
                                && returns.TryGetValue(normalizedMethod, out var returnTypes))
                            {
                                PushCallReturn(returnTypes.Clone());
                            }
                            else
                            {
                                PushCallReturn(ConcreteDeclaredValue(calledMethod.ReturnType));
                            }
                        }

                        void PushCallReturn(FlowValue value)
                        {
                            value.AwaitableResultTypes.UnionWith(
                                getRuntimeOverrideAwaitableResultTypes(calledMethod));
                            if (receiver != null && IsAwaitableCarrierMethod(calledMethod))
                            {
                                value.AwaitableResultTypes.UnionWith(receiver.AwaitableResultTypes);
                            }
                            if (inferTaskFactoryResults
                                && IsTaskFromResult(calledMethod)
                                && callArguments.Length == 1)
                            {
                                value.AwaitableResultTypes.UnionWith(callArguments[0].Types.Where(type =>
                                    type != typeof(KnownNull)));
                            }
                            stack.Push(value);
                        }
                    }
                CallComplete:;
                }
                else if (opcode == OpCodes.Ret)
                {
                    if (decoder.MethodBase is MethodInfo method && method.ReturnType != typeof(void))
                    {
                        result.ReturnValue.UnionWith(Pop(stack));
                    }
                    foreach (var write in currentByRefWrites
                        ?? Enumerable.Empty<KeyValuePair<int, FlowValue>>())
                    {
                        if (!result.ByRefWrites.TryGetValue(write.Key, out var written))
                        {
                            written = new FlowValue();
                            result.ByRefWrites.Add(write.Key, written);
                        }
                        written.UnionWith(write.Value);
                    }
                }
                else if (opcode == OpCodes.Throw)
                {
                    var thrownTypes = new HashSet<Type>(Pop(stack).Types);
                    if (thrownTypes.Remove(typeof(KnownNull)))
                    {
                        instructionExceptions.Add(typeof(NullReferenceException));
                    }
                    instructionExceptions.UnionWith(thrownTypes.Where(typeof(Exception).IsAssignableFrom));
                }
                else if (opcode == OpCodes.Rethrow)
                {
                    instructionExceptions.UnionWith(state.CurrentExceptions);
                }
                else if (opcode == OpCodes.Dup)
                {
                    var value = Pop(stack);
                    stack.Push(value);
                    stack.Push(value.Clone());
                }
                else if (opcode == OpCodes.Pop)
                {
                    Pop(stack);
                }
                else if (TryGetLocalIndex(opcode, instruction.Operand, out var localIndex))
                {
                    if (opcode.Name!.StartsWith("stloc", StringComparison.Ordinal))
                    {
                        locals[localIndex] = Pop(stack).Clone();
                    }
                    else if (opcode.Name.StartsWith("ldloca", StringComparison.Ordinal))
                    {
                        stack.Push(FlowValue.ForReference(StorageReference.ForLocal(localIndex)));
                    }
                    else
                    {
                        stack.Push(locals[localIndex].Clone());
                    }
                }
                else if (TryGetArgumentIndex(opcode, instruction.Operand, out var argumentIndex))
                {
                    if (opcode.Name!.StartsWith("starg", StringComparison.Ordinal))
                    {
                        currentArguments[argumentIndex] = Pop(stack).Clone();
                    }
                    else if (opcode.Name.StartsWith("ldarga", StringComparison.Ordinal)
                        || IsByRefArgument(decoder.MethodBase, argumentIndex))
                    {
                        stack.Push(FlowValue.ForReference(
                            StorageReference.ForArgument(argumentIndex)));
                    }
                    else
                    {
                        stack.Push(currentArguments[argumentIndex].Clone());
                    }
                }
                else if (opcode == OpCodes.Ldind_Ref)
                {
                    stack.Push(storage.LoadReferences(
                        Pop(stack),
                        locals,
                        currentArguments,
                        currentFields,
                        currentArrayElements));
                }
                else if (opcode == OpCodes.Stind_Ref)
                {
                    var value = Pop(stack);
                    storage.StoreReferences(
                        Pop(stack),
                        value,
                        replace: true,
                        locals,
                        currentArguments,
                        currentByRefWrites,
                        currentFields,
                        currentArrayElements);
                }
                else if (opcode == OpCodes.Ldstr)
                {
                    stack.Push(FlowValue.ForUnknownType(typeof(string)));
                }
                else if (opcode == OpCodes.Ldnull)
                {
                    stack.Push(FlowValue.ForType(typeof(KnownNull)));
                }
                else if (opcode == OpCodes.Box && instruction.Operand is Type boxedType)
                {
                    var valueTypes = Pop(stack);
                    stack.Push(boxedType.IsGenericParameter
                        ? valueTypes
                        : FlowValue.ForAllocation(
                            boxedType,
                            decoder.MethodBase,
                            instruction.ILOffset));
                }
                else if ((opcode == OpCodes.Castclass || opcode == OpCodes.Isinst)
                    && instruction.Operand is Type castType)
                {
                    var sourceTypes = Pop(stack);
                    var castValue = sourceTypes.Filter(castType);
                    if (sourceTypes.Types.Any(type => type != typeof(KnownNull)
                            && (type.IsAbstract || type.IsInterface)))
                    {
                        castValue.Types.Add(castType);
                    }
                    if (opcode == OpCodes.Isinst)
                    {
                        castValue.Types.Add(typeof(KnownNull));
                    }
                    stack.Push(castValue);
                }
                else if (opcode.Name!.StartsWith("ldc.i4", StringComparison.Ordinal))
                {
                    stack.Push(FlowValue.ForType(typeof(int)));
                }
                else if (opcode == OpCodes.Ldc_I8)
                {
                    stack.Push(FlowValue.ForType(typeof(long)));
                }
                else if (opcode == OpCodes.Ldc_R4)
                {
                    stack.Push(FlowValue.ForType(typeof(float)));
                }
                else if (opcode == OpCodes.Ldc_R8)
                {
                    stack.Push(FlowValue.ForType(typeof(double)));
                }
                else if (opcode == OpCodes.Newarr && instruction.Operand is Type elementType)
                {
                    Pop(stack);
                    stack.Push(FlowValue.ForAllocation(
                        elementType.MakeArrayType(),
                        decoder.MethodBase,
                        instruction.ILOffset));
                }
                else if (opcode == OpCodes.Ldelem_Ref
                    || opcode == OpCodes.Ldelem && instruction.Operand is Type loadedElementType && !loadedElementType.IsValueType)
                {
                    Pop(stack);
                    var arrays = Pop(stack);
                    stack.Push(storage.LoadArrayElements(arrays, currentArrayElements));
                }
                else if (opcode == OpCodes.Ldelema)
                {
                    Pop(stack);
                    stack.Push(storage.CreateArrayReferences(Pop(stack)));
                }
                else if (opcode == OpCodes.Stelem_Ref
                    || opcode == OpCodes.Stelem && instruction.Operand is Type storedElementType && !storedElementType.IsValueType)
                {
                    var value = Pop(stack);
                    Pop(stack);
                    var arrays = Pop(stack);
                    storage.RecordArrayWrite(arrays, value, currentArrayElements);
                }
                else if ((opcode == OpCodes.Ldfld || opcode == OpCodes.Ldsfld)
                    && instruction.Operand is FieldInfo loadedField)
                {
                    loadedField = GenericsHelper.GetPossiblyGenericReferenceFromField(loadedField);
                    var receiver = opcode == OpCodes.Ldfld ? Pop(stack) : null;
                    stack.Push(storage.LoadField(loadedField, receiver, currentFields));
                }
                else if ((opcode == OpCodes.Ldflda || opcode == OpCodes.Ldsflda)
                    && instruction.Operand is FieldInfo referencedField)
                {
                    referencedField = GenericsHelper.GetPossiblyGenericReferenceFromField(
                        referencedField);
                    var receiver = opcode == OpCodes.Ldflda ? Pop(stack) : null;
                    stack.Push(storage.CreateFieldReferences(referencedField, receiver));
                }
                else if ((opcode == OpCodes.Stfld || opcode == OpCodes.Stsfld)
                    && instruction.Operand is FieldInfo storedField)
                {
                    var value = Pop(stack);
                    storedField = GenericsHelper.GetPossiblyGenericReferenceFromField(storedField);
                    var receiver = opcode == OpCodes.Stfld ? Pop(stack) : null;
                    storage.RecordFieldWrite(storedField, receiver, value, currentFields);
                }
                else
                {
                    ApplyStackBehaviour(stack, opcode);
                }

                foreach (var successor in GetSuccessors(instructionIndex, instruction, instructions.Length, offsetIndexes))
                {
                    stateMerger.Merge(successor, state);
                }
                if (instructionExceptions.Count > 0)
                {
                    exceptionFlow.PropagateExceptions(
                        instruction,
                        instructionExceptions,
                        exceptionalCallState?.Locals ?? locals,
                        tracksArgumentStorage
                            ? exceptionalCallState?.Arguments ?? currentArguments
                            : null,
                        exceptionalCallState?.ByRefWrites ?? currentByRefWrites,
                        exceptionalCallState?.Fields ?? currentFields,
                        exceptionalCallState?.ArrayElements ?? currentArrayElements);
                }
            }

            static FlowValue GetDelegateInvocationReturn(
                MethodInfo invocation,
                FlowValue receiver)
            {
                var result = new FlowValue();
                var declaringType = invocation.DeclaringType!;
                if (!declaringType.IsGenericType)
                {
                    result.UnionWith(DeclaredDelegateReturn(invocation.ReturnType));
                    return result;
                }

                var definition = declaringType.GetGenericTypeDefinition();
                foreach (var receiverType in receiver.Types.Where(type =>
                    type.IsConstructedGenericType
                    && type.GetGenericTypeDefinition() == definition))
                {
                    var substitutions = new Dictionary<Type, Type>();
                    var compatible = true;
                    foreach (var pair in declaringType.GetGenericArguments()
                        .Zip(receiverType.GetGenericArguments()))
                    {
                        if (substitutions.TryGetValue(pair.First, out var existing))
                        {
                            compatible &= existing == pair.Second;
                        }
                        else
                        {
                            substitutions.Add(pair.First, pair.Second);
                        }
                    }
                    if (!compatible)
                    {
                        continue;
                    }
                    result.UnionWith(DeclaredDelegateReturn(
                        SubstituteGenericParameters(invocation.ReturnType, substitutions)));
                }
                return result;

                static FlowValue DeclaredDelegateReturn(Type type)
                    => (type.IsInterface || type.IsAbstract)
                        && type.Assembly != typeof(object).Assembly
                        ? FlowValue.ForUnknownType(type)
                        : ConcreteDeclaredValue(type);
            }

            static Type SubstituteGenericParameters(
                Type type,
                IReadOnlyDictionary<Type, Type> substitutions)
            {
                if (type.IsGenericParameter
                    && substitutions.TryGetValue(type, out var substituted))
                {
                    return substituted;
                }
                if (type.IsArray)
                {
                    var elementType = SubstituteGenericParameters(
                        type.GetElementType()!,
                        substitutions);
                    return type.IsSZArray
                        ? elementType.MakeArrayType()
                        : elementType.MakeArrayType(type.GetArrayRank());
                }
                if (!type.IsConstructedGenericType)
                {
                    return type;
                }
                return type.GetGenericTypeDefinition().MakeGenericType(
                    type.GetGenericArguments()
                        .Select(argument => SubstituteGenericParameters(argument, substitutions))
                        .ToArray());
            }
            return result;

        }
    }
}
