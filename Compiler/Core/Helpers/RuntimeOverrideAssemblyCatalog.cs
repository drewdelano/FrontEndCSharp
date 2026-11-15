using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FrontEndCSharp.RuntimeOverrides;

namespace Compiler.Helpers
{
    public sealed class RuntimeOverrideAssemblyCatalog
    {
        private static readonly Regex RegistrationPattern = new Regex(
            @"globalThis\.DotNet\.Methods\[['\""'](?<signature>[^'\""']+)['\""']\]",
            RegexOptions.CultureInvariant);
        private const ManagedTypeAccess SupportedManagedTypeAccess =
            ManagedTypeAccess.PublicParameterlessConstructor
            | ManagedTypeAccess.ReadPublicProperties
            | ManagedTypeAccess.WritePublicProperties
            | ManagedTypeAccess.ReadPublicFields
            | ManagedTypeAccess.WritePublicFields
            | ManagedTypeAccess.InstanceConstructors
            | ManagedTypeAccess.ReadNonPublicProperties
            | ManagedTypeAccess.WriteNonPublicProperties
            | ManagedTypeAccess.ReadNonPublicFields
            | ManagedTypeAccess.WriteNonPublicFields
            | ManagedTypeAccess.EnumMetadata
            | ManagedTypeAccess.CustomAttributes
            | ManagedTypeAccess.PublicInstanceMethods;

        public string TargetAssemblyName { get; init; }

        public IReadOnlyDictionary<string, RuntimeOverrideAssemblyEntry> Overrides { get; init; }

        public IReadOnlyDictionary<string, JavaScriptPackageDependency> JavaScriptPackages { get; init; }

        public byte[] JavaScriptPackageArchive { get; init; }

        public IReadOnlyDictionary<string, string> RuntimeAssets { get; init; }
            = new Dictionary<string, string>(StringComparer.Ordinal);

        public static RuntimeOverrideAssemblyCatalog Load(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("The runtime override assembly does not exist.", fullPath);
            }

            var resources = RuntimeOverrideResourceCatalogReader.ReadEmbeddedResources(fullPath);
            var manifest = RuntimeOverrideResourceCatalogReader.ReadManifest(
                fullPath,
                resources);
            var resolverPaths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Concat(Directory.GetFiles(Path.GetDirectoryName(fullPath), "*.dll"))
                .Append(typeof(RuntimeOverrideAssemblyAttribute).Assembly.Location)
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            using var metadataContext = new MetadataLoadContext(new PathAssemblyResolver(resolverPaths));
            using var dependencyReader = new RuntimeOverrideDependencyReader(fullPath, resolverPaths);
            var assembly = metadataContext.LoadFromAssemblyPath(fullPath);
            var companionAssemblyName = assembly.GetName().Name
                ?? throw new InvalidDataException(
                    $"Runtime override assembly '{fullPath}' has no assembly simple name.");
            var assemblyAttributes = FindAttributes(
                assembly.GetCustomAttributesData(),
                typeof(RuntimeOverrideAssemblyAttribute).FullName).ToList();
            if (assemblyAttributes.Count != 1)
            {
                throw new InvalidDataException(
                    $"Runtime override assembly '{fullPath}' must declare exactly one "
                    + $"{nameof(RuntimeOverrideAssemblyAttribute)}.");
            }

