using Compiler.Write;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Compiler.Debugging
{
    public static class EditAndContinueUpdateBuilder
    {
        private static readonly Regex RegistrationPattern = new Regex(
            "globalThis\\.DotNet\\.Methods\\[(?<name>\"(?:\\\\.|[^\"])*\")\\]\\s*=\\s*function",
            RegexOptions.Compiled);
        public static EditAndContinueUpdateResult Build(
            string baselineManifestPath,
            string candidateManifestPath,
            string baselineOutputPath,
            string candidateOutputPath,
            int generation,
            IReadOnlyCollection<int> updatedMethodTokens,
            string outputManifestPath)
        {
            if (generation <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }
            if (updatedMethodTokens == null || updatedMethodTokens.Count == 0)
            {
                throw new InvalidOperationException("Roslyn reported no updated methods.");
            }
            var baseline = ReadManifest(baselineManifestPath);
            var candidate = ReadManifest(candidateManifestPath);
            if (baseline.Generation != generation - 1)
            {
                throw new InvalidOperationException(
                    $"The baseline manifest is generation {baseline.Generation}, not {generation - 1}.");
            }

            baselineOutputPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baselineOutputPath));
            candidateOutputPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidateOutputPath));
            var staged = new List<StagedMethod>();
            foreach (var token in updatedMethodTokens.Distinct().OrderBy(value => value))
            {
                var baselineEntry = FindMethod(baseline, token);
                var candidateEntry = FindMethod(candidate, token);
                if (!string.Equals(
                    baselineEntry.Method.Name,
                    candidateEntry.Method.Name,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Method token 0x{token:x8} changed identity between generations.");
                }
                var definition = MsilBinaryPackage.ReadMethodDefinition(
                    Path.Combine(candidateOutputPath, MsilBinaryPackage.FileName),
                    candidateEntry.Module.ModuleId,
                    token);
                var source = $@"
globalThis.DotNet.Methods[{JsonConvert.ToString(definition.Name)}] = function(ctx, ...args) {{
    const blocks = {definition.Blocks.ToString(Formatting.None)};
    const methodInfo = {definition.Metadata.ToString(Formatting.None)};
    return ExecuteMSIL(ctx, methodInfo, blocks, args);
}}
".Trim()
                    .Replace(candidateEntry.Module.ModuleId, baselineEntry.Module.ModuleId, StringComparison.Ordinal);
                var registrations = RegistrationPattern.Matches(source);
                if (registrations.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"The emitted module for '{candidateEntry.Method.Name}' has an unexpected registration shape.");
                }
                var registration = registrations[0];
                var registryName = JsonConvert.DeserializeObject<string>(registration.Groups["name"].Value)
                    ?? throw new InvalidDataException("The emitted method registration has no name.");
                source = source.Remove(registration.Index, registration.Length)
                    .Insert(registration.Index, "export const updateMethod = function")
                    .Trim();

                const string candidateRelativeDirectory = "runtime";
                var baselineDirectory = Path.Combine(baselineOutputPath, candidateRelativeDirectory);
                Directory.CreateDirectory(baselineDirectory);
                var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
                    .ToLowerInvariant()[..12];
                var stagedName = $"enc-generation-{generation}-0x{token:x8}-{sourceHash}.js";
                var stagedPath = Path.Combine(baselineDirectory, stagedName);
                staged.Add(new StagedMethod(
                    token,
                    baselineEntry.Module.ModuleId,
                    registryName,
                    stagedPath,
                    Path.GetRelativePath(baselineOutputPath, stagedPath).Replace('\\', '/'),
                    source));
            }

            NormalizeManifest(candidate, baseline, generation);
            candidate.ContentHash = string.Empty;
            var updateRelativeDirectory = Path.GetDirectoryName(staged[0].RelativePath) ?? string.Empty;
            var methods = staged.Select((method, index) => $$"""
                    {
                        name: {{JsonConvert.ToString(method.Name)}},
                        moduleId: {{JsonConvert.ToString(method.ModuleId)}},
                        methodToken: {{method.Token}},
                        implementation: method{{index}}
                    }
                """);
            var implementations = staged.Select((method, index) =>
                method.Source.Replace(
                    "export const updateMethod",
                    $"const method{index}",
                    StringComparison.Ordinal));
            var updateSource = $$"""
                (() => {
                    const ExecuteMSIL = globalThis.__FrontEndCSharpExecuteMSIL;
                    if (typeof ExecuteMSIL !== "function") {
                        throw new Error("The FrontEndCSharp interpreter is not loaded.");
                    }
                {{string.Join(Environment.NewLine + Environment.NewLine, implementations)}}
                    const updatePackage = Object.freeze({
                        baselineBuildId: {{JsonConvert.ToString(baseline.BuildId)}},
                        expectedGeneration: {{generation - 1}},
                        nextGeneration: {{generation}},
                        hash: __frontEndCSharpUpdateHash,
                        methods: Object.freeze([
                {{string.Join("," + Environment.NewLine, methods)}}
                        ])
                    });
                    return globalThis.__FrontEndCSharpDebugger.applyUpdate(updatePackage);
                })()
                """;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(updateSource)))
                .ToLowerInvariant();
            candidate.ContentHash = hash;
            var updateRelativePath = Path.Combine(
                updateRelativeDirectory,
                $"enc-update-generation-{generation}-{hash[..12]}.js").Replace('\\', '/');
            var updatePath = Path.Combine(
                baselineOutputPath,
                updateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(updatePath, updateSource);
            DebugManifestWriter.Write(outputManifestPath, candidate);
            return new EditAndContinueUpdateResult(
                updateRelativePath,
                Path.GetFullPath(outputManifestPath),
                generation,
                hash);
        }

        private static void NormalizeManifest(DebugManifest candidate, DebugManifest baseline, int generation)
        {
            if (candidate.Modules.Count != baseline.Modules.Count)
            {
                throw new InvalidOperationException("The update changed the module set.");
            }
            foreach (var candidateModule in candidate.Modules)
            {
                var baselineModule = baseline.Modules.SingleOrDefault(module =>
                    string.Equals(module.AssemblyName, candidateModule.AssemblyName, StringComparison.Ordinal));
                if (baselineModule == null)
                {
                    throw new InvalidOperationException(
                        $"The update introduced module '{candidateModule.AssemblyName}'.");
                }
                candidateModule.ModuleId = baselineModule.ModuleId;
            }
            candidate.BuildId = baseline.BuildId;
            candidate.Generation = generation;
        }

        private static ManifestMethod FindMethod(DebugManifest manifest, int token)
        {
            var entryModules = manifest.Modules.Where(module => module.IsEntryModule).ToList();
            if (entryModules.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one entry module in the debug manifest, found {entryModules.Count}.");
            }
            var matches = entryModules.SelectMany(module => module.Methods
                    .Where(method => method.MethodToken == token)
                    .Select(method => new ManifestMethod(module, method)))
                .ToList();
            return matches.Count == 1
                ? matches[0]
                : throw new InvalidOperationException(
                    $"Expected one debug-manifest method for token 0x{token:x8}, found {matches.Count}.");
        }

        private static DebugManifest ReadManifest(string path) =>
            JsonConvert.DeserializeObject<DebugManifest>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Debug manifest '{path}' is empty.");

        private sealed record class ManifestMethod(DebugModule Module, DebugMethod Method);

        private sealed record StagedMethod(
            int Token,
            string ModuleId,
            string Name,
            string Path,
            string RelativePath,
            string Source);
    }

    public sealed record EditAndContinueUpdateResult(
        string UpdateModulePath,
        string DebugManifestPath,
        int Generation,
        string Hash);
}
