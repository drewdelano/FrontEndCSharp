using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.PossibleType;
using Compiler.Performance;

namespace Compiler.Read
{
    public static class Reader
    {
        public static CallGraphInfo GetCallGraph(string binInput)
        {
            CompilerLog.WriteLine(CompilerLogLevel.Normal, "Reading call graph...");

            var fullInputPath = Path.GetFullPath(binInput);
            var loadContext = new TargetAssemblyLoadContext(fullInputPath);
            var asmRoot = loadContext.LoadFromAssemblyPath(fullInputPath);

            var ctx = new ReaderContext
            {
                RuntimeOverrides = RuntimeOverrideAssemblyDiscovery.Discover(binInput, loadContext),
                EntryPoint = asmRoot.EntryPoint
            };
            ReadCallGraph.LoadExceptionsThrownFromFrameworkCore(ctx);

            // walk the call chain
            ctx.Queue.Enqueue(new MethodBaseEntry
            {
                MethodBase = asmRoot.EntryPoint,
                Caller = null,
                Order = 0,
            });
            var hasMissingIntrinsics = false;
            var loggedPossibleTypeAnalysis = false;

            bool foundNewMethods;
            do
            {
                foundNewMethods = false;
                using (CompilationPerformance.Measure("call-graph.decode-methods"))
                {
                    ReadCallGraph.ProcessQueuedMethods(
                        ctx,
                        ref hasMissingIntrinsics,
                        ref foundNewMethods);
                }

                if (foundNewMethods)
                {
                    if (!loggedPossibleTypeAnalysis)
                    {
                        CompilerLog.WriteLine(
                            CompilerLogLevel.Normal,
                            "Analyzing possible runtime types...");
                        loggedPossibleTypeAnalysis = true;
                    }

                    using (CompilationPerformance.Measure("call-graph.managed-type-demand"))
                    {
                        ReadCallGraph.FindRuntimeOverrideManagedParameterMembers(
                            ctx,
                            validateResolvedTypes: false);
                    }

                    var runtimeOverrideDispatch = CompilationPerformance.Measure(
                        "call-graph.runtime-override-dispatch",
                        () => PossibleTypeCallGraph.FindRuntimeOverrideDispatchImplementations(ctx));
                    foreach (var newMethod in runtimeOverrideDispatch)
                    {
                        ctx.Queue.Enqueue(newMethod);
                    }

                    var dispatch = CompilationPerformance.Measure(
                        "call-graph.virtual-dispatch",
                        () => PossibleTypeCallGraph.FindDispatchImplementations(ctx));
                    foreach (var newMethod in dispatch)
                    {
                        ctx.Queue.Enqueue(newMethod);
                    }

                    var reflectionRoots = CompilationPerformance.Measure(
                        "call-graph.reflection-demand",
                        () => DiscoverReflectionRoots(ctx).ToArray());
                    foreach (var reflectionRoot in reflectionRoots)
                    {
                        ctx.Queue.Enqueue(new MethodBaseEntry
                        {
                            MethodBase = reflectionRoot,
                            Caller = null,
                            Order = 1
                        });
                    }
                }
            }
            while (foundNewMethods);

            using (CompilationPerformance.Measure("call-graph.validate-managed-type-demand"))
            {
                ReadCallGraph.FindRuntimeOverrideManagedParameterMembers(
                    ctx,
                    validateResolvedTypes: true);
            }

            if (hasMissingIntrinsics)
            {
                throw new Exception("Missing intrinsics in compilation process");
            }

            HashSet<MethodBase> requiredMethods;
            using (CompilationPerformance.Measure("call-graph.required-methods"))
            {
                requiredMethods = PossibleTypeCallGraph.FindRequiredMethods(ctx);
            }
            requiredMethods.UnionWith(ctx.ReflectionRoots);

            // sort methods by deepest referenced
            // this makes sure we don't generate method missing dependencies
            var orderedCallInfo = ctx.OrderToOutput
                .Where(kvp => requiredMethods.Contains(kvp.Key))
                .Select(kvp => kvp.Value)
                .OrderByDescending(kvp => kvp.Order)
                .Select(kvp => kvp.MethodDecoder)
                .ToList();
            var reflectedTypes = ctx.ReflectedTypes;
            var fieldReflectedTypes = ctx.FieldReflectedTypes;
            var enumMetadataTypes = ctx.EnumMetadataTypes;

            // group fields by declaring type
            var referencedFields = ctx.AllReferencedFields
                .GroupBy(f => (TypeInfo)f.Key.DeclaringType)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Select(v => v.Key).ToList());
            foreach (var reflectedType in reflectedTypes)
            {
                AddMetadataType(typeof(MemberInfo));
                AddMetadataType(typeof(PropertyInfo));
                AddMetadataType(typeof(MethodInfo));
                AddMetadataType(reflectedType);
                foreach (var property in reflectedType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    AddMetadataType(property.PropertyType);
                    foreach (var attribute in property.CustomAttributes)
                    {
                        AddMetadataType(attribute.AttributeType);
                    }
                }
                foreach (var method in reflectedType.GetMethods(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    AddMetadataType(method.ReturnType);
                    foreach (var parameter in method.GetParameters())
                    {
                        AddMetadataType(parameter.ParameterType);
                    }
                    foreach (var attribute in method.CustomAttributes)
                    {
                        AddMetadataType(attribute.AttributeType);
                    }
                }
                foreach (var attribute in reflectedType.CustomAttributes)
                {
                    AddMetadataType(attribute.AttributeType);
                }
            }
            if (fieldReflectedTypes.Count > 0)
            {
                AddMetadataType(typeof(FieldInfo));
            }
            foreach (var reflectedType in fieldReflectedTypes)
            {
                foreach (var field in reflectedType.GetFields())
                {
                    AddMetadataType(field.FieldType);
                    foreach (var attribute in field.CustomAttributes)
                    {
                        AddMetadataType(attribute.AttributeType);
                    }
                    var declaringType = GenericsHelper.GetPossiblyGenericReferenceFromType(
                        (TypeInfo)field.DeclaringType!);
                    if (!referencedFields.TryGetValue(declaringType, out var declaringFields))
                    {
                        declaringFields = new List<FieldInfo>();
                        referencedFields.Add(declaringType, declaringFields);
                    }
                    if (!declaringFields.Any(candidate =>
                        candidate.Module.ModuleVersionId == field.Module.ModuleVersionId
                        && candidate.MetadataToken == field.MetadataToken))
                    {
                        declaringFields.Add(field);
                    }
                }
            }
            foreach (var enumType in enumMetadataTypes)
            {
                AddMetadataType(enumType);
                AddMetadataType(Enum.GetUnderlyingType(enumType));
            }

            // patch any missing types
            foreach (var kvp in ctx.AllReferencedTypes)
            {
                if (!referencedFields.ContainsKey(kvp.Key))
                {
                    referencedFields.Add(kvp.Key, new List<FieldInfo>());
                }
            }

            // Runtime-created exceptions need type metadata for managed catch/type checks,
            // but must not participate in call-graph virtual dispatch discovery. Adding
            // them after discovery prevents unused framework overrides from being compiled.
            foreach (var exceptionType in RuntimeExceptionTypes.RequiredMetadata)
            {
                if (!referencedFields.ContainsKey(exceptionType))
                {
                    referencedFields.Add(exceptionType, new List<FieldInfo>());
                }
            }

            return new CallGraphInfo
            {
                RuntimeOverrides = ctx.RuntimeOverrides,
                MethodDecoders = orderedCallInfo,
                AllReferencedFields = referencedFields,
                EntryPoint = asmRoot.EntryPoint,
                ReflectedTypes = reflectedTypes,
                FieldReflectedTypes = fieldReflectedTypes,
                EnumMetadataTypes = enumMetadataTypes
            };

            void AddMetadataType(Type type)
            {
                while (type.HasElementType)
                {
                    type = type.GetElementType()!;
                }
                if (type.IsGenericParameter)
                {
                    return;
                }
                var typeInfo = GenericsHelper.GetPossiblyGenericReferenceFromType((TypeInfo)type);
                if (!referencedFields.ContainsKey(typeInfo))
                {
                    referencedFields.Add(typeInfo, new List<FieldInfo>());
                }
                if (typeInfo.BaseType != null)
                {
                    AddMetadataType(typeInfo.BaseType);
                }
                foreach (var interfaceType in typeInfo.GetInterfaces())
                {
                    AddMetadataType(interfaceType);
                }
            }
        }

