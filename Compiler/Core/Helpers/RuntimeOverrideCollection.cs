using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using FrontEndCSharp.RuntimeOverrides;

namespace Compiler.Helpers
{
    public sealed class RuntimeOverrideCollection
    {
        private static readonly Regex StaticModuleSpecifierPattern = new Regex(
            @"(?:\bfrom\s+|\bimport\s*)(?<quote>['""])(?<specifier>[^'""]+)\k<quote>",
            RegexOptions.CultureInvariant);
        private const string RuntimeAssetModulePrefix = "runtime-assets/";

        readonly IReadOnlyDictionary<string, RuntimeOverrideAssemblyEntry> entries;
        readonly Dictionary<DependencyMethodIdentity, MethodBase> dependencyMethods = new();
        readonly Dictionary<MethodBase, RuntimeOverrideLookup> methodLookups = new();
        readonly IReadOnlyList<byte[]> javaScriptPackageArchives;
        readonly IReadOnlyDictionary<string, string> companionAssemblyPaths;
        readonly IReadOnlyDictionary<string, string> runtimeAssets;
        readonly AssemblyLoadContext targetLoadContext;

        RuntimeOverrideCollection(
            IReadOnlyDictionary<string, RuntimeOverrideAssemblyEntry> entries,
            IReadOnlyDictionary<string, JavaScriptPackageDependency> javaScriptPackages,
            IReadOnlyList<byte[]> javaScriptPackageArchives,
            IReadOnlyDictionary<string, string> companionAssemblyPaths,
            IReadOnlyDictionary<string, string> runtimeAssets,
            AssemblyLoadContext targetLoadContext)
        {
            this.entries = entries;
            JavaScriptPackages = javaScriptPackages;
            this.javaScriptPackageArchives = javaScriptPackageArchives;
            this.companionAssemblyPaths = companionAssemblyPaths;
            this.runtimeAssets = runtimeAssets;
            this.targetLoadContext = targetLoadContext;
        }

        public IReadOnlyDictionary<string, JavaScriptPackageDependency> JavaScriptPackages { get; }

        public static RuntimeOverrideCollection Empty { get; } = Load(Array.Empty<string>());

        internal int CachedMethodLookupCount
        {
            get
            {
                lock (methodLookups)
                {
                    return methodLookups.Count;
                }
            }
        }

        public static RuntimeOverrideCollection Load(IEnumerable<string> assemblyPaths)
            => Load(assemblyPaths, targetLoadContext: null);

        internal static RuntimeOverrideCollection Load(
            IEnumerable<string> assemblyPaths,
            AssemblyLoadContext targetLoadContext)
        {
            ArgumentNullException.ThrowIfNull(assemblyPaths);
            var entries = new Dictionary<string, RuntimeOverrideAssemblyEntry>(StringComparer.Ordinal);
            var javaScriptPackages = new Dictionary<string, JavaScriptPackageDependency>(StringComparer.Ordinal);
            var javaScriptPackageArchives = new List<byte[]>();
            var companionAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            var runtimeAssets = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var assemblyPath in assemblyPaths
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var assemblyName = AssemblyName.GetAssemblyName(assemblyPath).Name
                    ?? throw new InvalidDataException(
                        $"Runtime override companion '{assemblyPath}' has no assembly simple name.");
                if (!companionAssemblyPaths.TryAdd(assemblyName, assemblyPath))
                {
                    throw new InvalidDataException(
                        $"More than one runtime override companion is named '{assemblyName}'.");
                }
                var catalog = RuntimeOverrideAssemblyCatalog.Load(assemblyPath);
                foreach (var asset in catalog.RuntimeAssets)
                {
                    var relativePath = assemblyName + "/" + asset.Key;
                    if (!runtimeAssets.TryAdd(relativePath, asset.Value))
                    {
                        throw new InvalidDataException(
                            $"Runtime asset path '{relativePath}' occurs more than once.");
                    }
                }
                if (catalog.JavaScriptPackageArchive != null)
                {
                    javaScriptPackageArchives.Add(catalog.JavaScriptPackageArchive);
                }
                foreach (var pair in catalog.Overrides)
                {
                    if (!entries.TryAdd(pair.Key, pair.Value))
                    {
                        throw new InvalidDataException(
                            $"Runtime override '{pair.Key}' occurs in more than one companion assembly.");
                    }
                }
                MergeJavaScriptPackages(javaScriptPackages, catalog.JavaScriptPackages);
            }