            var targetAssemblyName = ReadRequiredStringArgument(assemblyAttributes[0], fullPath);
            var javaScriptPackages = RuntimeOverrideResourceCatalogReader.ReadJavaScriptPackages(
                assembly,
                fullPath);
            var javaScriptPackageArchive = RuntimeOverrideResourceCatalogReader.ReadJavaScriptPackageArchive(
                fullPath,
                resources,
                javaScriptPackages);
            var runtimeAssets = RuntimeOverrideResourceCatalogReader.ReadRuntimeAssets(
                fullPath,
                companionAssemblyName,
                resources);
            const string companionFileSuffix = ".fecs.dll";
            if (!Path.GetFileName(fullPath).EndsWith(companionFileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Runtime override assembly '{fullPath}' must use the '{companionFileSuffix}' suffix.");
            }

            var overrides = new Dictionary<string, RuntimeOverrideAssemblyEntry>(StringComparer.Ordinal);
            var runtimeAssetResourcePrefix = companionAssemblyName + ".RuntimeAssets.";
            var usedResources = runtimeAssets.Keys
                .Select(fileName => runtimeAssetResourcePrefix + fileName)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var providerType in assembly.GetTypes())
            {
                var typeAttributes = FindAttributes(
                    providerType.GetCustomAttributesData(),
                    typeof(RuntimeOverrideTypeAttribute).FullName).ToList();
                if (typeAttributes.Count == 0)
                {
                    continue;
                }
                if (typeAttributes.Count != 1)
                {
                    throw new InvalidDataException(
                        $"Runtime override provider '{providerType.FullName}' must declare exactly one "
                        + $"{nameof(RuntimeOverrideTypeAttribute)}.");
                }

                var targetType = ReadRequiredTypeArgument(typeAttributes[0], fullPath);
                var targetsDeclaredAssembly = string.Equals(
                    targetType.Assembly.GetName().Name,
                    targetAssemblyName,
                    StringComparison.Ordinal);
                var targetsCompanionImplementation = targetType.Assembly == assembly
                    && FindAttributes(
                        targetType.GetCustomAttributesData(),
                        typeof(RuntimeOverrideImplementationTypeAttribute).FullName).Count() == 1;
                if (!targetsDeclaredAssembly && !targetsCompanionImplementation)
                {
                    throw new InvalidDataException(
                        $"Provider '{providerType.FullName}' targets type '{targetType}', which is not in "
                        + $"declared target assembly '{targetAssemblyName}'.");
                }

                foreach (var providerMethod in providerType.GetMethods(
                    BindingFlags.DeclaredOnly
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Static
                    | BindingFlags.Instance))
                {
                    var methodAttributes = FindAttributes(
                        providerMethod.GetCustomAttributesData(),
                        typeof(JavaScriptOverrideAttribute).FullName).ToList();
                    if (methodAttributes.Count == 0)
                    {
                        continue;
                    }
                    if (methodAttributes.Count != 1)
                    {
                        throw new InvalidDataException(
                            $"Runtime override provider method '{providerMethod}' must declare exactly one "
                            + $"{nameof(JavaScriptOverrideAttribute)}.");
                    }
                    if (!providerMethod.IsStatic)
                    {
                        throw new InvalidDataException(
                            $"Runtime override provider method '{providerMethod}' must be static.");
                    }

                    var attribute = methodAttributes[0];
                    var memberKind = ReadMemberKind(attribute);
                    var targetMemberName = ReadTargetMemberName(attribute) ?? providerMethod.Name;
                    var signature = GetTargetSignature(targetType, providerMethod, memberKind, targetMemberName);
                    if (!manifest.TryGetValue(signature, out var fileName))
                    {
                        throw new InvalidDataException(
                            $"Runtime override provider method '{providerMethod}' is missing from the embedded manifest.");
                    }

                    var resourceName = resources.Keys.SingleOrDefault(name =>
                        name.EndsWith(".Overrides." + fileName, StringComparison.Ordinal));
                    if (resourceName == null)
                    {
                        throw new InvalidDataException(
                            $"Runtime override manifest entry '{signature}' refers to missing embedded resource '{fileName}'.");
                    }
                    if (!resources.TryGetValue(resourceName, out var javascriptBytes))
                    {
                        throw new InvalidDataException(
                            $"Runtime override provider method '{providerMethod}' refers to missing embedded "
                            + $"resource '{resourceName}'.");
                    }
                    var javascript = Encoding.UTF8.GetString(javascriptBytes);
                    if (!usedResources.Add(resourceName))
                    {
                        throw new InvalidDataException(
                            $"Embedded JavaScript resource '{resourceName}' is assigned to more than one override.");
                    }

                    ValidateTargetMember(targetType, providerMethod, memberKind, signature);
                    ValidateRegistration(fullPath, resourceName, javascript, signature);
                    var managedDependencies = RuntimeOverrideManagedDependencyInspector
                        .GetManagedDependencies(providerMethod, dependencyReader);
                    if (!overrides.TryAdd(signature, new RuntimeOverrideAssemblyEntry
                    {
                        Signature = signature,
                        JavaScript = javascript,
                        ManagedDependencies = managedDependencies,
                        ManagedReturnTypePatterns = RuntimeOverrideManagedDependencyInspector
                            .GetManagedReturnTypePatterns(providerMethod),
                        ManagedAwaitableResultTypePatterns =
                            RuntimeOverrideManagedDependencyInspector
                                .GetManagedAwaitableResultTypePatterns(
                                    providerMethod,
                                    dependencyReader),
                        ManagedByRefParameterWrites = RuntimeOverrideManagedDependencyInspector
                            .GetManagedByRefParameterWrites(
                            providerMethod,
                            dependencyReader,
                            memberKind),
                        ManagedGenericTypeAccesses = GetManagedGenericTypeAccesses(providerMethod),
                        ManagedParameterTypeAccesses = GetManagedParameterTypeAccesses(providerMethod, memberKind),
                    }))
                    {
                        throw new InvalidDataException(
                            $"Runtime override signature '{signature}' occurs more than once in '{fullPath}'.");
                    }
                }
            }

