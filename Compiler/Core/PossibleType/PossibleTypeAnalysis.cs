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
    internal static class PossibleTypeAnalysis
    {
        internal static IReadOnlyCollection<Type> FindAwaitableResultTypes(MethodDecoder decoder)
        {
            if (decoder.MethodBase is not MethodInfo method
                || !method.ReturnType.IsConstructedGenericType
                || method.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
            {
                return Array.Empty<Type>();
            }

            var argumentTypes = new List<FlowValue>();
            if (!method.IsStatic)
            {
                argumentTypes.Add(ConcreteDeclaredValue(method.DeclaringType!));
            }
            argumentTypes.AddRange(method.GetParameters()
                .Select(parameter => ConcreteDeclaredValue(parameter.ParameterType)));
            var analysis = PossibleTypeMethodAnalyzer.Analyze(
                decoder,
                new Dictionary<MethodInfo, FlowValue>(),
                argumentTypes,
                new Dictionary<FieldLocation, FlowValue>(),
                new Dictionary<ArrayLocation, FlowValue>(),
                new Dictionary<MethodBase, HashSet<Type>>(),
                _ => false,
                _ => Array.Empty<Type>(),
                _ => Array.Empty<Type>(),
                _ => Array.Empty<ByRefParameterTypes>(),
                inferTaskFactoryResults: true,
                collectReceivers: false);
            return analysis?.ReturnValue.AwaitableResultTypes.ToArray() ?? Array.Empty<Type>();
        }

        internal static IReadOnlyList<ByRefParameterTypes> FindByRefParameterTypes(
            MethodDecoder rootDecoder)
        {
            var providerAssembly = rootDecoder.MethodBase.Module.Assembly;
            var decoders = new Dictionary<MethodBase, MethodDecoder>
            {
                [rootDecoder.MethodBase] = rootDecoder,
            };
            var pending = new Queue<MethodDecoder>();
            pending.Enqueue(rootDecoder);
            while (pending.Count > 0)
            {
                foreach (var referencedMethod in pending.Dequeue().ReferencedMethods.Keys
                    .Where(method => method.Module.Assembly == providerAssembly
                        && method.GetMethodBody() != null
                        && (method.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) == 0))
                {
                    var normalized = GenericsHelper.GetPossiblyGenericReferenceFromMethod(referencedMethod);
                    if (decoders.ContainsKey(normalized))
                    {
                        continue;
                    }
                    var decoder = new MethodDecoder();
                    decoder.LoadMethod(normalized, validateRuntimeSemantics: false);
                    decoders.Add(normalized, decoder);
                    pending.Enqueue(decoder);
                }
            }

            var returns = decoders.Keys.OfType<MethodInfo>()
                .ToDictionary(method => method, _ => new FlowValue());
            var exceptions = decoders.Keys
                .ToDictionary(method => method, _ => new HashSet<Type>());
            var arguments = decoders.Keys.ToDictionary(
                method => method,
                method => (method.IsStatic
                        ? Enumerable.Empty<FlowValue>()
                        : new[] { ConcreteDeclaredValue(method.DeclaringType!) })
                    .Concat(method.GetParameters()
                        .Select(parameter => ConcreteDeclaredValue(parameter.ParameterType)))
                    .ToArray());
            var fields = new Dictionary<FieldLocation, FlowValue>();
            var arrayElements = new Dictionary<ArrayLocation, FlowValue>();
            var writes = decoders.Keys.ToDictionary(
                method => method,
                _ => new Dictionary<int, FlowValue>());

            while (true)
            {
                var changed = false;
                foreach (var decoder in decoders.Values)
                {
                    var analysis = PossibleTypeMethodAnalyzer.Analyze(
                        decoder,
                        returns,
                        arguments[decoder.MethodBase],
                        fields,
                        arrayElements,
                        exceptions,
                        _ => false,
                        _ => Array.Empty<Type>(),
                        _ => Array.Empty<Type>(),
                        GetSummary,
                        inferTaskFactoryResults: false,
                        collectReceivers: false);
                    if (analysis == null)
                    {
                        continue;
                    }
                    if (decoder.MethodBase is MethodInfo method)
                    {
                        changed |= returns[method].UnionWith(analysis.ReturnValue);
                    }
                    changed |= exceptions[decoder.MethodBase].UnionWithChanged(
                        analysis.EscapingExceptions);
                    foreach (var call in analysis.Calls)
                    {
                        var called = GenericsHelper.GetPossiblyGenericReferenceFromMethod(call.Method);
                        if (!arguments.TryGetValue(called, out var targetArguments))
                        {
                            continue;
                        }
                        for (var index = 0; index < Math.Min(targetArguments.Length, call.Arguments.Length); index++)
                        {
                            changed |= targetArguments[index].UnionWith(call.Arguments[index]);
                        }
                    }
                    changed |= MergeWrites(fields, analysis.FieldWrites);
                    changed |= MergeWrites(arrayElements, analysis.ArrayWrites);
                    changed |= MergeWrites(writes[decoder.MethodBase], analysis.ByRefWrites);
                }
                if (!changed)
                {
                    break;
                }
            }

            return GetSummary(rootDecoder.MethodBase);

            IReadOnlyList<ByRefParameterTypes> GetSummary(MethodBase method)
            {
                var normalized = GenericsHelper.GetPossiblyGenericReferenceFromMethod(method);
                return writes.TryGetValue(normalized, out var methodWrites)
                    ? CreateByRefParameterTypes(normalized, methodWrites)
                    : Array.Empty<ByRefParameterTypes>();
            }

            static bool MergeWrites<TKey>(
                Dictionary<TKey, FlowValue> target,
                IReadOnlyDictionary<TKey, FlowValue> source)
            {
                var changed = false;
                foreach (var write in source)
                {
                    if (!target.TryGetValue(write.Key, out var value))
                    {
                        value = new FlowValue();
                        target.Add(write.Key, value);
                    }
                    changed |= value.UnionWith(write.Value);
                }
                return changed;
            }
        }

        public static HashSet<MethodBase> FindRequiredMethods(ReaderContext context)
        {
            var receivers = PossibleTypeGraphAnalyzer.FindVirtualReceivers(context, out var reachableOffsets, out _, out _);
            var decoders = context.OrderToOutput.Values
                .Select(value => value.MethodDecoder)
                .ToDictionary(decoder => decoder.MethodBase);
            var required = new HashSet<MethodBase>();
            var pending = new Queue<MethodBase>();
            Enqueue(context.EntryPoint);
            foreach (var reflectionRoot in context.ReflectionRoots)
            {
                Enqueue(reflectionRoot);
            }
            foreach (var runtimeOverrideDynamicRoot in context.RuntimeOverrideDynamicRoots)
            {
                Enqueue(runtimeOverrideDynamicRoot);
            }
            while (pending.Count > 0)
            {
                var method = pending.Dequeue();
                if (!decoders.TryGetValue(method, out var decoder))
                {
                    continue;
                }

                var isBoundary = context.RuntimeOverrides.HasRuntimeOverride(method)
                    || PathLocator.HasBcl(method);
                if (isBoundary)
                {
                    foreach (var dependency in decoder.ReferencedMethods.Keys)
                    {
                        Enqueue(dependency);
                    }
                    continue;
                }

                foreach (var instruction in decoder.InstructionBlocks.SelectMany(block => block.Instructions))
                {
                    if (instruction.OpCode == OpCodes.Newobj
                        && instruction.Operand is ConstructorInfo exceptionConstructor
                        && exceptionConstructor.DeclaringType != null
                        && typeof(Exception).IsAssignableFrom(exceptionConstructor.DeclaringType))
                    {
                        // Exception construction can sit behind state that becomes
                        // observable only across a framework or package boundary.
                        // Keep the directly referenced constructor even when local
                        // possible-type reachability cannot prove that block.
                        Enqueue(exceptionConstructor);
                    }
                    if (!reachableOffsets.TryGetValue(decoder.MethodBase, out var methodOffsets)
                        || !methodOffsets.Contains(instruction.ILOffset))
                    {
                        continue;
                    }
                    if (instruction.Operand is FieldInfo field
                        && field.IsStatic
                        && field.DeclaringType?.TypeInitializer is ConstructorInfo typeInitializer)
                    {
                        Enqueue(typeInitializer);
                    }
                    if (instruction.Operand is not MethodBase called)
                    {
                        continue;
                    }
                    if (called is MethodInfo constructedCall && constructedCall.IsConstructedGenericMethod)
                    {
                        var genericArguments = constructedCall.GetGenericArguments();
                        foreach (var genericArgument in genericArguments.Where(type =>
                            typeof(System.Runtime.CompilerServices.IAsyncStateMachine).IsAssignableFrom(type)))
                        {
                            var moveNext = genericArgument.GetMethod(
                                nameof(System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext),
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (moveNext != null)
                            {
                                Enqueue(moveNext);
                            }
                        }
                        if (constructedCall.Name == "AwaitOnCompleted"
                            && genericArguments.Length > 0)
                        {
                            var onCompleted = genericArguments[0].GetMethod(
                                "OnCompleted",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                binder: null,
                                new[] { typeof(Action) },
                                modifiers: null);
                            if (onCompleted != null)
                            {
                                Enqueue(onCompleted);
                            }
                        }
                    }
                    if (instruction.OpCode == OpCodes.Newobj
                        || instruction.OpCode == OpCodes.Call
                        || instruction.OpCode == OpCodes.Ldftn)
                    {
                        Enqueue(called);
                        continue;
                    }
                    if (instruction.OpCode != OpCodes.Callvirt || called is not MethodInfo virtualMethod)
                    {
                        continue;
                    }
                    if (virtualMethod.Name == nameof(Action.Invoke)
                        && typeof(MulticastDelegate).IsAssignableFrom(virtualMethod.DeclaringType))
                    {
                        // Delegate dispatch is represented by the ldftn/ldvirtftn target and
                        // handled specially by the JavaScript runtime. It has no ordinary
                        // concrete virtual target to resolve here.
                        continue;
                    }
                    var normalizedVirtualMethod = (MethodInfo)
                        GenericsHelper.GetPossiblyGenericReferenceFromMethod(virtualMethod);
                    if (context.RuntimeOverrides.HasRuntimeOverride(normalizedVirtualMethod))
                    {
                        // The JavaScript boundary implements this exact virtual contract.
                        // It does not require a managed concrete target to be retained.
                        Enqueue(normalizedVirtualMethod);
                        continue;
                    }
                    if (!virtualMethod.IsVirtual)
                    {
                        var nonVirtualCallSite = new VirtualCallSite(
                            decoder.MethodBase,
                            instruction.ILOffset,
                            virtualMethod);
                        if (receivers.TryGetValue(nonVirtualCallSite, out var nonVirtualReceivers)
                            && nonVirtualReceivers.Count > 0
                            && nonVirtualReceivers.All(type => type == typeof(KnownNull)))
                        {
                            continue;
                        }
                        Enqueue(virtualMethod);
                        continue;
                    }
                    var callSite = new VirtualCallSite(
                        decoder.MethodBase,
                        instruction.ILOffset,
                        virtualMethod);
                    if (receivers.TryGetValue(callSite, out var possibleTypes))
                    {
                        var concreteTypes = possibleTypes
                            .Where(type => type != typeof(KnownNull))
                            .ToArray();
                        if (concreteTypes.Length == 0
                            && !possibleTypes.Contains(typeof(KnownNull)))
                        {
                            throw new InvalidOperationException(
                                $"The compiler could not prove a concrete receiver type for reachable virtual call " +
                                $"'{virtualMethod}' in '{method}'.");
                        }
                        foreach (var receiverType in concreteTypes)
                        {
                            Enqueue(ResolveVirtualTarget(receiverType, virtualMethod));
                        }
                    }
                    else
                    {
                        // The decoder contains every instruction in the method body, including handlers
                        // whose exception types cannot occur. A missing call site is unreachable; a
                        // reachable call site with an empty receiver set is represented above and fails.
                        continue;
                    }
                }
            }
            return required;

            void Enqueue(MethodBase candidate)
            {
                candidate = GenericsHelper.GetPossiblyGenericReferenceFromMethod(candidate);
                if (required.Add(candidate))
                {
                    pending.Enqueue(candidate);
                }
            }
        }

        internal static MethodInfo ResolveVirtualTarget(Type receiverType, MethodInfo method)
        {
            if (receiverType.IsArray && IsGenericEnumerableGetEnumerator(method))
            {
                // SZARRAY implements IEnumerable<T> through runtime-provided methods that
                // reflection intentionally does not expose through GetInterfaceMap.
                return method;
            }
            if (IsGenericArrayEnumerator(receiverType)
                && method.DeclaringType?.IsInterface == true)
            {
                // The CLR's array enumerator is runtime-provided. Retain the interface
                // contract while the JavaScript runtime supplies its implementation;
                // walking the internal CoreLib implementation would retain unrelated
                // runtime machinery that cannot execute in the browser.
                return method;
            }
            if (method.DeclaringType!.IsInterface)
            {
                var implementedInterface = receiverType.GetInterfaces().FirstOrDefault(type =>
                    type == method.DeclaringType
                    || (type.IsGenericType && method.DeclaringType.IsGenericType
                        && type.GetGenericTypeDefinition() == method.DeclaringType.GetGenericTypeDefinition()));
                if (implementedInterface != null)
                {
                    var map = receiverType.GetInterfaceMap(implementedInterface);
                    for (var index = 0; index < map.InterfaceMethods.Length; index++)
                    {
                        if (map.InterfaceMethods[index].Name == method.Name
                            && map.InterfaceMethods[index].GetParameters().Length == method.GetParameters().Length)
                        {
                            return map.TargetMethods[index];
                        }
                    }
                }
            }

            return receiverType.GetMethod(
                       method.Name,
                       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                       binder: null,
                       method.GetParameters().Select(parameter => parameter.ParameterType).ToArray(),
                       modifiers: null)
                   ?? method;
        }

        public static IReadOnlyDictionary<VirtualCallSite, HashSet<Type>> FindVirtualReceivers(
            ReaderContext context)
            => PossibleTypeGraphAnalyzer.FindVirtualReceivers(context, out _, out _, out _);

        public static IReadOnlyDictionary<MethodBase, IReadOnlyList<CallArgumentTypes>> FindCallArgumentTypes(
            ReaderContext context)
        {
            PossibleTypeGraphAnalyzer.FindVirtualReceivers(context, out _, out var argumentTypes, out _);
            return argumentTypes;
        }

        public static void FindCallArgumentAndReturnTypes(
            ReaderContext context,
            out IReadOnlyDictionary<MethodBase, IReadOnlyList<CallArgumentTypes>> callArgumentTypes,
            out IReadOnlyDictionary<MethodInfo, CallArgumentTypes> returnTypes)
        {
            PossibleTypeGraphAnalyzer.FindVirtualReceivers(context, out _, out callArgumentTypes, out returnTypes);
        }

        internal static bool IsKnownNull(Type type) => type == typeof(KnownNull);
    }
}