            return new RuntimeOverrideCollection(
                entries,
                javaScriptPackages,
                javaScriptPackageArchives,
                companionAssemblyPaths,
                runtimeAssets,
                targetLoadContext);
        }

        internal static void MergeJavaScriptPackages(
            IDictionary<string, JavaScriptPackageDependency> destination,
            IEnumerable<KeyValuePair<string, JavaScriptPackageDependency>> packages)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(packages);
            foreach (var pair in packages)
            {
                if (destination.TryGetValue(pair.Key, out var existing))
                {
                    if (!string.Equals(existing.VersionRange, pair.Value.VersionRange, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            $"JavaScript package '{pair.Key}' has incompatible runtime-override declarations "
                            + $"('{existing.VersionRange}' {existing.Kind} and "
                            + $"'{pair.Value.VersionRange}' {pair.Value.Kind}).");
                    }
                    if (existing.Kind == JavaScriptPackageDependencyKind.PeerDependency
                        && pair.Value.Kind == JavaScriptPackageDependencyKind.Dependency)
                    {
                        destination[pair.Key] = pair.Value;
                    }
                    continue;
                }
                destination.Add(pair.Key, pair.Value);
            }
        }

        internal void ExtractRuntimeAssets(
            string targetDirectory,
            IEnumerable<MethodBase> runtimeOverrideMethods)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
            ArgumentNullException.ThrowIfNull(runtimeOverrideMethods);
            Directory.CreateDirectory(targetDirectory);
            var requiredAssets = GetRequiredRuntimeAssets(runtimeOverrideMethods);
            var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            foreach (var asset in requiredAssets)
            {
                var targetPath = Path.Combine(
                    targetDirectory,
                    asset.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.WriteAllText(
                    targetPath,
                    asset.Value,
                    utf8);
            }
        }

        private IReadOnlyDictionary<string, string> GetRequiredRuntimeAssets(
            IEnumerable<MethodBase> runtimeOverrideMethods)
        {
            var requiredAssets = new Dictionary<string, string>(StringComparer.Ordinal);
            var pendingModules = new Queue<(string Path, string JavaScript)>();
            foreach (var method in runtimeOverrideMethods.Distinct())
            {
                if (TryGetEntry(method, out var entry))
                {
                    pendingModules.Enqueue((
                        "runtime/" + NameNormalizers.GetRuntimeOverrideFileName(entry.Signature),
                        entry.JavaScript));
                }
            }

            while (pendingModules.Count != 0)
            {
                var module = pendingModules.Dequeue();
                foreach (Match match in StaticModuleSpecifierPattern.Matches(module.JavaScript))
                {
                    var specifier = match.Groups["specifier"].Value;
                    if (!specifier.StartsWith(".", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var importedModulePath = ResolveModulePath(module.Path, specifier);
                    if (!importedModulePath.StartsWith(
                        RuntimeAssetModulePrefix,
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var relativeAssetPath = importedModulePath[RuntimeAssetModulePrefix.Length..];
                    if (!runtimeAssets.TryGetValue(relativeAssetPath, out var javaScript))
                    {
                        throw new InvalidDataException(
                            $"JavaScript module '{module.Path}' imports unavailable runtime asset "
                            + $"'{importedModulePath}'. The owning companion must be selected explicitly.");
                    }
                    if (requiredAssets.TryAdd(relativeAssetPath, javaScript))
                    {
                        pendingModules.Enqueue((importedModulePath, javaScript));
                    }
                }
            }

            return requiredAssets;
        }

        private static string ResolveModulePath(string sourceModulePath, string specifier)
        {
            var segments = sourceModulePath.Split('/').SkipLast(1).ToList();
            foreach (var segment in specifier.Split('/'))
            {
                if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal))
                {
                    continue;
                }
                if (string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    if (segments.Count == 0)
                    {
                        throw new InvalidDataException(
                            $"JavaScript module '{sourceModulePath}' has escaping import '{specifier}'.");
                    }
                    segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                segments.Add(segment);
            }
            return string.Join("/", segments);
        }

        public void ExtractJavaScriptPackages(string targetDirectory)
            => JavaScriptPackageArchiveExtractor.Extract(
                targetDirectory,
                javaScriptPackageArchives);

        public bool HasRuntimeOverride(MethodBase method) =>
            TryGetEntry(method, out _);

        public string GetRuntimeOverrideFileName(MethodBase method)
        {
            var lookup = GetLookup(method);
            if (lookup.Entry != null)
            {
                return NameNormalizers.GetRuntimeOverrideFileName(lookup.Signature);
            }

            throw MissingOverride(method);
        }

        public List<MethodBase> GetDependencies(MethodBase method)
        {
            if (!TryGetEntry(method, out var entry))
            {
                throw MissingOverride(method);
            }

            var dependencies = new List<MethodBase>();
            foreach (var managedDependency in entry.ManagedDependencies)
            {
                var dependency = ResolveDependencyMethod(method, managedDependency, entry.Signature);
                if (managedDependency.GenericArgumentPatterns.Count > 0)
                {
                    if (dependency is not MethodInfo dependencyMethod
                        || !dependencyMethod.IsGenericMethodDefinition
                        || dependencyMethod.GetGenericArguments().Length
                            != managedDependency.GenericArgumentPatterns.Count)
                    {
                        throw new InvalidDataException(
                            $"Runtime override '{entry.Signature}' has invalid generic dependency "
                            + $"'{managedDependency.Signature}'.");
                    }
                    var arguments = managedDependency.GenericArgumentPatterns
                        .Select(pattern => ResolveDependencyTypePattern(method, pattern))
                        .ToArray();
                    dependency = dependencyMethod.MakeGenericMethod(arguments);
                }
                dependencies.Add(dependency);
            }
            return dependencies;
        }

        private Type ResolveDependencyTypePattern(
            MethodBase runtimeOverrideMethod,
            RuntimeOverrideManagedTypeReference reference)
        {
            if (reference.ArrayElementType != null)
            {
                return ResolveManagedTypeReference(
                    runtimeOverrideMethod,
                    reference,
                    "dependency generic argument");
            }
            if (reference.GenericParameterIndex >= 0)
            {
                if (runtimeOverrideMethod is MethodInfo genericMethod
                    && genericMethod.IsConstructedGenericMethod
                    && reference.GenericParameterIndex < genericMethod.GetGenericArguments().Length)
                {
                    return genericMethod.GetGenericArguments()[reference.GenericParameterIndex];
                }
                throw new InvalidDataException(
                    $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(runtimeOverrideMethod)}' "
                    + $"cannot resolve dependency generic parameter index "
                    + $"{reference.GenericParameterIndex}.");
            }

            var assembly = ResolveDependencyAssembly(
                runtimeOverrideMethod,
                reference.AssemblyName);
            return assembly.GetType(reference.TypeName, throwOnError: true, ignoreCase: false)
                ?? throw new InvalidDataException(
                    $"Managed dependency type '{reference.TypeName}' was not found in "
                    + $"assembly '{reference.AssemblyName}'.");
        }

        public IReadOnlyList<Type> GetReturnTypePatterns(MethodBase method)
        {
            if (!TryGetEntry(method, out var entry))
            {
                throw MissingOverride(method);
            }

            return entry.ManagedReturnTypePatterns
                .Select(reference => ResolveReturnTypePattern(method, reference))
                .ToList();
        }

        private Type ResolveReturnTypePattern(
            MethodBase method,
            RuntimeOverrideManagedTypeReference reference)
        {
            if (reference.GenericParameterIndex >= 0)
            {
                if (method is MethodInfo genericMethod
                    && genericMethod.IsGenericMethod
                    && reference.GenericParameterIndex < genericMethod.GetGenericArguments().Length)
                {
                    return genericMethod.GetGenericArguments()[reference.GenericParameterIndex];
                }
                throw new InvalidDataException(
                    $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(method)}' declares invalid "
                    + $"return generic parameter index {reference.GenericParameterIndex}.");
            }

            return ResolveManagedTypeReference(method, reference, "return type");
        }

        public IReadOnlyList<Type> GetAwaitableResultTypePatterns(MethodBase method)
        {
            if (!TryGetEntry(method, out var entry))
            {
                throw MissingOverride(method);
            }

            return entry.ManagedAwaitableResultTypePatterns
                .Select(reference => ResolveManagedTypeReference(method, reference, "awaitable result type"))
                .ToList();
        }

        public IReadOnlyList<RuntimeOverrideResolvedByRefParameterWrite>
            GetByRefParameterWrites(MethodBase method)
        {
            if (!TryGetEntry(method, out var entry))
            {
                throw MissingOverride(method);
            }
            var parameters = method.GetParameters();
            return entry.ManagedByRefParameterWrites.Select(write =>
            {
                if (write.ParameterIndex < 0
                    || write.ParameterIndex >= parameters.Length
                    || !parameters[write.ParameterIndex].ParameterType.IsByRef)
                {
                    throw new InvalidDataException(
                        $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(method)}' declares "
                        + $"an invalid by-ref parameter index {write.ParameterIndex}.");
                }
                var elementType = parameters[write.ParameterIndex].ParameterType.GetElementType()!;
                var types = write.Types
                    .Select(reference => ResolveManagedTypeReference(
                        method,
                        reference,
                        "by-ref parameter type"))
                    .ToArray();
                var incompatible = types.FirstOrDefault(type => !elementType.IsAssignableFrom(type));
                if (incompatible != null)
                {
                    throw new InvalidDataException(
                        $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(method)}' declares "
                        + $"incompatible by-ref result type '{incompatible}' for parameter "
                        + $"'{parameters[write.ParameterIndex].Name}' of type '{elementType}'.");
                }
                return new RuntimeOverrideResolvedByRefParameterWrite(
                    write.ParameterIndex,
                    write.IsOut,
                    types,
                    write.IncludesNull);
            }).ToArray();
        }

        private Type ResolveManagedTypeReference(
            MethodBase method,
            RuntimeOverrideManagedTypeReference reference,
            string kind)
        {
            if (reference.ArrayElementType != null)
            {
                ValidateArrayTypeReference(method, reference, kind);
                var elementType = ResolveManagedTypeReference(
                    method,
                    reference.ArrayElementType,
                    kind);
                return reference.ArrayKind == "szarray"
                    ? elementType.MakeArrayType()
                    : elementType.MakeArrayType(reference.ArrayRank);
            }
            var assembly = ResolveDependencyAssembly(method, reference.AssemblyName);
            var type = assembly.GetType(reference.TypeName, throwOnError: false, ignoreCase: false)
                ?? throw new InvalidDataException(
                    $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(method)}' declares unknown "
                    + $"{kind} pattern '{reference.TypeName}' in assembly '{assembly.FullName}'.");
            if (reference.GenericArguments.Count == 0)
            {
                return type;
            }
            if (!type.IsGenericTypeDefinition
                || type.GetGenericArguments().Length != reference.GenericArguments.Count)
            {
                throw new InvalidDataException(
                    $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(method)}' declares invalid "
                    + $"{kind} generic pattern '{reference.TypeName}'.");
            }
            return type.MakeGenericType(reference.GenericArguments
                .Select(argument => ResolveManagedTypeReference(method, argument, kind))
                .ToArray());
        }

        private static void ValidateArrayTypeReference(
            MethodBase method,
            RuntimeOverrideManagedTypeReference reference,
            string kind)
        {
            var validKind = reference.ArrayKind == "szarray" || reference.ArrayKind == "array";
            var validRank = reference.ArrayRank >= 1
                && (reference.ArrayKind != "szarray" || reference.ArrayRank == 1);
            // System.Type can distinguish SZARRAY from general ARRAY and retain
            // rank, but reflection cannot construct a type with explicit CLI size
            // or lower-bound vectors. Catalog-created references are therefore
            // required to carry the exact empty shape rather than silently losing
            // a non-empty one during resolution.
            var validShape = reference.ArraySizes.Count == 0
                && reference.ArrayLowerBounds.Count == 0;
            if (!validKind || !validRank || !validShape)
            {
                throw new InvalidDataException(
                    $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(method)}' declares invalid "
                    + $"{kind} array kind, rank, or shape.");
            }
        }

        public IReadOnlyList<RuntimeOverrideManagedGenericTypeAccess> GetManagedGenericTypeAccesses(MethodBase method)
        {
            if (TryGetEntry(method, out var entry))
            {
                return entry.ManagedGenericTypeAccesses;
            }
            throw MissingOverride(method);
        }

        public IReadOnlyList<RuntimeOverrideManagedParameterTypeAccess> GetManagedParameterTypeAccesses(MethodBase method)
        {
            if (TryGetEntry(method, out var entry))
            {
                return entry.ManagedParameterTypeAccesses;
            }
            throw MissingOverride(method);
        }

        public IReadOnlySet<Type> ResolveManagedTypeAccessExcludedTypes(
            MethodBase method,
            IReadOnlyList<RuntimeOverrideManagedTypeReference> references)
            => references
                .Select(reference => ResolveManagedTypeReference(
                    method,
                    reference,
                    "managed type access exclusion"))
                .ToHashSet();

        public void CopyRuntimeOverride(MethodBase method, string target)
        {
            if (!TryGetEntry(method, out var entry))
            {
                throw MissingOverride(method);
            }
            File.WriteAllText(target, entry.JavaScript);
        }

        bool TryGetEntry(MethodBase method, out RuntimeOverrideAssemblyEntry entry)
        {
            var lookup = GetLookup(method);
            entry = lookup.Entry;
            return entry != null;
        }

        RuntimeOverrideLookup GetLookup(MethodBase method)
        {
            // Empty is a process-wide singleton. Avoid retaining arbitrary MethodBase
            // instances in it, and avoid all normalization work for the common no-
            // companion case.
            if (entries.Count == 0)
            {
                return RuntimeOverrideLookup.Missing;
            }

            lock (methodLookups)
            {
                if (methodLookups.TryGetValue(method, out var lookup))
                {
                    return lookup;
                }

                lookup = ResolveLookup(method);
                methodLookups.Add(method, lookup);
                return lookup;
            }
        }

        RuntimeOverrideLookup ResolveLookup(MethodBase method)
        {
            foreach (var signature in GetSignatures(method))
            {
                if (entries.TryGetValue(signature, out var entry))
                {
                    return new RuntimeOverrideLookup(entry, signature);
                }
            }
            return RuntimeOverrideLookup.Missing;
        }

        static IEnumerable<string> GetSignatures(MethodBase method)
        {
            yield return NameNormalizers.GetFullyQualifiedMethodName(method);
            if (method.DeclaringType?.IsConstructedGenericType != true)
            {
                yield break;
            }

            var declaringTypeDefinition = method.DeclaringType.GetGenericTypeDefinition();
            MethodBase methodDefinition = method is ConstructorInfo
                ? declaringTypeDefinition
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(candidate => candidate.HasSameMetadataDefinitionAs(method))
                : declaringTypeDefinition
                    .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(candidate => candidate.HasSameMetadataDefinitionAs(method));
            yield return NameNormalizers.GetFullyQualifiedMethodName(methodDefinition);
        }

        MethodBase ResolveDependencyMethod(
            MethodBase runtimeOverrideMethod,
            RuntimeOverrideManagedDependency dependency,
            string runtimeOverrideSignature)
        {
            var assembly = ResolveDependencyAssembly(
                runtimeOverrideMethod,
                dependency.AssemblyName);
            var key = new DependencyMethodIdentity(assembly, dependency.Signature);
            lock (dependencyMethods)
            {
                if (dependencyMethods.TryGetValue(key, out var cached))
                {
                    return cached;
                }

                var flags = BindingFlags.DeclaredOnly
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Static
                    | BindingFlags.Instance;
                foreach (var type in GetLoadableTypes(assembly))
                {
                    IReadOnlyList<MethodBase> candidates;
                    try
                    {
                        candidates = type.GetMethods(flags)
                            .Cast<MethodBase>()
                            .Concat(type.GetConstructors(flags))
                            .ToList();
                    }
                    catch (Exception exception) when (IsUnavailableReflectionDependency(exception))
                    {
                        continue;
                    }

                    foreach (var method in candidates)
                    {
                        string signature;
                        try
                        {
                            signature = NameNormalizers.GetFullyQualifiedMethodName(method);
                        }
                        catch (Exception exception) when (IsUnavailableReflectionDependency(exception))
                        {
                            continue;
                        }
                        if (string.Equals(signature, dependency.Signature, StringComparison.Ordinal))
                        {
                            dependencyMethods.Add(key, method);
                            return method;
                        }
                    }
                }

                throw new InvalidDataException(
                    $"Runtime override '{runtimeOverrideSignature}' references unknown managed dependency "
                    + $"'{dependency.Signature}' in assembly '{assembly.FullName}'.");
            }
        }

        private Assembly ResolveDependencyAssembly(
            MethodBase runtimeOverrideMethod,
            string assemblyName)
        {
            var selectedAssembly = runtimeOverrideMethod.Module.Assembly;
            if (HasSimpleName(selectedAssembly, assemblyName))
            {
                return selectedAssembly;
            }

            if (targetLoadContext != null)
            {
                var resolved = ResolveFromLoadContext(
                    targetLoadContext,
                    assemblyName,
                    runtimeOverrideMethod,
                    useCompanionPath: true);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            var selectedLoadContext = AssemblyLoadContext.GetLoadContext(selectedAssembly);
            if (selectedLoadContext != null && selectedLoadContext != targetLoadContext)
            {
                var resolved = ResolveFromLoadContext(
                    selectedLoadContext,
                    assemblyName,
                    runtimeOverrideMethod,
                    useCompanionPath: false);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            var candidates = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => HasSimpleName(assembly, assemblyName))
                .ToList();
            return candidates.Count switch
            {
                1 => candidates[0],
                0 => throw new InvalidDataException(
                    $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(runtimeOverrideMethod)}' "
                    + $"requires unavailable assembly '{assemblyName}'."),
                _ => throw new InvalidDataException(
                    $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(runtimeOverrideMethod)}' "
                    + $"cannot select among multiple loaded assemblies named '{assemblyName}'.")
            };
        }

        private Assembly ResolveFromLoadContext(
            AssemblyLoadContext loadContext,
            string assemblyName,
            MethodBase runtimeOverrideMethod,
            bool useCompanionPath)
        {
            var loaded = loadContext.Assemblies
                .Where(assembly => HasSimpleName(assembly, assemblyName))
                .ToList();
            if (loaded.Count == 1)
            {
                return loaded[0];
            }
            if (loaded.Count > 1)
            {
                throw new InvalidDataException(
                    $"Load context '{loadContext.Name}' contains more than one assembly named "
                    + $"'{assemblyName}' while resolving dependencies for "
                    + $"'{NameNormalizers.GetFullyQualifiedMethodName(runtimeOverrideMethod)}'.");
            }

            try
            {
                if (useCompanionPath
                    && companionAssemblyPaths.TryGetValue(assemblyName, out var companionPath))
                {
                    return loadContext.LoadFromAssemblyPath(companionPath);
                }
                var resolved = loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));
                return HasSimpleName(resolved, assemblyName) ? resolved : null;
            }
            catch (Exception exception) when (IsUnavailableReflectionDependency(exception))
            {
                if (loadContext == targetLoadContext)
                {
                    throw new InvalidDataException(
                        $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(runtimeOverrideMethod)}' "
                        + $"requires assembly '{assemblyName}' in its target load context.",
                        exception);
                }
                return null;
            }
        }

        private static bool HasSimpleName(Assembly assembly, string name) =>
            string.Equals(assembly.GetName().Name, name, StringComparison.Ordinal);

        internal static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                // Optional framework dependencies can make unrelated types unloadable. Keep
                // resolving declared companion dependencies from the types reflection did load;
                // GetDependencies still rejects any declared method that remains unresolved.
                return exception.Types.OfType<Type>().ToList();
            }
        }

        static bool IsUnavailableReflectionDependency(Exception exception) =>
            exception is BadImageFormatException
                or FileNotFoundException
                or FileLoadException
                or TypeLoadException
                or ReflectionTypeLoadException;

        static InvalidOperationException MissingOverride(MethodBase method) =>
            new InvalidOperationException(
                $"No runtime override is registered for '{NameNormalizers.GetFullyQualifiedMethodName(method)}'.");

        private sealed record class DependencyMethodIdentity(Assembly Assembly, string Signature);

        private sealed record class RuntimeOverrideLookup(
            RuntimeOverrideAssemblyEntry Entry,
            string Signature)
        {
            public static RuntimeOverrideLookup Missing { get; } = new(null, null);
        }
    }

    public sealed record RuntimeOverrideResolvedByRefParameterWrite(
        int ParameterIndex,
        bool IsOut,
        IReadOnlyList<Type> Types,
        bool IncludesNull);
}