            if (overrides.Count == 0)
            {
                throw new InvalidDataException(
                    $"Runtime override assembly '{fullPath}' does not contain any JavaScript overrides.");
            }
            var unassignedManifestEntries = manifest.Keys
                .Where(signature => !overrides.ContainsKey(signature))
                .ToList();
            if (unassignedManifestEntries.Count != 0)
            {
                throw new InvalidDataException(
                    $"Runtime override manifest in '{fullPath}' contains entries without typed providers: "
                    + string.Join(", ", unassignedManifestEntries));
            }
            var unusedResources = resources.Keys
                .Where(resource => resource.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                .Where(resource => !usedResources.Contains(resource))
                .ToList();
            if (unusedResources.Count != 0)
            {
                throw new InvalidDataException(
                    $"Runtime override assembly '{fullPath}' contains unassigned JavaScript resources: "
                    + string.Join(", ", unusedResources));
            }

            return new RuntimeOverrideAssemblyCatalog
            {
                TargetAssemblyName = targetAssemblyName,
                Overrides = overrides,
                JavaScriptPackages = javaScriptPackages,
                JavaScriptPackageArchive = javaScriptPackageArchive,
                RuntimeAssets = runtimeAssets,
            };
        }

        private static IEnumerable<CustomAttributeData> FindAttributes(
            IEnumerable<CustomAttributeData> attributes,
            string attributeTypeName)
        {
            return attributes.Where(attribute =>
                string.Equals(attribute.AttributeType.FullName, attributeTypeName, StringComparison.Ordinal));
        }

        private static string ReadRequiredStringArgument(CustomAttributeData attribute, string path)
        {
            if (attribute.ConstructorArguments.Count != 1
                || attribute.ConstructorArguments[0].Value is not string value
                || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    $"Attribute '{attribute.AttributeType.FullName}' in '{path}' requires one non-empty string argument.");
            }

            return value;
        }

        private static Type ReadRequiredTypeArgument(CustomAttributeData attribute, string path)
        {
            if (attribute.ConstructorArguments.Count != 1
                || attribute.ConstructorArguments[0].Value is not Type value)
            {
                throw new InvalidDataException(
                    $"Attribute '{attribute.AttributeType.FullName}' in '{path}' requires one type argument.");
            }

            return value;
        }

