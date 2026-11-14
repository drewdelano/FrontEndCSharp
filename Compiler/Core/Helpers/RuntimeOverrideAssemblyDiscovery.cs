using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;

namespace Compiler.Helpers
{
    public static class RuntimeOverrideAssemblyDiscovery
    {
        public static RuntimeOverrideCollection Discover(string applicationAssemblyPath)
            => Discover(applicationAssemblyPath, targetLoadContext: null);

        internal static RuntimeOverrideCollection Discover(
            string applicationAssemblyPath,
            AssemblyLoadContext targetLoadContext)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(applicationAssemblyPath);
            var fullAssemblyPath = Path.GetFullPath(applicationAssemblyPath);
            var dependencyPath = Path.ChangeExtension(fullAssemblyPath, ".deps.json");
            if (!File.Exists(dependencyPath))
            {
                return RuntimeOverrideCollection.Empty;
            }

            var document = JObject.Parse(File.ReadAllText(dependencyPath));
            var targetName = (string)document["runtimeTarget"]?["name"];
            var targets = document["targets"] as JObject
                ?? throw new InvalidDataException($"Dependency manifest '{dependencyPath}' has no targets.");
            var target = targetName == null
                ? targets.Properties().SingleOrDefault()?.Value as JObject
                : targets[targetName] as JObject;
            if (target == null)
            {
                throw new InvalidDataException(
                    $"Dependency manifest '{dependencyPath}' does not contain runtime target '{targetName}'.");
            }

            var assetPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var library in target.Properties().Select(property => property.Value).OfType<JObject>())
            {
                AddRuntimeAssets(library["runtime"] as JObject, assetPaths, requireRuntimeAssetType: false);
                AddRuntimeAssets(library["runtimeTargets"] as JObject, assetPaths, requireRuntimeAssetType: true);
            }

            var applicationDirectory = Path.GetDirectoryName(fullAssemblyPath)
                ?? throw new InvalidOperationException($"Application assembly '{fullAssemblyPath}' has no directory.");
            var companionPaths = assetPaths
                .Where(path => Path.GetFileName(path).EndsWith(".fecs.dll", StringComparison.OrdinalIgnoreCase))
                .Select(path => ResolveAsset(applicationDirectory, path, dependencyPath))
                .ToList();
            return RuntimeOverrideCollection.Load(companionPaths, targetLoadContext);
        }

        static void AddRuntimeAssets(
            JObject assets,
            ISet<string> assetPaths,
            bool requireRuntimeAssetType)
        {
            if (assets == null)
            {
                return;
            }

            foreach (var asset in assets.Properties())
            {
                if (requireRuntimeAssetType
                    && !string.Equals((string)asset.Value["assetType"], "runtime", StringComparison.Ordinal))
                {
                    continue;
                }
                assetPaths.Add(asset.Name);
            }
        }

        static string ResolveAsset(string applicationDirectory, string assetPath, string dependencyPath)
        {
            var relativeCandidate = Path.Combine(
                applicationDirectory,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(relativeCandidate))
            {
                return relativeCandidate;
            }

            var flatCandidate = Path.Combine(applicationDirectory, Path.GetFileName(assetPath));
            if (File.Exists(flatCandidate))
            {
                return flatCandidate;
            }

            throw new FileNotFoundException(
                $"Runtime override dependency '{assetPath}' declared by '{dependencyPath}' was not copied beside the application.",
                flatCandidate);
        }
    }
}
