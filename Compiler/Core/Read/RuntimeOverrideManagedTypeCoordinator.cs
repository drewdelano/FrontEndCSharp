using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.PossibleType;
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
    internal static class RuntimeOverrideManagedTypeCoordinator
    {
        internal static void RegisterRuntimeOverrideManagedTypeAccess(
            ReaderContext ctx,
            MethodBase method,
            MethodDecoder methodDecoder,
            MethodBaseEntry methodEntry)
        {
            var accesses = ctx.RuntimeOverrides.GetManagedGenericTypeAccesses(method);
            if (accesses.Count == 0)
            {
                return;
            }
            if (method is not MethodInfo methodInfo || !methodInfo.IsConstructedGenericMethod)
            {
                throw new InvalidDataException(
                    $"Runtime override managed type access requires a constructed generic method: "
                    + $"{NameNormalizers.GetFullyQualifiedMethodName(method)}.");
            }

            var arguments = methodInfo.GetGenericArguments();
            foreach (var declaration in accesses)
            {
                var type = arguments[declaration.GenericParameterIndex];
                if (type.IsGenericParameter)
                {
                    throw new InvalidDataException(
                        $"Runtime override managed type access could not resolve generic parameter "
                        + $"{declaration.GenericParameterIndex} for "
                        + $"{NameNormalizers.GetFullyQualifiedMethodName(method)}.");
                }

                RegisterManagedTypeAccess(
                    ctx,
                    methodDecoder,
                    methodEntry,
                    type,
                    declaration.Access,
                    declaration.NonPublicMemberAttributeTypeName,
                    ctx.RuntimeOverrides.ResolveManagedTypeAccessExcludedTypes(
                        method,
                        declaration.ExcludedTypes),
                    isDirectProviderRequest: true);
            }
        }

        internal static void RegisterRuntimeOverrideManagedParameterDependencies(
            ReaderContext ctx,
            MethodBase method,
            MethodBaseEntry methodEntry)
        {
            foreach (var declaration in ctx.RuntimeOverrides.GetManagedParameterTypeAccesses(method))
            {
                var minimumParameterIndex = method.IsStatic ? 0 : -1;
                if (declaration.ParameterIndex < minimumParameterIndex
                    || declaration.ParameterIndex >= method.GetParameters().Length)
                {
                    throw new InvalidDataException(
                        $"Runtime override managed type access parameter {declaration.ParameterIndex} is invalid for "
                        + $"{NameNormalizers.GetFullyQualifiedMethodName(method)}.");
                }
                ctx.RuntimeOverrideManagedParameterDependencies.Add(
                    new RuntimeOverrideManagedParameterDependency
                    {
                        Method = method,
                        ParameterIndex = declaration.ParameterIndex,
                        Access = declaration.Access,
                        NonPublicMemberAttributeTypeName =
                            declaration.NonPublicMemberAttributeTypeName,
                        ExcludedTypes = ctx.RuntimeOverrides.ResolveManagedTypeAccessExcludedTypes(
                            method,
                            declaration.ExcludedTypes),
                        Caller = methodEntry,
                    });
            }
        }

        public static void FindRuntimeOverrideManagedParameterMembers(
            ReaderContext ctx,
            bool validateResolvedTypes)
        {
            if (ctx.RuntimeOverrideManagedParameterDependencies.Count == 0)
            {
                return;
            }

            var callArgumentTypes = PossibleTypeAnalysis.FindCallArgumentTypes(ctx);
            foreach (var dependency in ctx.RuntimeOverrideManagedParameterDependencies)
            {
                var method = GenericsHelper.GetPossiblyGenericReferenceFromMethod(dependency.Method);
                if (!callArgumentTypes.TryGetValue(method, out var arguments))
                {
                    if (validateResolvedTypes)
                    {
                        throw new InvalidDataException(
                            $"The compiler could not analyze arguments for runtime override "
                            + $"{NameNormalizers.GetFullyQualifiedMethodName(dependency.Method)}.");
                    }
                    continue;
                }

                var argumentIndex = dependency.ParameterIndex + (dependency.Method.IsStatic ? 0 : 1);
                var argument = arguments[argumentIndex];
                var unresolved = argument.Types.Count == 0
                    || argument.UnknownAllocationTypes.Any(type =>
                        !PossibleTypeAnalysis.IsKnownNull(type)
                        && !type.IsValueType
                        && (!type.IsSealed || type == typeof(object)));
                var enumMetadataOnly = dependency.Access == ManagedTypeAccess.EnumMetadata;
                if (unresolved && !enumMetadataOnly)
                {
                    if (validateResolvedTypes)
                    {
                        var inferredTypes = argument.Types.Count == 0
                            ? "<none>"
                            : string.Join(", ", argument.Types.Select(type => type.ToString()));
                        var unknownTypes = argument.UnknownAllocationTypes.Count == 0
                            ? "<none>"
                            : string.Join(", ", argument.UnknownAllocationTypes.Select(type => type.ToString()));
                        throw new InvalidDataException(
                            $"The compiler could not prove concrete managed types for parameter "
                            + $"{dependency.ParameterIndex} of runtime override "
                            + $"{NameNormalizers.GetFullyQualifiedMethodName(dependency.Method)}. "
                            + $"Inferred types: {inferredTypes}; unknown allocation types: "
                            + $"{unknownTypes}.");
                    }
                    continue;
                }

                foreach (var type in argument.Types.Where(type =>
                    !PossibleTypeAnalysis.IsKnownNull(type)
                    && dependency.RegisteredTypes.Add(type)))
                {
                    RegisterManagedTypeAccess(
                        ctx,
                        ctx.OrderToOutput[dependency.Method].MethodDecoder,
                        dependency.Caller,
                        type,
                        dependency.Access,
                        dependency.NonPublicMemberAttributeTypeName,
                        dependency.ExcludedTypes,
                        isDirectProviderRequest: true);
                }
            }
        }

        private static void RegisterManagedTypeAccess(
            ReaderContext ctx,
            MethodDecoder methodDecoder,
            MethodBaseEntry methodEntry,
            Type type,
            ManagedTypeAccess access,
            string nonPublicMemberAttributeTypeName,
            IReadOnlySet<Type> excludedTypes,
            bool isDirectProviderRequest)
        {
            RegisterRuntimeOverrideManagedType(ctx, methodDecoder.MethodBase, type);
            if (excludedTypes.Contains(type))
            {
                return;
            }
            var isDirectValueTypeContract = isDirectProviderRequest
                && IsApplicationValueTypeContract(ctx, type);
            var excludedTypesKey = string.Join(
                "|",
                excludedTypes
                    .Select(excludedType => excludedType.AssemblyQualifiedName)
                    .OrderBy(name => name, StringComparer.Ordinal));
            if (!ctx.ManagedTypeAccessRegistrations.Add(new ManagedTypeAccessRegistration(
                type,
                access,
                nonPublicMemberAttributeTypeName ?? string.Empty,
                isDirectValueTypeContract,
                excludedTypesKey)))
            {
                return;
            }
            if (access.HasFlag(ManagedTypeAccess.EnumMetadata))
            {
                RegisterEnumMetadata(ctx, type);
                if (access == ManagedTypeAccess.EnumMetadata || type.IsEnum)
                {
                    // Enum metadata is a declarative contract. Do not expand an enum
                    // root into the ordinary reflected hierarchy unless another access
                    // path independently requests that reflection surface.
                    return;
                }
            }
            if (type.IsValueType && !isDirectValueTypeContract)
            {
                // A provider may handle terminal values directly, but that does not
                // make primitives, nullable values, tuples, framework structs, or a
                // recursively encountered application struct into reflected POCOs.
                return;
            }
            if (RegisterManagedDependencyTypeAccess(
                ctx,
                methodDecoder,
                methodEntry,
                type,
                access,
                nonPublicMemberAttributeTypeName,
                excludedTypes))
            {
                // The provider explicitly names the requested contract or a concrete
                // implementation of it. Its dependencies describe how that contract is
                // constructed and accessed; applying the caller's POCO reflection policy
                // to the framework type as well would independently retain desktop
                // implementation details such as Dictionary's serialization machinery.
                return;
            }
            RegisterReflectedTypeHierarchy(ctx, type);
            if (access.HasFlag(ManagedTypeAccess.CustomAttributes))
            {
                RegisterCustomAttributes(
                    ctx,
                    methodDecoder,
                    methodEntry,
                    type.CustomAttributes);
            }
            if (type.IsArray)
            {
                // Managed type access describes a POCO contract. Array elements require a
                // separate, explicit reachability decision; inherited System.Array members
                // are not members of that contract.
                return;
            }
            if (access.HasFlag(ManagedTypeAccess.PublicParameterlessConstructor)
                && !type.IsValueType)
            {
                var constructor = type.GetConstructor(Type.EmptyTypes);
                if (constructor != null)
                {
                    RegisterRuntimeOverrideManagedMember(ctx, methodDecoder, methodEntry, constructor);
                }
            }
            if (access.HasFlag(ManagedTypeAccess.InstanceConstructors)
                && (!type.IsValueType || isDirectValueTypeContract))
            {
                foreach (var constructor in type.GetConstructors(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly))
                {
                    RegisterRuntimeOverrideManagedMember(ctx, methodDecoder, methodEntry, constructor);
                }
            }
            if (access.HasFlag(ManagedTypeAccess.PublicInstanceMethods))
            {
                for (var current = type; current != null; current = current.BaseType)
                {
                    foreach (var method in current.GetMethods(
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.DeclaredOnly))
                    {
                        RegisterRuntimeOverrideManagedMember(
                            ctx,
                            methodDecoder,
                            methodEntry,
                            method);
                    }
                }
            }

            // Reflection's inherited-member view collapses a base property hidden by a
            // derived property with the same CLR name. Runtime-override contracts can
            // distinguish those members through attributes or other package rules, so
            // retain every requested property declared across the hierarchy.
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var property in current.GetProperties(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly))
                {
                    if (access.HasFlag(ManagedTypeAccess.CustomAttributes))
                    {
                        RegisterCustomAttributes(
                            ctx,
                            methodDecoder,
                            methodEntry,
                            property.CustomAttributes);
                    }
                    if (property.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }
                    var isPublic = property.GetMethod?.IsPublic == true
                        || property.SetMethod?.IsPublic == true;
                    var readAccess = isPublic
                        ? ManagedTypeAccess.ReadPublicProperties
                        : ManagedTypeAccess.ReadNonPublicProperties;
                    var writeAccess = isPublic
                        ? ManagedTypeAccess.WritePublicProperties
                        : ManagedTypeAccess.WriteNonPublicProperties;
                    if (!isPublic
                        && nonPublicMemberAttributeTypeName != null
                        && !HasAttribute(property.CustomAttributes, nonPublicMemberAttributeTypeName))
                    {
                        continue;
                    }
                    if (access.HasFlag(readAccess)
                        && property.GetMethod != null)
                    {
                        RegisterRuntimeOverrideManagedMember(ctx, methodDecoder, methodEntry, property.GetMethod);
                    }
                    if (access.HasFlag(writeAccess)
                        && property.SetMethod != null)
                    {
                        RegisterRuntimeOverrideManagedMember(ctx, methodDecoder, methodEntry, property.SetMethod);
                    }
                    if (access.HasFlag(ManagedTypeAccess.EnumMetadata)
                        && (access.HasFlag(readAccess) && property.GetMethod != null
                            || access.HasFlag(writeAccess) && property.SetMethod != null))
                    {
                        RegisterEnumMetadata(ctx, property.PropertyType);
                    }
                    if (access.HasFlag(readAccess) && property.GetMethod != null
                        || access.HasFlag(writeAccess) && property.SetMethod != null)
                    {
                        RegisterManagedMemberTypeAccess(
                            ctx,
                            methodDecoder,
                            methodEntry,
                            property.PropertyType,
                            access,
                            nonPublicMemberAttributeTypeName,
                            excludedTypes);
                    }
                }
            }

            if (access.HasFlag(ManagedTypeAccess.ReadPublicFields)
                || access.HasFlag(ManagedTypeAccess.WritePublicFields)
                || access.HasFlag(ManagedTypeAccess.ReadNonPublicFields)
                || access.HasFlag(ManagedTypeAccess.WriteNonPublicFields))
            {
                for (var current = type; current != null; current = current.BaseType)
                {
                    foreach (var field in current.GetFields(
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly))
                    {
                        if (access.HasFlag(ManagedTypeAccess.CustomAttributes))
                        {
                            RegisterCustomAttributes(
                                ctx,
                                methodDecoder,
                                methodEntry,
                                field.CustomAttributes);
                        }
                        var readAccess = field.IsPublic
                            ? ManagedTypeAccess.ReadPublicFields
                            : ManagedTypeAccess.ReadNonPublicFields;
                        var writeAccess = field.IsPublic
                            ? ManagedTypeAccess.WritePublicFields
                            : ManagedTypeAccess.WriteNonPublicFields;
                        if (!field.IsPublic
                            && nonPublicMemberAttributeTypeName != null
                            && !HasAttribute(field.CustomAttributes, nonPublicMemberAttributeTypeName))
                        {
                            continue;
                        }
                        if (!access.HasFlag(readAccess) && !access.HasFlag(writeAccess))
                        {
                            continue;
                        }
                        ctx.AllReferencedFields[field] = true;
                        ctx.AllReferencedTypes[field.FieldType.GetTypeInfo()] = true;
                        if (access.HasFlag(ManagedTypeAccess.EnumMetadata))
                        {
                            RegisterEnumMetadata(ctx, field.FieldType);
                        }
                        RegisterManagedMemberTypeAccess(
                            ctx,
                            methodDecoder,
                            methodEntry,
                            field.FieldType,
                            access,
                            nonPublicMemberAttributeTypeName,
                            excludedTypes);
                    }
                }
            }
        }

        private static void RegisterManagedMemberTypeAccess(
            ReaderContext ctx,
            MethodDecoder methodDecoder,
            MethodBaseEntry methodEntry,
            Type contractType,
            ManagedTypeAccess access,
            string nonPublicMemberAttributeTypeName,
            IReadOnlySet<Type> excludedTypes)
        {
            if (IsApplicationAssembly(ctx, contractType.Assembly))
            {
                // Provider reflection policy follows the application's model graph.
                // Framework members instead stay behind typed provider dependencies so
                // recursively inspecting a POCO cannot retain desktop implementation details.
                RegisterManagedTypeAccess(
                    ctx,
                    methodDecoder,
                    methodEntry,
                    contractType,
                    access,
                    nonPublicMemberAttributeTypeName,
                    excludedTypes,
                    isDirectProviderRequest: false);
                return;
            }

            RegisterManagedDependencyTypeAccess(
                ctx,
                methodDecoder,
                methodEntry,
                contractType,
                access,
                nonPublicMemberAttributeTypeName,
                excludedTypes);
        }

        private static bool RegisterManagedDependencyTypeAccess(
            ReaderContext ctx,
            MethodDecoder methodDecoder,
            MethodBaseEntry methodEntry,
            Type contractType,
            ManagedTypeAccess access,
            string nonPublicMemberAttributeTypeName,
            IReadOnlySet<Type> excludedTypes)
        {
            var implementationTypes = RuntimeOverrideTypeResolver.FindRuntimeOverrideDependencyImplementations(
                ctx,
                methodDecoder.MethodBase,
                contractType);
            foreach (var implementationType in implementationTypes)
            {
                RegisterReflectedTypeHierarchy(ctx, implementationType);
                RegisterRuntimeOverrideManagedType(ctx, methodDecoder.MethodBase, implementationType);
            }

            var relatedTypes = new HashSet<Type>();
            var hasCompatibleDependency = HasCompatibleRuntimeOverrideDependency(
                ctx,
                methodDecoder.MethodBase,
                contractType);
            if (hasCompatibleDependency)
            {
                relatedTypes.Add(contractType);
            }
            relatedTypes.UnionWith(implementationTypes);
            foreach (var relatedType in relatedTypes.Where(type => type.IsGenericType))
            {
                foreach (var argument in relatedType.GetGenericArguments())
                {
                    if (argument.IsGenericParameter)
                    {
                        continue;
                    }
                    if (argument.IsPrimitive || argument == typeof(string))
                    {
                        ctx.AllReferencedTypes[argument.GetTypeInfo()] = true;
                        RegisterRuntimeOverrideManagedType(ctx, methodDecoder.MethodBase, argument);
                        continue;
                    }
                    RegisterManagedTypeAccess(
                        ctx,
                        methodDecoder,
                        methodEntry,
                        argument,
                        access,
                        nonPublicMemberAttributeTypeName,
                        excludedTypes,
                        isDirectProviderRequest: false);
                }
            }
            return hasCompatibleDependency || implementationTypes.Count != 0;
        }

        private static bool IsApplicationValueTypeContract(
            ReaderContext ctx,
            Type type)
        {
            if (!type.IsValueType
                || type.IsPrimitive
                || type.IsEnum
                || type.IsByRefLike
                || Nullable.GetUnderlyingType(type) != null
                || IsValueTuple(type))
            {
                return false;
            }

            // ManagedTypeAccess describes application contracts. Framework value
            // types such as DateTime are terminal values owned by their companion
            // implementation, not structs whose entire constructor surface should
            // become a POCO contract. Assemblies resolved with the entry assembly's
            // load context include application and deployed library models while the
            // shared framework remains in the default context.
            var applicationContext = AssemblyLoadContext.GetLoadContext(
                ctx.EntryPoint.Module.Assembly);
            var declaringContext = AssemblyLoadContext.GetLoadContext(type.Assembly);
            return applicationContext != null && declaringContext == applicationContext;
        }

        private static void RegisterCustomAttributes(
            ReaderContext ctx,
            MethodDecoder methodDecoder,
            MethodBaseEntry methodEntry,
            IEnumerable<CustomAttributeData> attributes)
        {
            foreach (var attribute in attributes.Where(attribute =>
                IsApplicationAssembly(ctx, attribute.AttributeType.Assembly)))
            {
                RegisterReflectedTypeHierarchy(ctx, attribute.AttributeType);
                RegisterRuntimeOverrideManagedType(
                    ctx,
                    methodDecoder.MethodBase,
                    attribute.AttributeType);
                RegisterRuntimeOverrideManagedMember(
                    ctx,
                    methodDecoder,
                    methodEntry,
                    attribute.Constructor);

                foreach (var argument in attribute.NamedArguments)
                {
                    if (argument.MemberInfo is PropertyInfo property
                        && property.SetMethod != null)
                    {
                        RegisterRuntimeOverrideManagedMember(
                            ctx,
                            methodDecoder,
                            methodEntry,
                            property.SetMethod);
                    }
                    else if (argument.MemberInfo is FieldInfo field)
                    {
                        ctx.AllReferencedFields[field] = true;
                        ctx.AllReferencedTypes[field.FieldType.GetTypeInfo()] = true;
                    }
                }

                foreach (var method in attribute.AttributeType.GetMethods(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly))
                {
                    RegisterRuntimeOverrideManagedMember(
                        ctx,
                        methodDecoder,
                        methodEntry,
                        method);
                }
            }
        }

        private static bool IsApplicationAssembly(ReaderContext ctx, Assembly assembly)
        {
            var applicationContext = AssemblyLoadContext.GetLoadContext(
                ctx.EntryPoint.Module.Assembly);
            return applicationContext != null
                && AssemblyLoadContext.GetLoadContext(assembly) == applicationContext;
        }

        private static bool IsValueTuple(Type type)
            => type == typeof(ValueTuple)
                || type.IsGenericType
                    && type.GetGenericTypeDefinition().Namespace == "System"
                    && type.GetGenericTypeDefinition().Name.StartsWith(
                        "ValueTuple`",
                        StringComparison.Ordinal);

        private static bool HasCompatibleRuntimeOverrideDependency(
            ReaderContext ctx,
            MethodBase runtimeOverrideMethod,
            Type contractType)
        {
            var contractShapes = new[] { contractType }
                .Concat(contractType.GetInterfaces())
                .ToArray();
            foreach (var dependencyType in ctx.RuntimeOverrides.GetDependencies(runtimeOverrideMethod)
                .Select(method => method.DeclaringType)
                .Where(type => type != null))
            {
                foreach (var actualType in contractShapes)
                {
                    var substitutions = new Dictionary<Type, Type>();
                    if (RuntimeOverrideTypeResolver.TryUnifyTypePattern(
                        dependencyType!,
                        actualType,
                        substitutions))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void RegisterRuntimeOverrideManagedType(
            ReaderContext ctx,
            MethodBase runtimeOverrideMethod,
            Type type)
        {
            runtimeOverrideMethod = GenericsHelper.GetPossiblyGenericReferenceFromMethod(
                runtimeOverrideMethod);
            if (!ctx.RuntimeOverrideManagedTypes.TryGetValue(
                runtimeOverrideMethod,
                out var managedTypes))
            {
                managedTypes = new HashSet<Type>();
                ctx.RuntimeOverrideManagedTypes[runtimeOverrideMethod] = managedTypes;
            }
            managedTypes.Add(type);
        }

        private static void RegisterEnumMetadata(ReaderContext ctx, Type type)
        {
            while (type.HasElementType)
            {
                type = type.GetElementType()!;
            }
            if (type.IsGenericParameter)
            {
                return;
            }
            if (type.IsEnum)
            {
                var enumType = type.GetTypeInfo();
                ctx.EnumMetadataTypes.Add(enumType);
                ctx.AllReferencedTypes[enumType] = true;
                ctx.AllReferencedTypes[Enum.GetUnderlyingType(type).GetTypeInfo()] = true;
                return;
            }
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    RegisterEnumMetadata(ctx, argument);
                }
            }
        }

        private static bool HasAttribute(
            IEnumerable<CustomAttributeData> attributes,
            string attributeTypeName)
            => attributes.Any(attribute => string.Equals(
                NameNormalizers.GetFullTypeName(attribute.AttributeType),
                attributeTypeName,
                StringComparison.Ordinal));

        private static void RegisterReflectedTypeHierarchy(ReaderContext ctx, Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var normalized = current.GetTypeInfo();
                ctx.ReflectedTypes.Add(normalized);
                ctx.AllReferencedTypes[normalized] = true;
            }
        }

        private static void RegisterRuntimeOverrideManagedMember(
            ReaderContext ctx,
            MethodDecoder methodDecoder,
            MethodBaseEntry methodEntry,
            MethodBase member)
        {
            // Required-method analysis and runtime override lookup use the open generic
            // member identity. Register reflected members with that same identity so a
            // member discovered through List<Foo>, Dictionary<string, Foo>, or another
            // constructed generic contract is not queued under an unreachable decoder key.
            member = GenericsHelper.GetPossiblyGenericReferenceFromMethod(member);
            ctx.RuntimeOverrideDynamicRoots.Add(member);
            ctx.Queue.Enqueue(new MethodBaseEntry
            {
                MethodBase = member,
                Caller = methodEntry,
                Order = methodEntry.Order + 1
            });
            methodDecoder.ReferencedMethods[member] = true;
        }

    }
}