        private static RuntimeOverrideMemberKind ReadMemberKind(CustomAttributeData attribute)
        {
            var arguments = attribute.NamedArguments.Where(candidate =>
                string.Equals(candidate.MemberName, nameof(JavaScriptOverrideAttribute.MemberKind), StringComparison.Ordinal));
            var memberKind = arguments.Any()
                ? (RuntimeOverrideMemberKind)Convert.ToInt32(arguments.Single().TypedValue.Value)
                : RuntimeOverrideMemberKind.StaticMethod;
            if (!Enum.IsDefined(memberKind))
            {
                throw new InvalidDataException($"Runtime override member kind '{memberKind}' is invalid.");
            }

            return memberKind;
        }

        private static string ReadTargetMemberName(CustomAttributeData attribute)
        {
            var arguments = attribute.NamedArguments.Where(candidate =>
                string.Equals(candidate.MemberName, nameof(JavaScriptOverrideAttribute.TargetMemberName), StringComparison.Ordinal));
            var targetMemberName = arguments.Any() ? (string)arguments.Single().TypedValue.Value : null;
            if (targetMemberName != null && string.IsNullOrWhiteSpace(targetMemberName))
            {
                throw new InvalidDataException("A runtime override target member name cannot be empty.");
            }

            return targetMemberName;
        }

