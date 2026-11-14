using Compiler.DTOs;
using Compiler.Debugging;
using Compiler.Helpers;
using Compiler.Performance;
using Compiler.Read;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler.Write
{
    public class Writer
    {
        private const string RuntimeAssetsDirectoryName = "runtime-assets";

        public static void WriteOutput(
            string jsOutputPath,
            CallGraphInfo callGraph,
            int? debuggerRendezvousTimeoutMilliseconds = null,
            bool bundle = true,
            JavaScriptSourceMapMode sourceMapMode = JavaScriptSourceMapMode.Managed)
        {
            if (debuggerRendezvousTimeoutMilliseconds is <= 0 or > 300_000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(debuggerRendezvousTimeoutMilliseconds),
                    "The debugger rendezvous timeout must be from 1 through 300000 milliseconds.");
            }

            CompilerLog.WriteLine(CompilerLogLevel.Normal, "Writing compiler output...");

            if (!bundle)
            {
                WriteOutputCore(
                    jsOutputPath,
                    callGraph,
                    debuggerRendezvousTimeoutMilliseconds,
                    bundle: false,
                    sourceMapRoot: jsOutputPath,
                    sourceMapMode: sourceMapMode);
                return;
            }

            var stagingDirectory = Path.Combine(
                Path.GetTempPath(),
                $"FrontEndCSharp-Output-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);
            try
            {
                WriteOutputCore(
                    stagingDirectory,
                    callGraph,
                    debuggerRendezvousTimeoutMilliseconds,
                    bundle: true,
                    sourceMapRoot: jsOutputPath,
                    sourceMapMode: sourceMapMode);
                using (CompilationPerformance.Measure("write.publish"))
                {
                    CompilerLog.WriteLine(
                        CompilerLogLevel.Normal,
                        "Publishing compiler output...");
                    PublishBundledOutput(stagingDirectory, jsOutputPath);
                }
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
            }
        }

        private static void WriteOutputCore(
            string jsOutputPath,
            CallGraphInfo callGraph,
            int? debuggerRendezvousTimeoutMilliseconds,
            bool bundle,
            string sourceMapRoot,
            JavaScriptSourceMapMode sourceMapMode)
        {
            using (CompilationPerformance.Measure("write.prepare-runtime"))
            {
                PrepareRuntimePath(jsOutputPath);
            }
            using (CompilationPerformance.Measure("write.override-packages"))
            {
                callGraph.RuntimeOverrides.ExtractJavaScriptPackages(jsOutputPath);
            }

            using (CompilationPerformance.Measure("write.dependencies"))
            {
                WriteDependencyGraph(jsOutputPath, callGraph);
            }
            using (CompilationPerformance.Measure("write.msil-package"))
            {
                WriteMsilPackage(jsOutputPath, callGraph);
            }
            DebugManifest debugManifest;
            using (CompilationPerformance.Measure("write.debug-manifest"))
            {
                debugManifest = TryWriteDebugManifest(
                    DebugManifestWriter.GetDefaultPath(callGraph.EntryPoint.Module.Assembly.Location),
                    callGraph);
            }
            if (debuggerRendezvousTimeoutMilliseconds.HasValue && debugManifest == null)
            {
                throw new InvalidOperationException(
                    "A debugger rendezvous requires matching portable or embedded PDB symbols.");
            }

            using (CompilationPerformance.Measure("write.runtime-payload"))
            {
                WriteJitEngine(jsOutputPath);
                WriteRootFolder(jsOutputPath);
                callGraph.RuntimeOverrides.ExtractRuntimeAssets(
                    Path.Combine(jsOutputPath, RuntimeAssetsDirectoryName),
                    callGraph.MethodDecoders.Select(decoder => decoder.MethodBase));
            }
            string entryPath;
            using (CompilationPerformance.Measure("write.entry-point"))
            {
                entryPath = WriteIndexJs(
                    jsOutputPath,
                    callGraph,
                    debuggerRendezvousTimeoutMilliseconds,
                    debugManifest?.BuildId,
                    bundle);
                if (debugManifest !=  null)
                {
                    WriteDebugBuildIdentity(jsOutputPath, debugManifest.BuildId);
                }
            }
            var indexPath = Path.Combine(jsOutputPath, "index.js");
            if (bundle)
            {
                using (CompilationPerformance.Measure("write.bundle"))
                {
                    CompilerLog.WriteLine(
                        CompilerLogLevel.Normal,
                        "Bundling JavaScript...");
                    JavaScriptBundler.Bundle(
                        entryPath,
                        indexPath,
                        sourceMapRoot,
                        sourceMapMode);
                    DeleteGeneratedManagedModules(jsOutputPath);
                }
            }
            else
            {
                using (CompilationPerformance.Measure("write.publish"))
                {
                    CompilerLog.WriteLine(
                        CompilerLogLevel.Normal,
                        "Publishing compiler output...");
                    File.Move(entryPath, indexPath, overwrite: true);
                    File.Delete(indexPath + ".map");
                }
            }
        }

        private static void PublishBundledOutput(string stagingDirectory, string jsOutputPath)
        {
            Directory.CreateDirectory(jsOutputPath);

            foreach (var runtimeFileName in PathLocator.GetRuntimeRootFileNames())
            {
                File.Delete(Path.Combine(jsOutputPath, runtimeFileName));
            }
            foreach (var generatedFileName in new[] { "debug-build.js", "index.entry.js" })
            {
                File.Delete(Path.Combine(jsOutputPath, generatedFileName));
            }
            foreach (var stalePackage in Directory.GetFiles(jsOutputPath, "msil.*.bin"))
            {
                File.Delete(stalePackage);
            }
            foreach (var generatedDirectoryName in new[]
            {
                "jit-engine",
                "runtime",
                RuntimeAssetsDirectoryName,
            })
            {
                var generatedDirectory = Path.Combine(jsOutputPath, generatedDirectoryName);
                if (Directory.Exists(generatedDirectory))
                {
                    Directory.Delete(generatedDirectory, recursive: true);
                }
            }

            foreach (var publicFileName in new[] { "index.js", "index.js.map", MsilBinaryPackage.FileName })
            {
                File.Copy(
                    Path.Combine(stagingDirectory, publicFileName),
                    Path.Combine(jsOutputPath, publicFileName),
                    overwrite: true);
            }
        }

        public static void WriteDebugManifest(string target, CallGraphInfo callGraph)
        {
            DebugManifestWriter.Write(target, CreateDebugManifest(callGraph));
        }

        private static DebugManifest CreateDebugManifest(CallGraphInfo callGraph)
        {
            var entryModule = callGraph.EntryPoint.Module;
            var decoders = callGraph.MethodDecoders
                .Where(decoder => decoder.MethodBody !=  null
                    && !PathLocator.HasBcl(decoder.MethodBase)
                    && !callGraph.RuntimeOverrides.HasRuntimeOverride(decoder.MethodBase))
                .GroupBy(decoder => decoder.MethodBase.Module)
                .Where(group =>
                {
                    var modulePath = PathLocator.GetModulePath(group.Key);
                    var hasSymbols = DebugManifestBuilder.HasMatchingPortablePdb(modulePath);
                    if (!hasSymbols && group.Key != entryModule)
                    {
                        Console.Error.WriteLine(
                            $"Warning: debug symbols for module '{modulePath}' were not included because a matching Portable PDB was not found.");
                    }
                    return hasSymbols;
                })
                .SelectMany(group => group)
                .ToList();
            if (decoders.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No interpreted methods were found for debug module '{entryModule.Name}'.");
            }
            if (!decoders.Any(decoder => decoder.MethodBase.Module == entryModule))
            {
                throw new InvalidDataException(
                    $"A matching Portable PDB is required for debug module '{PathLocator.GetModulePath(entryModule)}'.");
            }

            return DebugManifestBuilder.Build(decoders, entryModule);
        }

        private static DebugManifest TryWriteDebugManifest(string target, CallGraphInfo callGraph)
        {
            var modulePath = PathLocator.GetModulePath(callGraph.EntryPoint.Module);
            if (!DebugManifestBuilder.HasMatchingPortablePdb(modulePath))
            {
                Console.Error.WriteLine(
                    $"Warning: debug manifest was not emitted because a matching Portable PDB was not found for '{modulePath}'.");
                return null;
            }

            var manifest = CreateDebugManifest(callGraph);
            DebugManifestWriter.Write(target, manifest);
            return manifest;
        }

        private static void WriteDebugBuildIdentity(string jsOutputPath, string buildId)
        {
            var serializedBuildId = JsonConvert.SerializeObject(buildId);
            File.WriteAllText(
                Path.Combine(jsOutputPath, "debug-build.js"),
                $@"export const buildId = {serializedBuildId};
const existingBuildIdentity = globalThis.__FrontEndCSharpBuildIdentity;
if (existingBuildIdentity === undefined) {{
    Object.defineProperty(globalThis, ""__FrontEndCSharpBuildIdentity"", {{
        value: Object.freeze({{ buildId }}),
        writable: false,
        configurable: false,
        enumerable: false
    }});
}} else if (existingBuildIdentity?.buildId !== buildId) {{
    throw new Error(""The page already contains a different FrontEndCSharp build identity."");
}}
");
        }

        static void PrepareRuntimePath(string jsOutputPath)
        {
            Directory.CreateDirectory(jsOutputPath);
            var runtimeAssetsDirectory = Path.Combine(jsOutputPath, RuntimeAssetsDirectoryName);
            if (Directory.Exists(runtimeAssetsDirectory))
            {
                Directory.Delete(runtimeAssetsDirectory, recursive: true);
            }
            var runtimeFolder = GetRuntimePath(jsOutputPath);
            if (!Directory.Exists(runtimeFolder))
            {
                Directory.CreateDirectory(runtimeFolder);
            }
            else
            {
                foreach (var pattern in new[] { "method.*.js", "type.*.js" })
                {
                    foreach (var generatedModule in Directory.GetFiles(runtimeFolder, pattern))
                    {
                        File.Delete(generatedModule);
                    }
                }
            }
            foreach (var stalePackage in Directory.GetFiles(jsOutputPath, "msil.*.bin"))
            {
                File.Delete(stalePackage);
            }
            File.Delete(Path.Combine(jsOutputPath, MsilBinaryPackage.FileName));
        }

        static string GetRuntimePath(string jsOutputPath) => Path.Combine(jsOutputPath, "runtime");

        static void WriteMsilPackage(string jsOutputPath, CallGraphInfo callGraph)
        {
            MsilBinaryPackage.Write(
                Path.Combine(jsOutputPath, MsilBinaryPackage.FileName),
                callGraph);
        }

        static void WriteJitEngine(string jsOutputPath)
        {
            PathLocator.CopyFolder(jsOutputPath, "jit-engine");
        }

        static void WriteRootFolder(string jsOutputPath)
        {
            PathLocator.CopyFolder(jsOutputPath, ".");
        }

        static string WriteIndexJs(
            string jsOutputPath,
            CallGraphInfo callGraph,
            int? debuggerRendezvousTimeoutMilliseconds,
            string debugBuildId,
            bool bundle)
        {
            var entryPath = Path.Combine(jsOutputPath, "index.entry.js");
            var entrypoint = callGraph.EntryPoint;
            var asyncEntrypoint = GetAsyncEntrypoint(callGraph);
            var intrinsicImports = string.Join(
                Environment.NewLine,
                callGraph.MethodDecoders
                    .Where(decoder => PathLocator.HasBcl(decoder.MethodBase)
                        || callGraph.RuntimeOverrides.HasRuntimeOverride(decoder.MethodBase))
                    .Select(decoder => NameNormalizers.GetMethodModuleFileName(decoder.MethodBase))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(fileName => fileName, StringComparer.Ordinal)
                    .Select(fileName => $@"import ""./runtime/{fileName}"";"));
            var debugBuildImport = debugBuildId !=  null
                ? @"import { buildId as debuggerBuildId } from ""./debug-build.js"";"
                : string.Empty;
            var debuggerRendezvous = debuggerRendezvousTimeoutMilliseconds.HasValue
                ? $@"import {{ waitForDebuggerConfiguration }} from ""./managed-debugger.js"";

await waitForDebuggerConfiguration(debuggerBuildId, {debuggerRendezvousTimeoutMilliseconds.Value});
"
                : string.Empty;
            var msilPackageHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
                    Path.Combine(jsOutputPath, MsilBinaryPackage.FileName))))
                .ToLowerInvariant();
            var msilPackageBaseUrl = bundle ? "__frontEndCSharpBundleUrl" : "import.meta.url";
            var msilPackageLoader = $@"import {{ loadMsilPackage }} from ""./msil-binary.js"";

await loadMsilPackage(new URL(""./{MsilBinaryPackage.FileName}?v={msilPackageHash}"", {msilPackageBaseUrl}));";
            if (asyncEntrypoint !=  null)
            {
                var asyncEntrypointArguments = GetAsyncEntrypointArguments(asyncEntrypoint);
                File.WriteAllText(entryPath, $@"
// Generated asynchronous entry point
import ""./bootstrap-msil.js"";
import {{ unwrap, wrap }} from ""./msil-type-helpers.js"";
import {{ waitForTask }} from ""./task-runtime.js"";
{msilPackageLoader}
{intrinsicImports}
{debugBuildImport}
{debuggerRendezvous}

const entrypointTask = DotNet.Methods[""{NameNormalizers.GetFullyQualifiedMethodName(asyncEntrypoint)}""]({{ stackTrace: """" }}{asyncEntrypointArguments});
await waitForTask(unwrap(entrypointTask));

");
                return entryPath;
            }

            File.WriteAllText(entryPath, $@"
// Generated entry point
import ""./bootstrap-msil.js"";
{msilPackageLoader}
{intrinsicImports}
{debugBuildImport}
{debuggerRendezvous}

DotNet.Methods[""{NameNormalizers.GetFullyQualifiedMethodName(entrypoint)}""]({{ stackTrace: """" }}, null) /* TODO: fix th is null hack */

");
            return entryPath;
        }

        static MethodInfo GetAsyncEntrypoint(CallGraphInfo callGraph)
        {
            if (callGraph.EntryPoint.Name != "<Main>" || callGraph.EntryPoint.ReturnType != typeof(void))
            {
                return null;
            }

            var entrypointDecoder = callGraph.MethodDecoders
                .Single(decoder => decoder.MethodBase == callGraph.EntryPoint);
            var entrypointParameterTypes = callGraph.EntryPoint
                .GetParameters()
                .Select(parameter => parameter.ParameterType);
            var taskReturningMethods = entrypointDecoder.ReferencedMethods.Keys
                .OfType<MethodInfo>()
                .Where(method => method.DeclaringType == callGraph.EntryPoint.DeclaringType
                    && typeof(Task).IsAssignableFrom(method.ReturnType)
                    && method.GetCustomAttribute<AsyncStateMachineAttribute>() !=  null
                    && method.GetParameters()
                        .Select(parameter => parameter.ParameterType)
                        .SequenceEqual(entrypointParameterTypes))
                .ToList();

            if (taskReturningMethods.Count == 0)
            {
                return null;
            }

            if (taskReturningMethods.Count != 1)
            {
                throw new NotSupportedException(
                    $"The entry point references {taskReturningMethods.Count} Task-returning methods; the asynchronous entry point is ambiguous.");
            }

            return taskReturningMethods[0];
        }

        static string GetAsyncEntrypointArguments(MethodInfo asyncEntrypoint)
        {
            var parameters = asyncEntrypoint.GetParameters();
            if (parameters.Length == 0)
            {
                return string.Empty;
            }

            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
            {
                return ", wrap([], \"System.String[]\")";
            }

            throw new NotSupportedException(
                $"The asynchronous entry point '{asyncEntrypoint}' has an unsupported parameter signature.");
        }

        public static void CopyIntrinsic(
            string target,
            MethodDecoder methodDecoder,
            CallGraphInfo callGraph)
        {
            var method = methodDecoder.MethodBase;
            if (callGraph.RuntimeOverrides.HasRuntimeOverride(method))
            {
                callGraph.RuntimeOverrides.CopyRuntimeOverride(method, target);
                RemoveManagedModuleImports(target);
            }
            else if (PathLocator.HasBcl(method))
            {
                PathLocator.CopyBcl(method, target);
                RemoveManagedModuleImports(target);
            }
            else
            {
                throw new NotSupportedException();
            }
            return;
        }

        private static void RemoveManagedModuleImports(string target)
        {
            var source = File.ReadAllText(target);
            source = Regex.Replace(
                source,
                "^\\s*import \\\"\\./(?:method|type)\\.[^\\\"]+\\\";\\s*(?:\\r?\\n)?",
                string.Empty,
                RegexOptions.Multiline | RegexOptions.CultureInvariant);

            File.WriteAllText(target, source);
        }

        public static void WriteDependencyGraph(string jsOutputPath, CallGraphInfo callGraph)
        {
            var runtimeFolder = GetRuntimePath(jsOutputPath);
            foreach (var methodDecoder in callGraph.MethodDecoders)
            {
                var hasRuntimeOverride = callGraph.RuntimeOverrides.HasRuntimeOverride(methodDecoder.MethodBase);
                var outputFileName = NameNormalizers.GetMethodModuleFileName(methodDecoder.MethodBase);
                var target = Path.Combine(runtimeFolder, outputFileName);
                if (PathLocator.HasBcl(methodDecoder.MethodBase) || hasRuntimeOverride)
                {
                    CopyIntrinsic(target, methodDecoder, callGraph);
                }
            }
        }

        private static void DeleteGeneratedManagedModules(string jsOutputPath)
        {
            var runtimeFolder = GetRuntimePath(jsOutputPath);
            foreach (var pattern in new[] { "method.*.js", "type.*.js" })
            {
                foreach (var generatedModule in Directory.GetFiles(runtimeFolder, pattern))
                {
                    File.Delete(generatedModule);
                }
            }
        }
    }
}
