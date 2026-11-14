using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Compiler.Helpers;

internal static class RuntimeOverrideManifestGenerator
{
    private static readonly Regex Registration = new Regex(
        "globalThis\\.DotNet\\.Methods\\[['\"](?<signature>[^'\"]+)['\"]\\]",
        RegexOptions.CultureInvariant);

    public static void Generate(string resourceDirectory, string outputPath)
    {
        var entries = new List<JObject>();
        foreach (var resource in Directory.GetFiles(resourceDirectory, "*.js")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var registrations = Registration.Matches(File.ReadAllText(resource));
            if (registrations.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource}' must contain exactly one managed registration.");
            }

            var signature = registrations[0].Groups["signature"].Value;
            var expectedFile = NameNormalizers.GetRuntimeOverrideFileName(signature);
            if (Path.GetFileName(resource) != expectedFile)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource}' must be named '{expectedFile}' for '{signature}'.");
            }

            entries.Add(new JObject
            {
                ["signature"] = signature,
                ["file"] = expectedFile,
            });
        }

        var manifest = new JObject
        {
            ["version"] = 1,
            ["overrides"] = new JArray(entries),
        };
        File.WriteAllText(
            outputPath,
            manifest.ToString(Formatting.Indented) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine(
            $"Generated runtime override manifest with {entries.Count} entries in '{outputPath}'.");
    }
}
