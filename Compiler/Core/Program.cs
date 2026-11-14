using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using Compiler.Read;
using Compiler.Write;
using Compiler.Helpers;
using Compiler.Debugging;
using Compiler.Performance;

namespace Compiler;

public class Program
{
    public static int Main(string[] args)
    {
        var originalLogLevel = CompilerLog.Level;
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
        finally
        {
            CompilerLog.Level = originalLogLevel;
        }
    }

    static int Run(string[] args)
    {
        CompilerLog.Level = CompilerLogLevel.Normal;

        if (args.Length > 0 && args[0] == "--generate-runtime-override-manifest")
        {
            if (args.Length != 3)
            {
                throw new ArgumentException(
                    "Usage: Compiler --generate-runtime-override-manifest <resource-directory> <output-path>");
            }
            RuntimeOverrideManifestGenerator.Generate(args[1], args[2]);
            return 0;
        }

        if (args.Length > 0 && args[0] == "--generate-runtime-override-providers")
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("Usage: Compiler --generate-runtime-override-providers <resource-directory> <output-path>");
            }
            RuntimeOverrideProviderGenerator.Generate(args[1], args[2]);
            return 0;
        }

        if (args.Length > 0 && args[0] == "--create-debug-update")
        {
            if (args.Length != 8)
            {
                throw new ArgumentException(
                    "Usage: Compiler --create-debug-update <baseline-manifest> <candidate-manifest> <baseline-output> <candidate-output> <generation> <method-tokens> <output-manifest>");
            }
            var tokens = args[6].Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            var result = EditAndContinueUpdateBuilder.Build(
                args[1],
                args[2],
                args[3],
                args[4],
                int.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture),
                tokens,
                args[7]);
            Console.WriteLine(JsonConvert.SerializeObject(result));
            return 0;
        }

        int? debuggerRendezvousTimeoutMilliseconds = null;
        string performanceReportPath = null;
        var bundle = true;
        var sourceMapMode = JavaScriptSourceMapMode.Managed;
        while (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
        {
            if (args[0] == "--no-bundle")
            {
                bundle = false;
                args = args.Skip(1).ToArray();
                continue;
            }
            if (args[0] == "--debugger-rendezvous-timeout-ms")
            {
                if (args.Length < 2
                    || !int.TryParse(
                        args[1],
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var parsedTimeout)
                    || parsedTimeout <= 0
                    || parsedTimeout > 300_000)
                {
                    throw new ArgumentException(
                        "--debugger-rendezvous-timeout-ms must be an integer from 1 through 300000.");
                }
                debuggerRendezvousTimeoutMilliseconds = parsedTimeout;
                args = args.Skip(2).ToArray();
                continue;
            }
            if (args[0] == "--performance-report")
            {
                if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                {
                    throw new ArgumentException("--performance-report requires an output path.");
                }
                performanceReportPath = args[1];
                args = args.Skip(2).ToArray();
                continue;
            }
            if (args[0] == "--source-map-mode")
            {
                if (args.Length < 2)
                {
                    throw new ArgumentException(
                        "--source-map-mode requires 'managed' or 'full'.");
                }
                sourceMapMode = args[1].ToLowerInvariant() switch
                {
                    "managed" => JavaScriptSourceMapMode.Managed,
                    "full" => JavaScriptSourceMapMode.Full,
                    _ => throw new ArgumentException(
                        "--source-map-mode requires 'managed' or 'full'."),
                };
                args = args.Skip(2).ToArray();
                continue;
            }
            if (args[0] == "--log-level")
            {
                if (args.Length < 2)
                {
                    throw new ArgumentException("--log-level requires 'normal' or 'verbose'.");
                }
                CompilerLog.Level = args[1].ToLowerInvariant() switch
                {
                    "normal" => CompilerLogLevel.Normal,
                    "verbose" => CompilerLogLevel.Verbose,
                    _ => throw new ArgumentException("--log-level requires 'normal' or 'verbose'."),
                };
                args = args.Skip(2).ToArray();
                continue;
            }

            throw new ArgumentException($"Unknown compiler option '{args[0]}'.");
        }

        using var performance = CompilationPerformance.Start(performanceReportPath);
        var paths = CompilationPerformance.Measure("resolve-inputs", () => ResolvePaths(args));

        var callGraph = CompilationPerformance.Measure(
            "read-call-graph",
            () => Reader.GetCallGraph(paths.BinaryInputPath));
        CompilationPerformance.SetMetric("selected-methods", callGraph.MethodDecoders.Count);
        CompilationPerformance.SetMetric("referenced-types", callGraph.AllReferencedFields.Count);

        using (CompilationPerformance.Measure("write-output"))
        {
            Writer.WriteOutput(
                paths.JavaScriptOutputPath,
                callGraph,
                debuggerRendezvousTimeoutMilliseconds,
                bundle,
                sourceMapMode);
        }

        return 0;
    }


    public static ResolvedCompilerPaths ResolvePaths(string[] args)
    {
        Console.WriteLine("FrontEndCSharp Compiler");
        Console.WriteLine("======================");

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: Compiler [--no-bundle] [--debugger-rendezvous-timeout-ms <milliseconds>] [--performance-report <path>] [--source-map-mode <managed|full>] [--log-level <normal|verbose>] <project-path> [output-path] [bin-input]");
            Console.WriteLine("Example: Compiler c:\\path\\to\\project.csproj c:\\output\\js");
            throw new Exception();
        }

        var projectPath = Path.GetFullPath(args[0]);
        if (!File.Exists(projectPath))
        {
            Console.WriteLine($"Error: Project file not found: {projectPath}");
            throw new Exception();
        }

        // Determine output path
        var jsOutputPath = args.Length > 1
            ? args[1]
            : Path.Combine(Path.GetDirectoryName(projectPath)!, "wwwroot", "js");
        var binInput = args.Length > 2
            ? args[2]
            : null;
        if (binInput == null)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var debugPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", "Debug");
            var dotNetVersions = Directory.GetDirectories(debugPath);
            var dotNetDebugPath = dotNetVersions[0];
            var exeFile = Directory.GetFiles(dotNetDebugPath, $"{projectName}.dll").FirstOrDefault();
            binInput = exeFile;
        }
        if (binInput == null)
        {
            Console.WriteLine($"Error: Unable to determine bin path");
            throw new Exception();
        }

        jsOutputPath = Path.GetFullPath(jsOutputPath);
        Console.WriteLine($"Output path: {jsOutputPath}");

        return new ResolvedCompilerPaths(jsOutputPath, binInput);
    }

}

public sealed record class ResolvedCompilerPaths(
    string JavaScriptOutputPath,
    string BinaryInputPath);
