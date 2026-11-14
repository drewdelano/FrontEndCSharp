using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.PossibleType;
using static Compiler.Read.RepresentedTypeFlowOperations;
using CallFlow = Compiler.Read.RepresentedTypeFlowModels.CallFlow;
using Result = Compiler.Read.RepresentedTypeFlowModels.Result;
using State = Compiler.Read.RepresentedTypeFlowModels.State;
using Value = Compiler.Read.RepresentedTypeFlowModels.Value;

namespace Compiler.Read
{
    internal static class RepresentedTypeMethodAnalyzer
    {
        internal static Result Analyze(
            MethodDecoder decoder,
            IReadOnlyDictionary<MethodInfo, Value> returns,
            IReadOnlyList<Value> arguments,
            IReadOnlyDictionary<FieldInfo, Value> fields,
            IReadOnlyDictionary<Type, Value> arrayElements,
            IReadOnlyDictionary<VirtualCallSite, HashSet<Type>> runtimeReceivers,
            RepresentedTypeAnalysis.Demand demand)
        {
            var instructions = decoder.InstructionBlocks.SelectMany(block => block.Instructions).ToArray();
            var result = new Result();
            if (instructions.Length == 0)
            {
                return result;
            }
            var offsetIndexes = new Dictionary<int, int>(instructions.Length);
            for (var index = 0; index < instructions.Length; index++)
            {
                offsetIndexes.Add(instructions[index].ILOffset, index);
            }
            var localCount = decoder.MethodBody?.LocalVariables.Count ?? 0;
            var states = new Dictionary<int, State> { [0] = new State([], NewValues(localCount)) };
            var pending = new Queue<int>();
            pending.Enqueue(0);
            if (decoder.MethodBody != null)
            {
                foreach (var clause in decoder.MethodBody.ExceptionHandlingClauses)
                {
                    var targetOffset = clause.Flags == ExceptionHandlingClauseOptions.Filter
                        ? clause.FilterOffset
                        : clause.HandlerOffset;
                    if (!offsetIndexes.TryGetValue(targetOffset, out var handlerIndex))
                    {
                        continue;
                    }
                    var handlerStack = new Stack<Value>();
                    if (clause.Flags == ExceptionHandlingClauseOptions.Clause
                        || clause.Flags == ExceptionHandlingClauseOptions.Filter)
                    {
                        handlerStack.Push(new Value());
                    }
                    MergeState(handlerIndex, new State(handlerStack, NewValues(localCount)));
                }
            }

            while (pending.Count > 0)
            {
                var instructionIndex = pending.Dequeue();
                var state = states[instructionIndex].Clone();
                var stack = state.Stack;
                var locals = state.Locals;
                var instruction = instructions[instructionIndex];
                var opcode = instruction.OpCode;

                if (opcode == OpCodes.Ldtoken && instruction.Operand is TokenMetadata token)
                {
                    var value = new Value();
                    if (token.TokenType == "Type" && token.SourceType != null)
                    {
                        value.Types.Add(NormalizeType(token.SourceType));
                    }
                    if (token.TokenType == "Method" && token.SourceMethod != null)
                    {
                        value.Methods.Add(token.SourceMethod);
                    }
                    stack.Push(value);
                }
                else if (opcode == OpCodes.Newobj && instruction.Operand is ConstructorInfo constructor)
                {
                    var callArguments = PopArguments(stack, constructor.GetParameters().Length);
                    result.Calls.Add(new CallFlow(
                        constructor,
                        new[] { new Value() }.Concat(callArguments).ToArray()));
                    stack.Push(new Value());
                }
                else if ((opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
                    && instruction.Operand is MethodBase called)
                {
                    var callArguments = PopArguments(stack, called.GetParameters().Length);
                    var receiver = called.IsStatic ? null : Pop(stack);
                    result.Calls.Add(new CallFlow(
                        called,
                        (receiver == null ? Enumerable.Empty<Value>() : new[] { receiver })
                            .Concat(callArguments)
                            .ToArray()));

                    if (demand != null)
                    {
                        if (IsTypeMetadataQuery(called))
                        {
                            if (receiver == null || receiver.Types.Count == 0)
                            {
                                throw new InvalidOperationException(
                                    $"The compiler could not prove a represented managed type for reachable reflection call " +
                                    $"'{called}' in '{decoder.MethodBase}'.");
                            }
                            demand.Types.UnionWith(receiver.Types);
                            if (called.Name == "GetFields")
                            {
                                demand.FieldTypes.UnionWith(receiver.Types);
                            }
                        }
                        if (IsEnumMetadataQuery(called))
                        {
                            if (receiver == null || receiver.Types.Count == 0)
                            {
                                throw new InvalidOperationException(
                                    $"The compiler could not prove a represented managed type for reachable enum reflection call " +
                                    $"'{called}' in '{decoder.MethodBase}'.");
                            }
                            demand.EnumTypes.UnionWith(receiver.Types);
                        }
                        if (IsGetMethodFromHandle(called))
                        {
                            if (callArguments.Length == 0 || callArguments[0].Methods.Count == 0)
                            {
                                throw new InvalidOperationException(
                                    $"The compiler could not prove a managed method for reachable method-handle reflection call " +
                                    $"'{called}' in '{decoder.MethodBase}'.");
                            }
                            demand.Types.UnionWith(callArguments[0].Methods
                                .Where(method => method.DeclaringType != null)
                                .Select(method => NormalizeType(method.DeclaringType)));
                        }
                        if (called.DeclaringType == typeof(PropertyInfo)
                            && called.Name == nameof(PropertyInfo.GetValue))
                        {
                            var properties = receiver?.Members.OfType<PropertyInfo>().ToArray() ?? [];
                            RequireMembers(properties, called);
                            demand.ReadProperties.UnionWith(properties);
                        }
                        if (called.DeclaringType == typeof(PropertyInfo)
                            && called.Name == nameof(PropertyInfo.SetValue))
                        {
                            var properties = receiver?.Members.OfType<PropertyInfo>().ToArray() ?? [];
                            RequireMembers(properties, called);
                            demand.WrittenProperties.UnionWith(properties);
                        }
                        if (typeof(MethodBase).IsAssignableFrom(called.DeclaringType)
                            && called.Name == nameof(MethodBase.Invoke))
                        {
                            var methods = receiver?.Members.OfType<MethodInfo>().ToArray() ?? [];
                            RequireMembers(methods, called);
                            demand.InvokedMethods.UnionWith(methods);
                        }
                        if (called is MethodInfo expressionMethod
                            && expressionMethod.Name == nameof(Expression<Action>.Compile)
                            && expressionMethod.DeclaringType?.IsGenericType == true
                            && expressionMethod.DeclaringType.GetGenericTypeDefinition()
                                == typeof(Expression<>))
                        {
                            demand.ReadProperties.UnionWith(
                                (receiver?.LambdaBodyMembers ?? [])
                                    .OfType<PropertyInfo>());
                        }
                    }

                    if (called is MethodInfo calledMethod && calledMethod.ReturnType != typeof(void))
                    {
                        if (IsGetTypeFromHandle(calledMethod))
                        {
                            stack.Push(callArguments.Length == 0 ? new Value() : callArguments[0].Clone());
                        }
                        else if (IsGetMethodFromHandle(calledMethod))
                        {
                            var value = new Value();
                            if (callArguments.Length > 0)
                            {
                                value.Members.UnionWith(callArguments[0].Methods);
                            }
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(Expression)
                            && calledMethod.Name == nameof(Expression.Parameter))
                        {
                            var value = new Value();
                            if (callArguments.Length > 0)
                            {
                                value.ExpressionTypes.UnionWith(callArguments[0].Types);
                            }
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(Expression)
                            && calledMethod.Name == nameof(Expression.Property))
                        {
                            var value = new Value();
                            if (callArguments.Length > 0)
                            {
                                value.ExpressionReceiverTypes.UnionWith(
                                    callArguments[0].ExpressionTypes);
                            }
                            if (callArguments.Length > 1)
                            {
                                foreach (var property in ResolveAccessorProperties(
                                    callArguments[1].Members.OfType<MethodInfo>()))
                                {
                                    value.ExpressionMembers.Add(property);
                                    value.ExpressionTypes.Add(NormalizeType(property.PropertyType));
                                }
                            }
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(Expression)
                            && calledMethod.Name == nameof(Expression.Lambda))
                        {
                            var value = new Value();
                            if (callArguments.Length > 0)
                            {
                                var body = callArguments[0];
                                value.LambdaReturnTypes.UnionWith(body.ExpressionTypes);
                                value.LambdaBodyTypes.UnionWith(body.ExpressionTypes);
                                value.LambdaBodyMembers.UnionWith(body.ExpressionMembers);
                                value.LambdaBodyReceiverTypes.UnionWith(body.ExpressionReceiverTypes);
                            }
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(LambdaExpression)
                            && calledMethod.Name == "get_Body")
                        {
                            var value = new Value();
                            if (receiver != null)
                            {
                                value.ExpressionTypes.UnionWith(receiver.LambdaBodyTypes);
                                value.ExpressionMembers.UnionWith(receiver.LambdaBodyMembers);
                                value.ExpressionReceiverTypes.UnionWith(
                                    receiver.LambdaBodyReceiverTypes);
                            }
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(LambdaExpression)
                            && calledMethod.Name == "get_ReturnType")
                        {
                            var value = new Value();
                            if (receiver != null)
                            {
                                value.Types.UnionWith(receiver.LambdaReturnTypes);
                            }
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(MemberExpression)
                            && calledMethod.Name == "get_Member")
                        {
                            var value = new Value();
                            if (receiver != null)
                            {
                                value.Members.UnionWith(receiver.ExpressionMembers);
                            }
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(MemberExpression)
                            && calledMethod.Name == "get_Expression")
                        {
                            var value = new Value();
                            if (receiver != null)
                            {
                                value.ExpressionTypes.UnionWith(
                                    receiver.ExpressionReceiverTypes);
                            }
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(Type)
                            && (calledMethod.Name == "GetProperties"
                                || calledMethod.Name == "GetMethods"
                                || calledMethod.Name == "GetFields"))
                        {
                            var value = new Value();
                            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static
                                | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                            foreach (var representedType in receiver?.Types ?? [])
                            {
                                if (calledMethod.Name == "GetProperties")
                                {
                                    value.Members.UnionWith(representedType.GetProperties(flags));
                                }
                                else if (calledMethod.Name == "GetMethods")
                                {
                                    value.Members.UnionWith(representedType.GetMethods(flags));
                                }
                                else
                                {
                                    value.Members.UnionWith(representedType.GetFields());
                                }
                            }
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(PropertyInfo)
                            && calledMethod.Name == "get_PropertyType")
                        {
                            var value = new Value();
                            value.Types.UnionWith((receiver?.Members ?? [])
                                .OfType<PropertyInfo>()
                                .Select(property => NormalizeType(property.PropertyType)));
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(FieldInfo)
                            && calledMethod.Name == "get_FieldType")
                        {
                            var value = new Value();
                            value.Types.UnionWith((receiver?.Members ?? [])
                                .OfType<FieldInfo>()
                                .Select(field => NormalizeType(field.FieldType)));
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(MethodInfo)
                            && calledMethod.Name == "get_ReturnType")
                        {
                            var value = new Value();
                            value.Types.UnionWith((receiver?.Members ?? [])
                                .OfType<MethodInfo>()
                                .Select(method => NormalizeType(method.ReturnType)));
                            stack.Push(value);
                        }
                        else if (calledMethod.DeclaringType == typeof(object)
                            && calledMethod.Name == nameof(object.GetType))
                        {
                            var value = new Value();
                            var callSite = new VirtualCallSite(
                                decoder.MethodBase,
                                instruction.ILOffset,
                                calledMethod);
                            if (runtimeReceivers.TryGetValue(callSite, out var receiverTypes))
                            {
                                value.Types.UnionWith(receiverTypes
                                    .Where(type => !PossibleTypeAnalysis.IsKnownNull(type))
                                    .Select(NormalizeType));
                            }
                            stack.Push(value);
                        }
                        else
                        {
                            var normalized = GenericsHelper.GetPossiblyGenericReferenceFromMethod(calledMethod);
                            stack.Push(normalized is MethodInfo normalizedMethod
                                && returns.TryGetValue(normalizedMethod, out var returnValue)
                                    ? returnValue.Clone()
                                    : new Value());
                        }
                    }
                }
                else if (opcode == OpCodes.Ret)
                {
                    if (decoder.MethodBase is MethodInfo method && method.ReturnType != typeof(void))
                    {
                        result.ReturnValue.UnionWith(Pop(stack));
                    }
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
                    else
                    {
                        stack.Push(locals[localIndex].Clone());
                    }
                }
                else if (TryGetArgumentIndex(opcode, instruction.Operand, out var argumentIndex))
                {
                    if (opcode.Name!.StartsWith("starg", StringComparison.Ordinal))
                    {
                        arguments[argumentIndex].UnionWith(Pop(stack));
                    }
                    else
                    {
                        stack.Push(arguments[argumentIndex].Clone());
                    }
                }
                else if (opcode == OpCodes.Newarr && instruction.Operand is Type elementType)
                {
                    Pop(stack);
                    var array = new Value();
                    array.ArrayTypes.Add(elementType.MakeArrayType());
                    stack.Push(array);
                }
                else if (opcode == OpCodes.Ldelem_Ref
                    || opcode == OpCodes.Ldelem && instruction.Operand is Type loadedElementType && !loadedElementType.IsValueType)
                {
                    Pop(stack);
                    var array = Pop(stack);
                    var loadedValue = new Value();
                    loadedValue.Members.UnionWith(array.Members);
                    foreach (var arrayType in array.ArrayTypes)
                    {
                        if (arrayElements.TryGetValue(arrayType, out var elementValue))
                        {
                            loadedValue.UnionWith(elementValue);
                        }
                    }
                    stack.Push(loadedValue);
                }
                else if (opcode == OpCodes.Stelem_Ref
                    || opcode == OpCodes.Stelem && instruction.Operand is Type storedElementType && !storedElementType.IsValueType)
                {
                    var value = Pop(stack);
                    Pop(stack);
                    var array = Pop(stack);
                    foreach (var arrayType in array.ArrayTypes)
                    {
                        if (!result.ArrayWrites.TryGetValue(arrayType, out var writtenValue))
                        {
                            writtenValue = new Value();
                            result.ArrayWrites.Add(arrayType, writtenValue);
                        }
                        writtenValue.UnionWith(value);
                    }
                }
                else if ((opcode == OpCodes.Ldfld || opcode == OpCodes.Ldsfld)
                    && instruction.Operand is FieldInfo loadedField)
                {
                    if (opcode == OpCodes.Ldfld)
                    {
                        Pop(stack);
                    }
                    loadedField = GenericsHelper.GetPossiblyGenericReferenceFromField(loadedField);
                    stack.Push(fields.TryGetValue(loadedField, out var fieldValue)
                        ? fieldValue.Clone()
                        : new Value());
                }
                else if ((opcode == OpCodes.Stfld || opcode == OpCodes.Stsfld)
                    && instruction.Operand is FieldInfo storedField)
                {
                    var value = Pop(stack);
                    if (opcode == OpCodes.Stfld)
                    {
                        Pop(stack);
                    }
                    storedField = GenericsHelper.GetPossiblyGenericReferenceFromField(storedField);
                    if (!result.FieldWrites.TryGetValue(storedField, out var writtenValue))
                    {
                        writtenValue = new Value();
                        result.FieldWrites.Add(storedField, writtenValue);
                    }
                    writtenValue.UnionWith(value);
                }
                else if (opcode == OpCodes.Box || opcode == OpCodes.Unbox_Any
                    || opcode == OpCodes.Castclass || opcode == OpCodes.Isinst)
                {
                    var value = Pop(stack);
                    stack.Push(value);
                }
                else
                {
                    Pop(stack, PopCount(opcode.StackBehaviourPop));
                    for (var index = 0; index < PushCount(opcode.StackBehaviourPush); index++)
                    {
                        stack.Push(new Value());
                    }
                }

                foreach (var successor in GetSuccessors(instructionIndex, instruction, instructions.Length, offsetIndexes))
                {
                    MergeState(successor, state);
                }
                if (decoder.MethodBody != null)
                {
                    foreach (var clause in decoder.MethodBody.ExceptionHandlingClauses.Where(clause =>
                        clause.TryOffset <= instruction.ILOffset
                        && instruction.ILOffset < clause.TryOffset + clause.TryLength))
                    {
                        var targetOffset = clause.Flags == ExceptionHandlingClauseOptions.Filter
                            ? clause.FilterOffset
                            : clause.HandlerOffset;
                        if (!offsetIndexes.TryGetValue(targetOffset, out var exceptionTarget))
                        {
                            continue;
                        }
                        var exceptionStack = new Stack<Value>();
                        if (clause.Flags == ExceptionHandlingClauseOptions.Clause
                            || clause.Flags == ExceptionHandlingClauseOptions.Filter)
                        {
                            exceptionStack.Push(new Value());
                        }
                        MergeState(
                            exceptionTarget,
                            new State(exceptionStack, locals.Select(value => value.Clone()).ToArray()));
                    }
                }
            }
            return result;

            void RequireMembers<TMember>(IReadOnlyCollection<TMember> members, MethodBase reflectionCall)
                where TMember : MemberInfo
            {
                if (members.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The compiler could not prove a reflected managed member for reachable reflection call " +
                        $"'{reflectionCall}' in '{decoder.MethodBase}'.");
                }
            }

            void MergeState(int index, State incoming)
            {
                if (!states.TryGetValue(index, out var existing))
                {
                    states.Add(index, incoming.Clone());
                    pending.Enqueue(index);
                }
                else if (existing.Merge(incoming))
                {
                    pending.Enqueue(index);
                }
            }
        }