        private static string GetTargetSignature(
            Type targetType,
            MethodInfo providerMethod,
            RuntimeOverrideMemberKind memberKind,
            string targetMemberName)
        {
            var parameters = providerMethod.GetParameters().AsEnumerable();
            if (memberKind == RuntimeOverrideMemberKind.InstanceMethod)
            {
                var receiver = parameters.FirstOrDefault();
                if (receiver == null
                    || !string.Equals(
                        NameNormalizers.GetFullyQualifiedTypeName(receiver.ParameterType),
                        NameNormalizers.GetFullyQualifiedTypeName(targetType),
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Instance override provider '{providerMethod}' must declare '{targetType}' as its first parameter.");
                }
                parameters = parameters.Skip(1);
            }
            else if (memberKind == RuntimeOverrideMemberKind.Constructor)
            {
                targetMemberName = ".ctor";
                if (!string.Equals(
                    NameNormalizers.GetFullyQualifiedTypeName(providerMethod.ReturnType),
                    NameNormalizers.GetFullyQualifiedTypeName(targetType),
                    StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Constructor override provider '{providerMethod}' must return '{targetType}'.");
                }
            }

            var genericParameters = providerMethod.IsGenericMethodDefinition
                ? $"<{string.Join(", ", providerMethod.GetGenericArguments().Select(argument => argument.Name))}>"
                : string.Empty;
            var parameterSignature = string.Join(
                ", ",
                parameters.Select(parameter => NameNormalizers.GetFullyQualifiedTypeName(parameter.ParameterType)));
            return $"{NameNormalizers.GetFullyQualifiedTypeName(targetType)}.{targetMemberName}{genericParameters}({parameterSignature})";
        }

        private static IReadOnlyList<RuntimeOverrideManagedGenericTypeAccess> GetManagedGenericTypeAccesses(
            MethodInfo providerMethod)
        {
            if (!providerMethod.IsGenericMethodDefinition)
            {
                return Array.Empty<RuntimeOverrideManagedGenericTypeAccess>();
            }

            return providerMethod.GetGenericArguments()
                .Select((parameter, index) => new { parameter, index })
                .Select(item => new
                {
                    item.index,
                    Attributes = FindAttributes(
                        item.parameter.GetCustomAttributesData(),
                        typeof(ManagedTypeAccessAttribute).FullName).ToList()
                })
                .Where(item => item.Attributes.Count != 0)
                .Select(item =>
                {
                    var declaration = ReadManagedTypeAccess(
                        item.Attributes,
                        $"generic parameter {item.index} on runtime override provider '{providerMethod}'");

                    return new RuntimeOverrideManagedGenericTypeAccess
                    {
                        GenericParameterIndex = item.index,
                        Access = declaration.Access,
                        NonPublicMemberAttributeTypeName =
                            declaration.NonPublicMemberAttributeTypeName,
                        ExcludedTypes = declaration.ExcludedTypes,
                    };
                })
                .ToList();
        }

        private static IReadOnlyList<RuntimeOverrideManagedParameterTypeAccess> GetManagedParameterTypeAccesses(
            MethodInfo providerMethod,
            RuntimeOverrideMemberKind memberKind)
        {
            var receiverOffset = memberKind == RuntimeOverrideMemberKind.InstanceMethod ? 1 : 0;
            return providerMethod.GetParameters()
                .Select((parameter, index) => new { parameter, index })
                .Select(item => new
                {
                    item.index,
                    Attributes = FindAttributes(
                        item.parameter.GetCustomAttributesData(),
                        typeof(ManagedTypeAccessAttribute).FullName).ToList()
                })
                .Where(item => item.Attributes.Count != 0)
                .Select(item =>
                {
                    var declaration = ReadManagedTypeAccess(
                        item.Attributes,
                        $"parameter {item.index} on runtime override provider '{providerMethod}'");
                    return new RuntimeOverrideManagedParameterTypeAccess
                    {
                        ParameterIndex = item.index - receiverOffset,
                        Access = declaration.Access,
                        NonPublicMemberAttributeTypeName =
                            declaration.NonPublicMemberAttributeTypeName,
                        ExcludedTypes = declaration.ExcludedTypes,
                    };
                })
                .ToList();
        }

        private static ManagedTypeAccessDeclaration ReadManagedTypeAccess(
            IReadOnlyList<CustomAttributeData> attributes,
            string location)
        {
            if (attributes.Count != 1 || attributes[0].ConstructorArguments.Count != 1)
            {
                throw new InvalidDataException(
                    $"{location} has an invalid {nameof(ManagedTypeAccessAttribute)} declaration.");
            }

            var access = (ManagedTypeAccess)Convert.ToInt32(
                attributes[0].ConstructorArguments[0].Value);
            if (access == ManagedTypeAccess.None
                || (access & ~SupportedManagedTypeAccess) != 0)
            {
                throw new InvalidDataException(
                    $"{location} declares invalid managed type access '{access}'.");
            }

            var unsupportedNamedArguments = attributes[0].NamedArguments.Where(argument =>
                argument.MemberName != nameof(ManagedTypeAccessAttribute.NonPublicMemberAttributeType)
                    && argument.MemberName != nameof(ManagedTypeAccessAttribute.ExcludedTypes))
                .ToList();
            if (unsupportedNamedArguments.Count != 0)
            {
                throw new InvalidDataException(
                    $"{location} has unsupported managed type access setting "
                    + $"'{unsupportedNamedArguments[0].MemberName}'.");
            }

            var attributeArguments = attributes[0].NamedArguments
                .Where(argument => argument.MemberName
                    == nameof(ManagedTypeAccessAttribute.NonPublicMemberAttributeType))
                .ToList();
            if (attributeArguments.Count > 1
                || attributeArguments.Count == 1
                    && attributeArguments[0].TypedValue.Value is not Type)
            {
                throw new InvalidDataException(
                    $"{location} has an invalid non-public member attribute filter.");
            }

            string attributeTypeName = null;
            if (attributeArguments.Count == 1)
            {
                const ManagedTypeAccess nonPublicAccess =
                    ManagedTypeAccess.ReadNonPublicProperties
                    | ManagedTypeAccess.WriteNonPublicProperties
                    | ManagedTypeAccess.ReadNonPublicFields
                    | ManagedTypeAccess.WriteNonPublicFields;
                if ((access & nonPublicAccess) == 0)
                {
                    throw new InvalidDataException(
                        $"{location} declares a non-public member attribute filter without non-public access.");
                }
                attributeTypeName = NameNormalizers.GetFullTypeName(
                    (Type)attributeArguments[0].TypedValue.Value!);
            }
            var excludedTypeArguments = attributes[0].NamedArguments
                .Where(argument => argument.MemberName
                    == nameof(ManagedTypeAccessAttribute.ExcludedTypes))
                .ToList();
            if (excludedTypeArguments.Count > 1
                || excludedTypeArguments.Count == 1
                    && excludedTypeArguments[0].TypedValue.Value
                        is not IEnumerable<CustomAttributeTypedArgument>)
            {
                throw new InvalidDataException(
                    $"{location} has an invalid managed type access exclusion list.");
            }

            var excludedTypes = new List<RuntimeOverrideManagedTypeReference>();
            if (excludedTypeArguments.Count == 1)
            {
                var values = (IEnumerable<CustomAttributeTypedArgument>)
                    excludedTypeArguments[0].TypedValue.Value!;
                foreach (var value in values)
                {
                    if (value.Value is not Type excludedType
                        || excludedType.IsByRef
                        || excludedType.IsPointer
                        || excludedType.ContainsGenericParameters)
                    {
                        throw new InvalidDataException(
                            $"{location} has an invalid managed type access exclusion.");
                    }
                    excludedTypes.Add(RuntimeOverrideManagedDependencyInspector
                        .CreateManagedTypeReference(excludedType));
                }
            }
            return new ManagedTypeAccessDeclaration(access, attributeTypeName, excludedTypes);
        }

        private static void ValidateTargetMember(
            Type targetType,
            MethodInfo providerMethod,
            RuntimeOverrideMemberKind memberKind,
            string signature)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic;
            IEnumerable<MethodBase> candidates;
            if (memberKind == RuntimeOverrideMemberKind.Constructor)
            {
                candidates = targetType.GetConstructors(flags | BindingFlags.Instance);
            }
            else
            {
                flags |= memberKind == RuntimeOverrideMemberKind.StaticMethod
                    ? BindingFlags.Static
                    : BindingFlags.Instance;
                candidates = targetType.GetMethods(flags);
            }

            var matchingMembers = candidates.Where(candidate =>
                string.Equals(
                    NameNormalizers.GetFullyQualifiedMethodName(candidate),
                    signature,
                    StringComparison.Ordinal)).ToList();
            if (matchingMembers.Count == 0)
            {
                throw new InvalidDataException(
                    $"Provider method '{providerMethod}' does not identify a member named '{signature}' on the target type.");
            }

            if (matchingMembers.OfType<MethodInfo>().Any()
                && !matchingMembers.OfType<MethodInfo>().Any(targetMethod =>
                    string.Equals(
                        NameNormalizers.GetFullyQualifiedTypeName(providerMethod.ReturnType),
                        NameNormalizers.GetFullyQualifiedTypeName(targetMethod.ReturnType),
                        StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Provider method '{providerMethod}' has the wrong return type for '{signature}'.");
            }
        }

        private static void ValidateRegistration(
            string assemblyPath,
            string resourceName,
            string javascript,
            string signature)
        {
            var registrations = RegistrationPattern.Matches(javascript);
            if (registrations.Count != 1
                || !string.Equals(
                    registrations[0].Groups["signature"].Value,
                    signature,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Embedded resource '{resourceName}' in '{assemblyPath}' must register exactly "
                    + $"the provider signature '{signature}'.");
            }
        }

        private sealed record class ManagedTypeAccessDeclaration(
            ManagedTypeAccess Access,
            string NonPublicMemberAttributeTypeName,
            IReadOnlyList<RuntimeOverrideManagedTypeReference> ExcludedTypes);
    }

}