        private static IEnumerable<MethodBase> DiscoverReflectionRoots(ReaderContext context)
        {
            var instructions = context.OrderToOutput.Values
                .SelectMany(value => value.MethodDecoder.InstructionBlocks)
                .SelectMany(block => block.Instructions)
                .ToArray();
            var calls = instructions
                .Select(instruction => instruction.Operand)
                .OfType<MethodInfo>()
                .ToArray();
            var usesMetadata = calls.Any(method =>
                method.DeclaringType == typeof(Type)
                    && (method.Name == "GetProperties"
                        || method.Name == "GetMethods"
                        || method.Name == "GetFields"
                        || method.Name == "GetCustomAttributes"
                        || method.Name == "get_IsEnum"
                        || method.Name == "GetEnumUnderlyingType"
                        || method.Name == "GetEnumNames"
                        || method.Name == "GetEnumValues"
                        || method.Name == "GetEnumValuesAsUnderlyingType"
                        || method.Name == "GetEnumName"
                        || method.Name == "IsEnumDefined"))
                || calls.Any(method => string.Equals(
                    method.DeclaringType?.Namespace,
                    "System.Linq.Expressions",
                    StringComparison.Ordinal));
            if (!usesMetadata)
            {
                return Array.Empty<MethodBase>();
            }

            var reflectionDemand = RepresentedTypeAnalysis.FindDemand(context);
            context.ReflectedTypes.UnionWith(reflectionDemand.Types);
            context.FieldReflectedTypes.UnionWith(reflectionDemand.FieldTypes);
            context.EnumMetadataTypes.UnionWith(
                reflectionDemand.EnumTypes.Where(type => type.IsEnum));

            var roots = new HashSet<MethodBase>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var type in context.ReflectedTypes)
            {
                foreach (var property in type.GetProperties(flags))
                {
                    if (reflectionDemand.ReadProperties.Contains(property)
                        && property.GetMethod != null)
                    {
                        roots.Add(property.GetMethod);
                    }
                    if (reflectionDemand.WrittenProperties.Contains(property)
                        && property.SetMethod != null)
                    {
                        roots.Add(property.SetMethod);
                    }
                }
            }
            roots.UnionWith(reflectionDemand.InvokedMethods);
            context.ReflectionRoots.UnionWith(roots);
            return roots.Where(method => !context.ExploredMethods.ContainsKey(method));
        }

        private sealed class TargetAssemblyLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;
            private readonly string _assemblyDirectory;

            public TargetAssemblyLoadContext(string assemblyPath)
                : base(isCollectible: false)
            {
                _resolver = new AssemblyDependencyResolver(assemblyPath);
                _assemblyDirectory = Path.GetDirectoryName(assemblyPath)
                    ?? throw new InvalidOperationException(
                        $"Target assembly '{assemblyPath}' has no containing directory.");
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath == null)
                {
                    var localPath = Path.Combine(_assemblyDirectory, assemblyName.Name + ".dll");
                    if (File.Exists(localPath))
                    {
                        assemblyPath = localPath;
                    }
                }

                return assemblyPath == null
                    ? null
                    : LoadFromAssemblyPath(assemblyPath);
            }
        }
    }
}
