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
    internal static class PossibleTypeGraphAnalyzer
    {
        internal static IReadOnlyDictionary<VirtualCallSite, HashSet<Type>> FindVirtualReceivers(
            ReaderContext context,
            out IReadOnlyDictionary<MethodBase, HashSet<int>> reachableOffsets,
            out IReadOnlyDictionary<MethodBase, IReadOnlyList<CallArgumentTypes>> callArgumentTypes,
            out IReadOnlyDictionary<MethodInfo, CallArgumentTypes> methodReturnTypes)
        {
            var decoders = context.OrderToOutput.Values
                .Select(value => value.MethodDecoder)
                .ToDictionary(decoder => decoder.MethodBase);
            var reachableAllocationTypes = decoders.Values
                .SelectMany(decoder => decoder.InstructionBlocks)
                .SelectMany(block => block.Instructions)
                .Where(instruction => instruction.OpCode == OpCodes.Newobj)
                .Select(instruction => instruction.Operand)
                .OfType<ConstructorInfo>()
                .Select(constructor => constructor.DeclaringType!)
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .ToHashSet();
            var returns = decoders.Keys
                .OfType<MethodInfo>()
                .Where(method => !context.RuntimeOverrides.HasRuntimeOverride(method))
                .ToDictionary(method => method, _ => new FlowValue());
            var exceptions = decoders.Keys
                .Where(method => !context.RuntimeOverrides.HasRuntimeOverride(method))
                .ToDictionary(method => method, _ => new HashSet<Type>());
            var arguments = decoders.Keys.ToDictionary(
                method => method,
                method => Enumerable.Range(0, method.GetParameters().Length + (method.IsStatic ? 0 : 1))
                    .Select(_ => new FlowValue())
                    .ToArray());
            var fields = new Dictionary<FieldLocation, FlowValue>();
            var arrayElements = new Dictionary<ArrayLocation, FlowValue>();
            var byRefWrites = decoders.Keys
                .Where(method => !context.RuntimeOverrides.HasRuntimeOverride(method))
                .ToDictionary(method => method, _ => new Dictionary<int, FlowValue>());
            if (arguments.TryGetValue(context.EntryPoint, out var entryArguments))
            {
                var entryParameters = context.EntryPoint.GetParameters();
                for (var index = 0; index < entryParameters.Length; index++)
                {
                    entryArguments[index].UnionWith(ConcreteDeclaredValue(entryParameters[index].ParameterType));
                }
            }
            foreach (var delegateTarget in decoders.Values
                .SelectMany(decoder => decoder.InstructionBlocks)
                .SelectMany(block => block.Instructions)
                .Where(instruction => instruction.OpCode is var opcode
                    && (opcode == OpCodes.Ldftn || opcode == OpCodes.Ldvirtftn))
                .Select(instruction => instruction.Operand)
                .OfType<MethodBase>())
            {
                var normalizedTarget = GenericsHelper.GetPossiblyGenericReferenceFromMethod(delegateTarget);
                if (!arguments.TryGetValue(normalizedTarget, out var targetArguments))
                {
                    continue;
                }
                var offset = normalizedTarget.IsStatic ? 0 : 1;
                if (!normalizedTarget.IsStatic)
                {
                    targetArguments[0].UnionWith(ConcreteDeclaredValue(normalizedTarget.DeclaringType!));
                }
                var parameters = normalizedTarget.GetParameters();
                for (var index = 0; index < parameters.Length; index++)
                {
                    targetArguments[index + offset].UnionWith(ConcreteDeclaredValue(parameters[index].ParameterType));
                }
            }
            foreach (var awaiterCallback in decoders.Values
                .SelectMany(decoder => decoder.InstructionBlocks)
                .SelectMany(block => block.Instructions)
                .Select(instruction => instruction.Operand)
                .OfType<MethodInfo>()
                .Where(method => method.IsConstructedGenericMethod && method.Name == "AwaitOnCompleted")
                .Select(method => method.GetGenericArguments()[0].GetMethod(
                    "OnCompleted",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    new[] { typeof(Action) },
                    modifiers: null))
                .Where(method => method != null))
            {
                var normalizedCallback = GenericsHelper.GetPossiblyGenericReferenceFromMethod(awaiterCallback!);
                if (!arguments.TryGetValue(normalizedCallback, out var callbackArguments))
                {
                    continue;
                }
                callbackArguments[0].UnionWith(ConcreteDeclaredValue(normalizedCallback.DeclaringType!));
                var callbackParameters = normalizedCallback.GetParameters();
                for (var index = 0; index < callbackParameters.Length; index++)
                {
                    callbackArguments[index + 1].UnionWith(
                        ConcreteDeclaredValue(callbackParameters[index].ParameterType));
                }
            }

            while (true)
            {
                var changed = false;
                foreach (var decoder in decoders.Values)
                {
                    if (context.RuntimeOverrides.HasRuntimeOverride(decoder.MethodBase))
                    {
                        continue;
                    }
                    var analysis = PossibleTypeMethodAnalyzer.Analyze(
                        decoder,
                        returns,
                        arguments[decoder.MethodBase],
                        fields,
                        arrayElements,
                        exceptions,
                        context.RuntimeOverrides.HasRuntimeOverride,
                        method => GetRuntimeOverrideDependencyReturnTypes(context, method),
                        method => GetRuntimeOverrideAwaitableResultTypes(context, method),
                        method => GetByRefParameterTypes(context, method, byRefWrites),
                        inferTaskFactoryResults: false,
                        collectReceivers: false);
                    if (analysis != null)
                    {
                        if (decoder.MethodBase is MethodInfo method
                            && returns.TryGetValue(method, out var result))
                        {
                            changed |= result.UnionWith(analysis.ReturnValue);
                        }
                        if (exceptions.TryGetValue(decoder.MethodBase, out var escapingExceptions))
                        {
                            changed |= escapingExceptions.UnionWithChanged(
                                analysis.EscapingExceptions);
                        }
                        foreach (var call in analysis.Calls)
                        {
                            var callTargets = new HashSet<MethodBase>
                            {
                                GenericsHelper.GetPossiblyGenericReferenceFromMethod(call.Method)
                            };
                            if (call.Method is MethodInfo { IsVirtual: true } virtualMethod
                                && call.Arguments.Length != 0)
                            {
                                var receiverTypes = call.Arguments[0].Types.Where(type =>
                                    type != typeof(KnownNull)
                                    && (!type.IsInterface && !type.IsAbstract
                                        || type == typeof(Type)
                                        || typeof(MemberInfo).IsAssignableFrom(type))).ToArray();
                                var unresolvedContracts = call.Arguments[0].UnknownAllocationTypes
                                    .Where(type => (type.IsInterface || type.IsAbstract)
                                        && type != typeof(Type)
                                        && !typeof(MemberInfo).IsAssignableFrom(type))
                                    .ToArray();
                                if (unresolvedContracts.Length != 0)
                                {
                                    receiverTypes = receiverTypes
                                        .Concat(reachableAllocationTypes.Where(type =>
                                            unresolvedContracts.Any(contract =>
                                                CanImplement(type, contract))))
                                        .Distinct()
                                        .ToArray();
                                }
                                if (receiverTypes.Length == 0)
                                {
                                    receiverTypes = CommonInterfaceFallbacks(virtualMethod).ToArray();
                                }
                                if (receiverTypes.Length == 0
                                    && virtualMethod.DeclaringType!.Assembly != typeof(object).Assembly)
                                {
                                    receiverTypes = reachableAllocationTypes.Where(type =>
                                        type.Assembly == virtualMethod.DeclaringType.Assembly
                                        && CanImplement(type, virtualMethod.DeclaringType)).ToArray();
                                }
                                foreach (var receiverType in receiverTypes)
                                {
                                    callTargets.Add(GenericsHelper.GetPossiblyGenericReferenceFromMethod(
                                        PossibleTypeAnalysis.ResolveVirtualTarget(receiverType, virtualMethod)));
                                }
                                var normalizedVirtualMethod = (MethodInfo)
                                    GenericsHelper.GetPossiblyGenericReferenceFromMethod(virtualMethod);
                                if (returns.TryGetValue(normalizedVirtualMethod, out var virtualReturn))
                                {
                                    foreach (var target in callTargets.OfType<MethodInfo>())
                                    {
                                        if (target != normalizedVirtualMethod
                                            && returns.TryGetValue(target, out var targetReturn))
                                        {
                                            changed |= virtualReturn.UnionWith(targetReturn);
                                        }
                                    }
                                }
                            }
                            foreach (var called in callTargets)
                            {
                                if (!arguments.TryGetValue(called, out var targetArguments))
                                {
                                    continue;
                                }
                                for (var index = 0; index < Math.Min(targetArguments.Length, call.Arguments.Length); index++)
                                {
                                    changed |= targetArguments[index].UnionWith(call.Arguments[index]);
                                }
                            }
                        }
                        foreach (var write in analysis.FieldWrites)
                        {
                            if (!fields.TryGetValue(write.Key, out var fieldValue))
                            {
                                fieldValue = new FlowValue();
                                fields.Add(write.Key, fieldValue);
                            }
                            changed |= fieldValue.UnionWith(write.Value);
                        }
                        foreach (var write in analysis.ArrayWrites)
                        {
                            if (!arrayElements.TryGetValue(write.Key, out var elementValue))
                            {
                                elementValue = new FlowValue();
                                arrayElements.Add(write.Key, elementValue);
                            }
                            changed |= elementValue.UnionWith(write.Value);
                        }
                        foreach (var write in analysis.ByRefWrites)
                        {
                            var methodWrites = byRefWrites[decoder.MethodBase];
                            if (!methodWrites.TryGetValue(write.Key, out var writtenValue))
                            {
                                writtenValue = new FlowValue();
                                methodWrites.Add(write.Key, writtenValue);
                            }
                            changed |= writtenValue.UnionWith(write.Value);
                        }
                    }
                }
                if (!changed)
                {
                    break;
                }
            }

            var receivers = new Dictionary<VirtualCallSite, HashSet<Type>>();
            var offsets = new Dictionary<MethodBase, HashSet<int>>();
            foreach (var decoder in decoders.Values)
            {
                if (context.RuntimeOverrides.HasRuntimeOverride(decoder.MethodBase))
                {
                    continue;
                }
                var analysis = PossibleTypeMethodAnalyzer.Analyze(
                    decoder,
                    returns,
                    arguments[decoder.MethodBase],
                    fields,
                    arrayElements,
                    exceptions,
                    context.RuntimeOverrides.HasRuntimeOverride,
                    method => GetRuntimeOverrideDependencyReturnTypes(context, method),
                    method => GetRuntimeOverrideAwaitableResultTypes(context, method),
                    method => GetByRefParameterTypes(context, method, byRefWrites),
                    inferTaskFactoryResults: false,
                    collectReceivers: true);
                if (analysis == null)
                {
                    continue;
                }
                offsets.Add(decoder.MethodBase, analysis.ReachableOffsets);
                foreach (var call in analysis.VirtualReceivers)
                {
                    var receiverTypes = call.Value.Types.ToHashSet();
                    var unresolvedContracts = call.Value.UnknownAllocationTypes
                        .Where(type => (type.IsInterface || type.IsAbstract)
                            && type != typeof(Type)
                            && !typeof(MemberInfo).IsAssignableFrom(type))
                        .ToArray();
                    receiverTypes.RemoveWhere(type => unresolvedContracts.Contains(type));
                    if (unresolvedContracts.Length != 0)
                    {
                        receiverTypes.UnionWith(reachableAllocationTypes.Where(type =>
                            unresolvedContracts.Any(contract => CanImplement(type, contract))));
                    }
                    if (receiverTypes.Count == 0)
                    {
                        receiverTypes.UnionWith(CommonInterfaceFallbacks(call.Key.Method));
                    }
                    if (receiverTypes.Count == 0
                        && call.Key.Method.DeclaringType!.Assembly != typeof(object).Assembly)
                    {
                        receiverTypes.UnionWith(reachableAllocationTypes.Where(type =>
                            type.Assembly == call.Key.Method.DeclaringType.Assembly
                            && CanImplement(type, call.Key.Method.DeclaringType)));
                    }
                    receivers.Add(
                        new VirtualCallSite(decoder.MethodBase, call.Key.Offset, call.Key.Method),
                        receiverTypes);
                }
            }
            reachableOffsets = offsets;
            callArgumentTypes = arguments.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<CallArgumentTypes>)pair.Value
                    .Select(value => new CallArgumentTypes(
                        new HashSet<Type>(value.Types),
                        new HashSet<Type>(value.UnknownAllocationTypes)))
                    .ToArray());
            methodReturnTypes = returns.ToDictionary(
                pair => pair.Key,
                pair => new CallArgumentTypes(
                    new HashSet<Type>(pair.Value.Types),
                    new HashSet<Type>(pair.Value.UnknownAllocationTypes)));
            return receivers;
        }

        internal static IEnumerable<Type> CommonInterfaceFallbacks(MethodInfo method)
        {
            var declaringType = method.DeclaringType;
            if (method.Name == nameof(IEnumerable<object>.GetEnumerator)
                && declaringType?.IsGenericType == true
                && declaringType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                yield return typeof(List<>).MakeGenericType(declaringType.GetGenericArguments());
            }
            else if (declaringType?.IsGenericType == true
                && declaringType.GetGenericTypeDefinition() == typeof(IEnumerator<>))
            {
                yield return typeof(List<>.Enumerator).MakeGenericType(
                    declaringType.GetGenericArguments());
            }
            else if (declaringType == typeof(System.Collections.IEnumerator))
            {
                yield return typeof(List<object>.Enumerator);
            }
            else if (declaringType == typeof(IDisposable)
                && method.Name == nameof(IDisposable.Dispose))
            {
                // Generic foreach lowers disposal through the non-generic
                // IDisposable contract. When the enumerator's generic return
                // type has been erased, retain the common list-enumerator
                // implementation; companion-specific enumerators are retained
                // by their provider dependencies.
                yield return typeof(List<object>.Enumerator);
            }
        }

        private static bool CanImplement(Type candidate, Type contract)
        {
            if (contract.IsAssignableFrom(candidate))
            {
                return true;
            }
            if (!contract.IsGenericType)
            {
                return false;
            }
            var definition = contract.GetGenericTypeDefinition();
            if (candidate.GetInterfaces().Any(type => type.IsGenericType
                && type.GetGenericTypeDefinition() == definition))
            {
                return true;
            }
            for (var current = candidate; current != null; current = current.BaseType)
            {
                if (current.IsGenericType
                    && current.GetGenericTypeDefinition() == definition)
                {
                    return true;
                }
            }
            return false;
        }

    }
}
