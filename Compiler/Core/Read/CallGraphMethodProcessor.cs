using Compiler.DTOs;
using Compiler.Helpers;
using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Compiler.Read
{
    internal static class CallGraphMethodProcessor
    {
        public static void LoadExceptionsThrownFromFrameworkCore(ReaderContext ctx)
        {
            // Load exception fields
            var stackTracStringField = typeof(Exception).GetField("_stackTraceString", BindingFlags.Instance | BindingFlags.NonPublic);
            ctx.AllReferencedFields.Add(stackTracStringField, true);
            var messageField = typeof(Exception).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic);
            ctx.AllReferencedFields.Add(messageField, true);

            // Load object type
            ctx.AllReferencedTypes.Add((TypeInfo)typeof(object), true);

        }

        public static void ProcessQueuedMethods(ReaderContext ctx, ref bool hasMissingIntrinsics, ref bool foundNewMethods)
        {
            while (ctx.Queue.Any())
            {
                var methodEntry = ctx.Queue.Dequeue();
                var method = methodEntry.MethodBase;
                var methodName = method.Name;

                // record keeping for our output order
                if (ctx.OrderToOutput.TryGetValue(method, out var currentOrder) && currentOrder.Order < methodEntry.Order)
                {
                    ctx.OrderToOutput[method].Order = methodEntry.Order;
                }

                // if we've already processed this, skip it.
                if (ctx.ExploredMethods.TryGetValue(method, out var hasBeenProcessed) && hasBeenProcessed)
                {
                    continue;
                }
                foundNewMethods = true;

                var isAlreadyCompiled = PathLocator.HasBcl(method);
                var hasPatch = ctx.RuntimeOverrides.HasRuntimeOverride(method);
                var isIntrinsic = method.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.IntrinsicAttribute");
                CompilerLog.WriteLine(
                    CompilerLogLevel.Verbose,
                    $"Working on {GetFullyQualifiedName(method)}{((isIntrinsic || hasPatch) ? (hasPatch ? " (PATCHED)" : " (INTRINSIC)") : "")} (called by {GetFullyQualifiedName(methodEntry?.Caller?.MethodBase) ?? "<NULL>"})...");

                try
                {
                    ThrowIfExplicitlyUnsupported(method, hasPatch);

                    // load the method and process its msil
                    var methodDecoder = new MethodDecoder();
                    ctx.ExploredMethods[method] = true;
                    methodDecoder.LoadMethod(method, validateRuntimeSemantics: !hasPatch);

                    // if we've previously compiled this, we can stop
                    if (isAlreadyCompiled || hasPatch)
                    {
                        if (hasPatch)
                        {
                            var runtimeOverrideDependencies = ctx.RuntimeOverrides.GetDependencies(method);
                            methodDecoder.ReferencedMethods.Clear();
                            foreach (var newMethod in runtimeOverrideDependencies)
                            {
                                // Calling an imported method does not require its declaring type
                                // metadata. Constructing an imported type does: newobj initializes
                                // the instance from globalThis.DotNet.Types before invoking .ctor.
                                // Keep this deliberately limited to constructors so an override's
                                // method import cannot pull an otherwise unnecessary framework type
                                // into interface and virtual-method discovery.
                                if (newMethod is ConstructorInfo)
                                {
                                    ctx.AllReferencedTypes[(TypeInfo)newMethod.DeclaringType] = true;
                                }
                                ctx.Queue.Enqueue(new MethodBaseEntry
                                {
                                    MethodBase = newMethod,
                                    Caller = methodEntry,
                                    Order = methodEntry.Order + 1
                                });
                                methodDecoder.ReferencedMethods[newMethod] = true;
                                if (newMethod is MethodInfo dispatchMethod
                                    && dispatchMethod.IsVirtual)
                                {
                                    ctx.RuntimeOverrideDispatchDependencies.Add(
                                        new RuntimeOverrideDispatchDependency
                                        {
                                            RuntimeOverrideMethod = method,
                                            DispatchMethod = dispatchMethod,
                                            Caller = methodEntry,
                                        });
                                }
                            }
                            if (method is MethodInfo runtimeOverrideMethod)
                            {
                                foreach (var returnType in RuntimeOverrideTypeResolver.FindRuntimeOverrideReturnImplementations(
                                    ctx,
                                    runtimeOverrideMethod,
                                    runtimeOverrideMethod.ReturnType))
                                {
                                    ctx.AllReferencedTypes[returnType.GetTypeInfo()] = true;
                                }
                                foreach (var resultType in ctx.RuntimeOverrides
                                    .GetAwaitableResultTypePatterns(runtimeOverrideMethod))
                                {
                                    ctx.AllReferencedTypes[resultType.GetTypeInfo()] = true;
                                }
                            }
                            RuntimeOverrideManagedTypeCoordinator.RegisterRuntimeOverrideManagedTypeAccess(
                                ctx,
                                method,
                                methodDecoder,
                                methodEntry);
                            RuntimeOverrideManagedTypeCoordinator.RegisterRuntimeOverrideManagedParameterDependencies(
                                ctx,
                                method,
                                methodEntry);
                        }

                        if (!ctx.OrderToOutput.ContainsKey(method))
                        {
                            ctx.OrderToOutput.Add(method, new MethodDecoderOrder
                            {
                                MethodDecoder = methodDecoder,
                                Caller = methodEntry.Caller,
                                Order = int.MaxValue
                            });
                        }
                        continue;
                    }

                    foreach (var memberInfo in methodDecoder.ReferencedMembers.Keys)
                    {
                        if (memberInfo is FieldInfo fi)
                        {
                            ctx.AllReferencedFields[fi] = true;
                        }
                    }
                    foreach (var typeInfo in methodDecoder.ReferencedTypes.Keys)
                    {
                        ctx.AllReferencedTypes[typeInfo] = true;
                    }

                    if (methodDecoder.IsMandatoryIntrinsic)
                    {
                        if (!methodDecoder.MethodBase.DeclaringType.IsInterface)
                        {
                            Console.Error.WriteLine($"ERROR: Missing mandatory intrinsic: {NameNormalizers.GetFullyQualifiedMethodName(method)}");
                            hasMissingIntrinsics = true;
                        }
                        continue;
                    }

                    // this makes outputting resumable without having the cache find methods we randomly implemented
                    if (!ctx.OrderToOutput.ContainsKey(method))
                    {
                        ctx.OrderToOutput.Add(method, new MethodDecoderOrder
                        {
                            MethodDecoder = methodDecoder,
                            Caller = methodEntry.Caller,
                            Order = methodEntry.Order
                        });
                    }

                    var generatedStateMachineType = method.CustomAttributes
                        .FirstOrDefault(attribute => typeof(System.Runtime.CompilerServices.StateMachineAttribute)
                            .IsAssignableFrom(attribute.AttributeType))?
                        .ConstructorArguments.FirstOrDefault().Value as Type;
                    MethodBase stateMachineMoveNext = generatedStateMachineType?.GetMethod(
                        nameof(System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext),
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (stateMachineMoveNext != null)
                    {
                        stateMachineMoveNext = GenericsHelper.GetPossiblyGenericReferenceFromMethod(stateMachineMoveNext);
                        methodDecoder.ReferencedMethods[stateMachineMoveNext] = true;
                        ctx.Queue.Enqueue(new MethodBaseEntry
                        {
                            MethodBase = stateMachineMoveNext,
                            Caller = methodEntry,
                            Order = methodEntry.Order + 1
                        });
                    }

                    // after processing we have a list of references methods we can add to the queue
                    foreach (var newMethod in methodDecoder.ReferencedMethods.Keys.ToList())
                    {
                        if (newMethod is MethodInfo methodInfo)
                        {
                            var typeInfo = (TypeInfo)methodInfo.ReturnType;
                            if (typeInfo.FullName != "System.Void")
                            {
                                ctx.AllReferencedTypes[typeInfo] = true;
                            }
                        }

                        ctx.Queue.Enqueue(new MethodBaseEntry
                        {
                            MethodBase = newMethod,
                            Caller = methodEntry,
                            Order = methodEntry.Order + 1
                        });

                        if (newMethod is not MethodInfo constructedMethod
                            || !constructedMethod.IsConstructedGenericMethod)
                        {
                            continue;
                        }
                        var genericArguments = constructedMethod.GetGenericArguments();
                        foreach (var stateMachineType in genericArguments.Where(type =>
                            typeof(System.Runtime.CompilerServices.IAsyncStateMachine).IsAssignableFrom(type)))
                        {
                            var moveNext = stateMachineType.GetMethod(
                                nameof(System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext),
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (moveNext != null)
                            {
                                moveNext = (MethodInfo)GenericsHelper.GetPossiblyGenericReferenceFromMethod(moveNext);
                                methodDecoder.ReferencedMethods[moveNext] = true;
                                ctx.Queue.Enqueue(new MethodBaseEntry
                                {
                                    MethodBase = moveNext,
                                    Caller = methodEntry,
                                    Order = methodEntry.Order + 1
                                });
                            }
                        }
                        if (constructedMethod.Name == "AwaitOnCompleted" && genericArguments.Length > 0)
                        {
                            var onCompleted = genericArguments[0].GetMethod(
                                "OnCompleted",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                binder: null,
                                new[] { typeof(Action) },
                                modifiers: null);
                            if (onCompleted != null)
                            {
                                onCompleted = (MethodInfo)GenericsHelper.GetPossiblyGenericReferenceFromMethod(onCompleted);
                                methodDecoder.ReferencedMethods[onCompleted] = true;
                                ctx.Queue.Enqueue(new MethodBaseEntry
                                {
                                    MethodBase = onCompleted,
                                    Caller = methodEntry,
                                    Order = methodEntry.Order + 1
                                });
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    while (methodEntry.Caller != null)
                    {
                        Console.WriteLine($"Called by: {methodEntry.MethodBase.DeclaringType} -> {methodEntry.MethodBase}");
                        methodEntry = methodEntry.Caller;
                    }
                    throw;
                }
            }
        }

        static string GetFullyQualifiedName(MethodBase methodBase) => methodBase == null ? null : $"'{Path.GetFileName(methodBase.Module.Assembly.Location)}'!{methodBase.DeclaringType} -> {methodBase}";

        private static void ThrowIfExplicitlyUnsupported(MethodBase method, bool hasPatch)
        {
            var declaringType = method.DeclaringType;
            var declaringNamespace = declaringType?.Namespace ?? string.Empty;

            if (!hasPatch
                && declaringNamespace == "System.Linq.Expressions"
                && method.Name == "Compile")
            {
                throw new NotSupportedException(
                    $"Expression.Compile() is not supported; expression trees may be constructed and inspected only ('{method}').");
            }

            if ((declaringType == typeof(Assembly) && method.Name.StartsWith("Load", StringComparison.Ordinal))
                || (declaringNamespace == "System.Runtime.Loader"
                    && method.Name.StartsWith("Load", StringComparison.Ordinal)))
            {
                throw new NotSupportedException(
                    $"Dynamic assembly loading is not supported ('{method}').");
            }

            if (declaringNamespace.StartsWith("System.Reflection.Emit", StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"Reflection.Emit is not supported ('{method}').");
            }

            if ((declaringType == typeof(RuntimeMethodHandle)
                    || declaringType == typeof(RuntimeFieldHandle))
                && method.Name == "GetFunctionPointer")
            {
                throw new NotSupportedException(
                    $"Unmanaged runtime handles are not supported ('{method}').");
            }

            var isFrameworkWideReflection =
                declaringType == typeof(Assembly)
                    && (method.Name == "GetTypes"
                        || method.Name == "GetExportedTypes"
                        || method.Name == "get_DefinedTypes"
                        || method.Name == "get_ExportedTypes")
                || declaringType == typeof(Module)
                    && method.Name == "GetTypes"
                || declaringType == typeof(AppDomain)
                    && method.Name == "GetAssemblies";
            if (isFrameworkWideReflection)
            {
                throw new NotSupportedException(
                    $"Framework-wide reflection is not supported ('{method}').");
            }
        }

    }
}
