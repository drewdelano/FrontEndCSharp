using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.Read;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Compiler.PossibleType
{
    internal static class PossibleTypeCallGraph
    {
        internal static HashSet<MethodBase> FindRequiredMethods(ReaderContext context)
            => PossibleTypeAnalysis.FindRequiredMethods(context);

        internal static List<MethodBaseEntry> FindRuntimeOverrideDispatchImplementations(ReaderContext ctx)
        {
            var queue = new List<MethodBaseEntry>();
            if (ctx.RuntimeOverrideDispatchDependencies.Count == 0)
            {
                return queue;
            }

            PossibleTypeAnalysis.FindCallArgumentAndReturnTypes(
                ctx,
                out var callArgumentTypes,
                out var returnTypes);
            foreach (var group in ctx.RuntimeOverrideDispatchDependencies
                .GroupBy(dependency => dependency.RuntimeOverrideMethod))
            {
                var dependencies = group.ToArray();
                var runtimeOverrideMethod = GenericsHelper.GetPossiblyGenericReferenceFromMethod(group.Key);
                if (!callArgumentTypes.TryGetValue(runtimeOverrideMethod, out var arguments))
                {
                    continue;
                }

                var upstreamCandidates = new HashSet<Type>(arguments
                    .SelectMany(argument => argument.Types)
                    .Where(type => !PossibleTypeAnalysis.IsKnownNull(type)));
                if (group.Key.IsGenericMethod)
                {
                    foreach (var genericArgument in group.Key.GetGenericArguments())
                    {
                        if (IsConcreteDispatchType(genericArgument))
                        {
                            upstreamCandidates.Add(genericArgument);
                        }
                        upstreamCandidates.UnionWith(
                            RuntimeOverrideTypeResolver.FindRuntimeOverrideDependencyImplementations(
                                ctx,
                                group.Key,
                                genericArgument));
                    }
                }
                if (ctx.RuntimeOverrideManagedTypes.TryGetValue(
                    runtimeOverrideMethod,
                    out var managedTypes))
                {
                    upstreamCandidates.UnionWith(managedTypes);
                    foreach (var managedType in managedTypes.ToArray())
                    {
                        upstreamCandidates.UnionWith(
                            RuntimeOverrideTypeResolver.FindRuntimeOverrideDependencyImplementations(
                                ctx,
                                group.Key,
                                managedType));
                    }
                }
                // A declared dispatch may consume a value passed directly to the
                // override, or a value returned by another declared dispatch. Seed
                // every dependency from upstream and add compatible returns below.
                // Treating a compatible return as exclusive is unsound for broad
                // receivers such as System.Object: Int32 and Boolean results would
                // otherwise hide a key's GetHashCode/Equals receiver.
                var candidates = dependencies.ToDictionary(
                    dependency => dependency,
                    _ => new HashSet<Type>(upstreamCandidates));
                bool changed;
                do
                {
                    changed = false;
                    foreach (var dependency in dependencies)
                    {
                        foreach (var predecessor in dependencies.Where(predecessor =>
                            predecessor != dependency
                            && CanFlowToReceiver(
                                predecessor.DispatchMethod.ReturnType,
                                dependency.DispatchMethod.DeclaringType!)))
                        {
                            foreach (var targetMethod in predecessor.ResolvedTargets.OfType<MethodInfo>())
                            {
                                if (returnTypes.TryGetValue(targetMethod, out var targetReturnTypes))
                                {
                                    var count = candidates[dependency].Count;
                                    candidates[dependency].UnionWith(targetReturnTypes.Types);
                                    changed |= candidates[dependency].Count != count;
                                }
                                if (IsConcreteDispatchType(targetMethod.ReturnType))
                                {
                                    changed |= candidates[dependency].Add(targetMethod.ReturnType);
                                }
                            }
                        }

                        foreach (var receiverType in candidates[dependency].ToArray())
                        {
                            if (!CanDispatch(receiverType, dependency.DispatchMethod))
                            {
                                continue;
                            }

                            var target = GenericsHelper.GetPossiblyGenericReferenceFromMethod(
                                PossibleTypeAnalysis.ResolveVirtualTarget(
                                    receiverType,
                                    dependency.DispatchMethod));
                            if (target == GenericsHelper.GetPossiblyGenericReferenceFromMethod(
                                    dependency.DispatchMethod))
                            {
                                continue;
                            }

                            if (dependency.ResolvedTargets.Add(target))
                            {
                                changed = true;
                                ctx.RuntimeOverrideDynamicRoots.Add(target);
                                ctx.OrderToOutput[runtimeOverrideMethod]
                                    .MethodDecoder.ReferencedMethods[target] = true;
                                if (!ctx.ExploredMethods.ContainsKey(target))
                                {
                                    queue.Add(new MethodBaseEntry
                                    {
                                        Caller = dependency.Caller,
                                        MethodBase = target,
                                        Order = ctx.OrderToOutput[runtimeOverrideMethod].Order + 1,
                                    });
                                }
                            }

                        }
                    }
                }
                while (changed);
            }
            return queue;
        }

        private static bool CanFlowToReceiver(Type returnType, Type receiverType)
        {
            if (returnType == typeof(void) || returnType.IsGenericParameter)
            {
                return false;
            }
            if (receiverType.IsAssignableFrom(returnType))
            {
                return true;
            }
            if (receiverType.IsGenericType && returnType.IsGenericType
                && receiverType.GetGenericTypeDefinition() == returnType.GetGenericTypeDefinition())
            {
                return true;
            }
            return returnType.GetInterfaces().Any(type =>
                type == receiverType
                || type.IsGenericType && receiverType.IsGenericType
                    && type.GetGenericTypeDefinition() == receiverType.GetGenericTypeDefinition());
        }

        private static bool CanDispatch(Type receiverType, MethodInfo method)
        {
            if (!IsConcreteDispatchType(receiverType) || receiverType.IsArray)
            {
                return false;
            }
            var declaringType = method.DeclaringType!;
            if (declaringType.IsAssignableFrom(receiverType))
            {
                return true;
            }
            if (!declaringType.IsGenericType)
            {
                return receiverType.GetInterfaces().Contains(declaringType);
            }
            var definition = declaringType.GetGenericTypeDefinition();
            return receiverType.GetInterfaces().Any(type =>
                type.IsGenericType && type.GetGenericTypeDefinition() == definition);
        }

        private static bool IsConcreteDispatchType(Type type) =>
            !type.IsInterface
            && !type.IsAbstract
            && !type.IsGenericParameter
            && !type.IsPointer
            && !PossibleTypeAnalysis.IsKnownNull(type);

        internal static List<MethodBaseEntry> FindDispatchImplementations(ReaderContext ctx)
        {
            var queue = new List<MethodBaseEntry>();
            var receiverTypes = PossibleTypeAnalysis.FindVirtualReceivers(ctx);
            var scheduled = new HashSet<MethodBase>();
            foreach (var (callSite, receivers) in receiverTypes)
            {
                var method = callSite.Method;
                var anchor = GenericsHelper.GetPossiblyGenericReferenceFromMethod(method);
                if (!ctx.OrderToOutput.TryGetValue(anchor, out var outputOrder))
                {
                    continue;
                }
                foreach (var receiver in receivers)
                {
                    if (PossibleTypeAnalysis.IsKnownNull(receiver))
                    {
                        continue;
                    }
                    var target = GenericsHelper.GetPossiblyGenericReferenceFromMethod(
                        PossibleTypeAnalysis.ResolveVirtualTarget(receiver, method));
                    if (target == anchor
                        || ctx.ExploredMethods.ContainsKey(target)
                        || !scheduled.Add(target))
                    {
                        continue;
                    }
                    outputOrder.MethodDecoder.ReferencedMethods[target] = true;
                    queue.Add(new MethodBaseEntry
                    {
                        Caller = outputOrder.Caller,
                        MethodBase = target,
                        Order = outputOrder.Order + 1
                    });
                }
            }
            return queue;
        }
    }
}
