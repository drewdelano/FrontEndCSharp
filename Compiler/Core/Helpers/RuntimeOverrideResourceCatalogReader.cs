using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using FrontEndCSharp.RuntimeOverrides;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Compiler.Helpers
{
    internal static class RuntimeOverrideResourceCatalogReader
    {
        private static readonly Regex JavaScriptPackageNamePattern = new Regex(
            @"^(?:@[a-z0-9][a-z0-9._-]*/)?[a-z0-9][a-z0-9._-]*$",
            RegexOptions.CultureInvariant);
        private static readonly Regex RuntimeAssetFileNamePattern = new Regex(
            @"^[a-z0-9][a-z0-9.-]*\.js$",
            RegexOptions.CultureInvariant);

        internal static IReadOnlyDictionary<string, string> ReadRuntimeAssets(
            string assemblyPath,
            string companionAssemblyName,
            IReadOnlyDictionary<string, byte[]> resources)
        {
            const string marker = ".RuntimeAssets.";
            var expectedPrefix = companionAssemblyName + marker;
            var assets = new Dictionary<string, string>(StringComparer.Ordinal);
            var strictUtf8 = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);
            foreach (var resource in resources.Where(candidate =>
                candidate.Key.Contains(marker, StringComparison.Ordinal)))
            {
                if (!resource.Key.StartsWith(expectedPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Runtime asset resource '{resource.Key}' in '{assemblyPath}' must belong to "
                        + $"companion assembly '{companionAssemblyName}'.");
                }
                var fileName = resource.Key[expectedPrefix.Length..];
                if (!RuntimeAssetFileNamePattern.IsMatch(fileName)
                    || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Runtime asset resource '{resource.Key}' in '{assemblyPath}' must use a lowercase "
                        + "JavaScript file name without a path.");
                }

                string source;
                try
                {
                    source = strictUtf8.GetString(resource.Value);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new InvalidDataException(
                        $"Runtime asset '{fileName}' in '{assemblyPath}' is not valid UTF-8.",
                        exception);
                }
                if (string.IsNullOrWhiteSpace(source))
                {
                    throw new InvalidDataException(
                        $"Runtime asset '{fileName}' in '{assemblyPath}' cannot be empty.");
                }
                if (!assets.TryAdd(fileName, source))
                {
                    throw new InvalidDataException(
                        $"Runtime asset '{fileName}' occurs more than once in '{assemblyPath}'.");
                }
            }
            return assets;
        }

        internal static byte[] ReadJavaScriptPackageArchive(
            string assemblyPath,
            IReadOnlyDictionary<string, byte[]> resources,
            IReadOnlyDictionary<string, JavaScriptPackageDependency> packages)
        {
            var archives = resources
                .Where(resource => string.Equals(
                    resource.Key,
                    "FrontEndCSharp.JavaScriptPackages.zip",
                    StringComparison.Ordinal))
                .ToList();
            if (archives.Count > 1)
            {
                throw new InvalidDataException(
                    $"Runtime override assembly '{assemblyPath}' contains more than one JavaScript package archive.");
            }

            var embeddedPackages = packages.Values
                .Where(package => package.Kind == JavaScriptPackageDependencyKind.Dependency)
                .ToList();
            if (embeddedPackages.Count == 0)
            {
                if (archives.Count != 0)
                {
                    throw new InvalidDataException(
                        $"Runtime override assembly '{assemblyPath}' embeds JavaScript packages but declares no dependencies.");
                }
                return null;
            }
            if (archives.Count != 1)
            {
                throw new InvalidDataException(
                    $"Runtime override assembly '{assemblyPath}' declares JavaScript dependencies but does not embed "
                    + "FrontEndCSharp.JavaScriptPackages.zip.");
            }

            var archiveBytes = archives[0].Value;
            using var stream = new MemoryStream(archiveBytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            foreach (var package in embeddedPackages)
            {
                var packagePath = package.PackageName + "/package.json";
                var entry = archive.Entries.SingleOrDefault(candidate =>
                    string.Equals(candidate.FullName, packagePath, StringComparison.Ordinal));
                if (entry == null)
                {
                    throw new InvalidDataException(
                        $"JavaScript package archive in '{assemblyPath}' does not contain '{packagePath}'.");
                }

                using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
                var document = JObject.Parse(reader.ReadToEnd());
                var actualName = (string)document["name"];
                var actualVersion = (string)document["version"];
                if (!string.Equals(actualName, package.PackageName, StringComparison.Ordinal)
                    || !string.Equals(actualVersion, package.VersionRange, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"JavaScript package archive in '{assemblyPath}' contains '{actualName}@{actualVersion}' "
                        + $"where '{package.PackageName}@{package.VersionRange}' was declared.");
                }
            }

            return archiveBytes;
        }

        internal static IReadOnlyDictionary<string, JavaScriptPackageDependency> ReadJavaScriptPackages(
            Assembly assembly,
            string path)
        {
            var packages = new Dictionary<string, JavaScriptPackageDependency>(StringComparer.Ordinal);
            foreach (var attribute in FindAttributes(
                assembly.GetCustomAttributesData(),
                typeof(JavaScriptPackageDependencyAttribute).FullName))
            {
                if (attribute.ConstructorArguments.Count != 2
                    || attribute.ConstructorArguments[0].Value is not string packageName
                    || attribute.ConstructorArguments[1].Value is not string versionRange
                    || !JavaScriptPackageNamePattern.IsMatch(packageName)
                    || string.IsNullOrWhiteSpace(versionRange))
                {
                    throw new InvalidDataException(
                        $"Attribute '{typeof(JavaScriptPackageDependencyAttribute).FullName}' in '{path}' "
                        + "requires a valid npm package name and non-empty version range.");
                }

                var kindArguments = attribute.NamedArguments.Where(candidate =>
                    string.Equals(
                        candidate.MemberName,
                        nameof(JavaScriptPackageDependencyAttribute.Kind),
                        StringComparison.Ordinal));
                var kind = kindArguments.Any()
                    ? (JavaScriptPackageDependencyKind)Convert.ToInt32(kindArguments.Single().TypedValue.Value)
                    : JavaScriptPackageDependencyKind.Dependency;
                if (!Enum.IsDefined(kind))
                {
                    throw new InvalidDataException(
                        $"JavaScript package dependency kind '{kind}' in '{path}' is invalid.");
                }

                if (!packages.TryAdd(packageName, new JavaScriptPackageDependency
                {
                    PackageName = packageName,
                    VersionRange = versionRange,
                    Kind = kind,
                }))
                {
                    throw new InvalidDataException(
                        $"JavaScript package '{packageName}' is declared more than once in '{path}'.");
                }
            }

            return packages;
        }

        internal static IReadOnlyDictionary<string, string> ReadManifest(
            string assemblyPath,
            IReadOnlyDictionary<string, byte[]> resources)
        {
            var manifestResources = resources
                .Where(resource => resource.Key.EndsWith(".Overrides.manifest.json", StringComparison.Ordinal))
                .ToList();
            if (manifestResources.Count != 1)
            {
                throw new InvalidDataException(
                    $"Runtime override assembly '{assemblyPath}' must contain exactly one embedded Overrides/manifest.json.");
            }

            var manifest = JsonConvert.DeserializeObject<RuntimeOverrideManifest>(
                Encoding.UTF8.GetString(manifestResources[0].Value));
            if (manifest == null || manifest.Version != 1 || manifest.Overrides == null)
            {
                throw new InvalidDataException(
                    $"Runtime override manifest in '{assemblyPath}' is invalid or has an unsupported version.");
            }

            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.Overrides)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.Signature)
                    || string.IsNullOrWhiteSpace(entry.File))
                {
                    throw new InvalidDataException(
                        $"Every runtime override manifest entry in '{assemblyPath}' requires a signature and file.");
                }
                if (!string.Equals(entry.File, Path.GetFileName(entry.File), StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Runtime override manifest entry '{entry.Signature}' must contain a file name, not a path.");
                }

                var expectedFile = NameNormalizers.GetRuntimeOverrideFileName(entry.Signature);
                if (!string.Equals(entry.File, expectedFile, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Runtime override '{entry.Signature}' must use deterministic file name '{expectedFile}'.");
                }
                if (!entries.TryAdd(entry.Signature, entry.File))
                {
                    throw new InvalidDataException(
                        $"Runtime override signature '{entry.Signature}' occurs more than once in '{assemblyPath}'.");
                }
                if (!files.Add(entry.File))
                {
                    throw new InvalidDataException(
                        $"Runtime override file '{entry.File}' occurs more than once in '{assemblyPath}'.");
                }
            }

            return entries;
        }


        internal static IReadOnlyDictionary<string, byte[]> ReadEmbeddedResources(string path)
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata || peReader.PEHeaders.CorHeader == null)
            {
                throw new InvalidDataException($"Runtime override assembly '{path}' is not a managed assembly.");
            }

            var metadataReader = peReader.GetMetadataReader();
            var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var resourcesRva = peReader.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress;
            foreach (var handle in metadataReader.ManifestResources)
            {
                var resource = metadataReader.GetManifestResource(handle);
                if (!resource.Implementation.IsNil)
                {
                    continue;
                }

                var name = metadataReader.GetString(resource.Name);
                var reader = peReader.GetSectionData(resourcesRva + checked((int)resource.Offset)).GetReader();
                var length = reader.ReadInt32();
                var bytes = reader.ReadBytes(length);
                resources.Add(name, bytes);
            }

            return resources;
        }

        private sealed class RuntimeOverrideManifest
        {
            public int Version { get; set; }

            public List<RuntimeOverrideManifestEntry> Overrides { get; set; }
        }

        private sealed class RuntimeOverrideManifestEntry
        {
            public string Signature { get; set; }

            public string File { get; set; }
        }


        private static IEnumerable<CustomAttributeData> FindAttributes(
            IEnumerable<CustomAttributeData> attributes,
            string attributeTypeName)
        {
            return attributes.Where(attribute =>
                string.Equals(
                    attribute.AttributeType.FullName,
                    attributeTypeName,
                    StringComparison.Ordinal));
        }
    }
}
