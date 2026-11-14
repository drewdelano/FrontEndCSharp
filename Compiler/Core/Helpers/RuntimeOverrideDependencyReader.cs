using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Compiler.Helpers
{
    internal sealed class RuntimeOverrideDependencyReader : IDisposable
    {
        private readonly RuntimeOverrideInspectionLoadContext loadContext;
        private readonly IReadOnlyDictionary<MethodIdentity, MethodInfo> methods;

        internal RuntimeOverrideDependencyReader(
            string assemblyPath,
            IEnumerable<string> resolverPaths)
        {
            loadContext = new RuntimeOverrideInspectionLoadContext(resolverPaths);
            var assembly = loadContext.LoadAssembly(assemblyPath);
            methods = assembly.GetTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.DeclaredOnly
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Static
                    | BindingFlags.Instance))
                .ToDictionary(method => new MethodIdentity(
                    method.Module.ModuleVersionId,
                    method.MetadataToken));
        }

        internal MethodInfo ResolveProviderMethod(Guid moduleId, int metadataToken)
        {
            if (methods.TryGetValue(new MethodIdentity(moduleId, metadataToken), out var method))
            {
                return method;
            }
            throw new InvalidDataException(
                $"Runtime override provider token '{moduleId:D}:0x{metadataToken:X8}' does not identify a method.");
        }

        public void Dispose() => loadContext.Unload();

        private sealed record class MethodIdentity(Guid ModuleId, int MetadataToken);
    }

    internal sealed class RuntimeOverrideInspectionLoadContext : AssemblyLoadContext
    {
        private readonly IReadOnlyDictionary<string, string> resolverPaths;

        internal RuntimeOverrideInspectionLoadContext(IEnumerable<string> paths)
            : base(isCollectible: true)
        {
            resolverPaths = paths
                .GroupBy(
                    path => Path.GetFileNameWithoutExtension(path),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);
        }

        internal Assembly LoadAssembly(string path)
        {
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            return LoadFromStream(stream);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var sharedAssembly = Default.Assemblies.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.GetName().Name,
                    assemblyName.Name,
                    StringComparison.OrdinalIgnoreCase));
            if (sharedAssembly != null)
            {
                return sharedAssembly;
            }
            if (assemblyName.Name != null
                && resolverPaths.TryGetValue(assemblyName.Name, out var path))
            {
                return LoadAssembly(path);
            }
            return null;
        }
    }
}