        private static bool IsTypeMetadataQuery(MethodBase method)
            => method.DeclaringType == typeof(Type)
                && (method.Name == "GetProperties"
                    || method.Name == "GetMethods"
                    || method.Name == "GetFields"
                    || method.Name == "GetCustomAttributes");

        private static bool IsEnumMetadataQuery(MethodBase method)
            => method.DeclaringType == typeof(Type)
                && (method.Name == "get_IsEnum"
                    || method.Name == "GetEnumUnderlyingType"
                    || method.Name == "GetEnumNames"
                    || method.Name == "GetEnumValues"
                    || method.Name == "GetEnumValuesAsUnderlyingType"
                    || method.Name == "GetEnumName"
                    || method.Name == "IsEnumDefined");

        private static bool IsGetTypeFromHandle(MethodInfo method)
            => method.DeclaringType == typeof(Type) && method.Name == nameof(Type.GetTypeFromHandle);

        private static bool IsGetMethodFromHandle(MethodBase method)
            => method.DeclaringType == typeof(MethodBase) && method.Name == nameof(MethodBase.GetMethodFromHandle);

        private static TypeInfo NormalizeType(Type type)
        {
            while (type.HasElementType)
            {
                type = type.GetElementType()!;
            }
            return GenericsHelper.GetPossiblyGenericReferenceFromType((TypeInfo)type);
        }

        private static IEnumerable<PropertyInfo> ResolveAccessorProperties(
            IEnumerable<MethodInfo> accessors)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var accessor in accessors)
            {
                if (accessor.DeclaringType == null)
                {
                    continue;
                }
                foreach (var property in accessor.DeclaringType.GetProperties(flags))
                {
                    if (HasSameDefinition(property.GetMethod, accessor)
                        || HasSameDefinition(property.SetMethod, accessor))
                    {
                        yield return property;
                    }
                }
            }
        }

        private static bool HasSameDefinition(MethodInfo candidate, MethodInfo expected)
            => candidate != null
                && (candidate == expected || candidate.HasSameMetadataDefinitionAs(expected));

    }
}
