using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using System.IO;

namespace Compiler.Debugging
{
    public static class DebugManifestWriter
    {
        public const string FileSuffix = ".frontendcsharp.debug.json";

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Culture = CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string Serialize(DebugManifest manifest) =>
            JsonConvert.SerializeObject(manifest, Formatting.Indented, Settings);

        internal static string SerializeForHash(DebugManifest manifest) =>
            JsonConvert.SerializeObject(manifest, Formatting.None, Settings);

        public static void Write(string path, DebugManifest manifest)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory!);
            File.WriteAllText(path, Serialize(manifest));
        }

        public static string GetDefaultPath(string assemblyPath) =>
            Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!,
                $"{Path.GetFileNameWithoutExtension(assemblyPath)}{FileSuffix}");
    }
}
