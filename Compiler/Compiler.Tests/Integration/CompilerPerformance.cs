using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FrontEndCSharp.Compiler.Integration;

internal static class CompilerPerformance
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CompilationTimeout = TimeSpan.FromMinutes(10);

    public static void Run(string[] args)
    {
        var options = Options.Parse(args);
        var repositoryRoot = RepositoryLayout.Root;
        var workspace = options.Workspace == null
            ? Path.Combine(repositoryRoot, "Compiler", "Compiler.Tests", "Performance")
            : Path.GetFullPath(options.Workspace);
        var fixtureRoot = Path.Combine(workspace, "fixture");
        var outputRoot = Path.Combine(workspace, "output");
        var reportRoot = Path.Combine(workspace, "reports");
        Directory.CreateDirectory(fixtureRoot);
        Directory.CreateDirectory(outputRoot);
        Directory.CreateDirectory(reportRoot);

        if (options.Fixture == FixtureKind.Generated)
        {
            WriteFixture(repositoryRoot, fixtureRoot, options.PageCount);
        }
        var fixtureProject = options.Fixture == FixtureKind.Generated
            ? Path.Combine(fixtureRoot, "CompilerPerformanceFixture.csproj")
            : Path.Combine(
                repositoryRoot,
                "Demo",
                "FrontEndCSharp.ReactDemo",
                "FrontEndCSharp.ReactDemo.csproj");
        var fixtureAssembly = options.Fixture == FixtureKind.Generated
            ? Path.Combine(
                fixtureRoot,
                "bin",
                "Release",
                "net8.0",
                "CompilerPerformanceFixture.dll")
            : Path.Combine(
                repositoryRoot,
                "Demo",
                "FrontEndCSharp.ReactDemo",
                "bin",
                "Release",
                "net8.0",
                "FrontEndCSharp.ReactDemo.dll");
        var compilerProject = Path.Combine(repositoryRoot, "Compiler", "Core", "Compiler.csproj");
        var compilerAssembly = options.CompilerPath == null
            ? Path.Combine(
                repositoryRoot,
                "Compiler",
                "Core",
                "bin",
                "Release",
                "net8.0",
                "Compiler.dll")
            : Path.GetFullPath(options.CompilerPath);

        var compilerBuildMilliseconds = 0d;
        if (options.CompilerPath == null)
        {
            var compilerBuildStopwatch = Stopwatch.StartNew();
            RunDotNet(repositoryRoot, "build", compilerProject, "--configuration", "Release", "--nologo");
            compilerBuildStopwatch.Stop();
            compilerBuildMilliseconds = compilerBuildStopwatch.Elapsed.TotalMilliseconds;
        }
        else if (!File.Exists(compilerAssembly))
        {
            throw new FileNotFoundException("The requested benchmark compiler does not exist.", compilerAssembly);
        }
        var fixtureBuildStopwatch = Stopwatch.StartNew();
        RunDotNet(repositoryRoot, "build", fixtureProject, "--configuration", "Release", "--nologo");
        fixtureBuildStopwatch.Stop();

        Console.Error.WriteLine(
            $"Benchmarking {options.FixtureDisplayName} ({(options.Bundle ? "bundled" : "no bundle")}): "
            + $"one warmup and {options.SampleCount} measured compilations.");
        _ = Compile(
            repositoryRoot,
            compilerAssembly,
            fixtureProject,
            fixtureAssembly,
            outputRoot,
            Path.Combine(reportRoot, options.Label + "-warmup.json"),
            options.Bundle);

        var samples = new List<SampleResult>();
        for (var sampleIndex = 0; sampleIndex < options.SampleCount; sampleIndex++)
        {
            var reportPath = Path.Combine(
                reportRoot,
                $"{options.Label}-{sampleIndex + 1}.json");
            var sample = Compile(
                repositoryRoot,
                compilerAssembly,
                fixtureProject,
                fixtureAssembly,
                outputRoot,
                reportPath,
                options.Bundle);
            samples.Add(sample);
            Console.Error.WriteLine(
                $"Sample {sampleIndex + 1}/{options.SampleCount}: {sample.TotalMilliseconds:0.0} ms, "
                + $"{sample.Metrics["selected-methods"]} methods.");
        }

        ValidateStableOutput(samples, options.Bundle);
        var summary = CreateSummary(
            options,
            samples,
            compilerBuildMilliseconds,
            fixtureBuildStopwatch.Elapsed.TotalMilliseconds);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var summaryJson = JsonSerializer.Serialize(summary, jsonOptions);
        File.WriteAllText(
            Path.Combine(reportRoot, "summary-" + options.Label + ".json"),
            summaryJson + Environment.NewLine);
        Console.WriteLine(summaryJson);
    }

    private static SampleResult Compile(
        string repositoryRoot,
        string compilerAssembly,
        string fixtureProject,
        string fixtureAssembly,
        string outputRoot,
        string reportPath,
        bool bundle)
    {
        var sampleOutput = Path.Combine(
            outputRoot,
            Path.GetFileNameWithoutExtension(reportPath));
        if (Directory.Exists(sampleOutput))
        {
            Directory.Delete(sampleOutput, recursive: true);
        }
        var stopwatch = Stopwatch.StartNew();
        var arguments = new List<string> { compilerAssembly };
        if (!bundle)
        {
            arguments.Add("--no-bundle");
        }
        arguments.Add("--performance-report");
        arguments.Add(reportPath);
        arguments.Add(fixtureProject);
        arguments.Add(sampleOutput);
        arguments.Add(fixtureAssembly);
        BoundedProcess.Run(
            "dotnet",
            repositoryRoot,
            CompilationTimeout,
            arguments.ToArray());
        stopwatch.Stop();

        var report = JsonSerializer.Deserialize<PerformanceReport>(File.ReadAllText(reportPath))
            ?? throw new InvalidDataException("The compiler performance report was empty.");
        Require(report.FormatVersion == 1, "The compiler performance report format changed.");
        Require(report.Phases.Count > 0, "The compiler performance report contained no phases.");
        return new SampleResult
        {
            TotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            Phases = report.Phases.ToDictionary(phase => phase.Name, StringComparer.Ordinal),
            Metrics = report.Metrics,
            IndexHash = Hash(Path.Combine(sampleOutput, "index.js")),
            MsilHash = Hash(Path.Combine(sampleOutput, "msil.bin")),
        };
    }

    private static object CreateSummary(
        Options options,
        IReadOnlyList<SampleResult> samples,
        double compilerBuildMilliseconds,
        double fixtureBuildMilliseconds)
    {
        var phaseNames = samples
            .SelectMany(sample => sample.Phases.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var phaseSummaries = phaseNames.Select(name => new
        {
            Name = name,
            MedianMilliseconds = Median(samples.Select(sample => sample.Phases[name].ElapsedMilliseconds)),
            MedianAllocatedBytes = (long)Median(samples.Select(sample => (double)sample.Phases[name].AllocatedBytes)),
            InvocationCount = samples[0].Phases[name].InvocationCount,
        }).ToArray();
        var indexHashStable = samples
            .Select(sample => sample.IndexHash)
            .Distinct(StringComparer.Ordinal)
            .Count() == 1;
        return new
        {
            FormatVersion = 1,
            options.Label,
            Fixture = options.FixtureDisplayName,
            PageCount = options.Fixture == FixtureKind.Generated ? options.PageCount : (int?)null,
            options.Bundle,
            options.SampleCount,
            Compiler = options.CompilerPath == null
                ? "repository Release build"
                : Path.GetFullPath(options.CompilerPath),
            CompilerBuildMilliseconds = compilerBuildMilliseconds,
            FixtureBuildMilliseconds = fixtureBuildMilliseconds,
            Runtime = Environment.Version.ToString(),
            OperatingSystem = Environment.OSVersion.ToString(),
            Environment.ProcessorCount,
            MedianTotalMilliseconds = Median(samples.Select(sample => sample.TotalMilliseconds)),
            Metrics = samples[0].Metrics,
            IndexSha256Stable = indexHashStable,
            IndexSha256 = indexHashStable ? samples[0].IndexHash : null,
            MsilSha256 = samples[0].MsilHash,
            Phases = phaseSummaries,
        };
    }

    private static void ValidateStableOutput(IReadOnlyList<SampleResult> samples, bool bundle)
    {
        var first = samples[0];
        foreach (var sample in samples.Skip(1))
        {
            Require(
                sample.Metrics.OrderBy(pair => pair.Key).SequenceEqual(first.Metrics.OrderBy(pair => pair.Key)),
                "Compiler graph metrics changed between benchmark samples.");
            if (!bundle)
            {
                Require(sample.IndexHash == first.IndexHash, "index.js changed between benchmark samples.");
            }
            Require(sample.MsilHash == first.MsilHash, "msil.bin changed between benchmark samples.");
            Require(
                sample.Phases.Keys.OrderBy(value => value).SequenceEqual(first.Phases.Keys.OrderBy(value => value)),
                "Compiler phase names changed between benchmark samples.");
        }
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        return ordered.Length % 2 == 0
            ? (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2
            : ordered[ordered.Length / 2];
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void WriteFixture(string repositoryRoot, string fixtureRoot, int pageCount)
    {
        static string ProjectReference(string path) =>
            $"    <ProjectReference Include=\"{SecurityElement.Escape(path)}\" />";

        var projectReferences = new[]
        {
            Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.Browser", "FrontEndCSharp.Overrides.Interop.JS.Browser", "FrontEndCSharp.Overrides.Interop.JS.Browser.csproj"),
            Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.Browser", "FrontEndCSharp.Overrides.Interop.JS.Browser.fecs", "FrontEndCSharp.Overrides.Interop.JS.Browser.fecs.csproj"),
            Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.React", "FrontEndCSharp.Overrides.Interop.JS.React", "FrontEndCSharp.Overrides.Interop.JS.React.csproj"),
            Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.React", "FrontEndCSharp.Overrides.Interop.JS.React.fecs", "FrontEndCSharp.Overrides.Interop.JS.React.fecs.csproj"),
            Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.System.Console", "FrontEndCSharp.Overrides.System.Console.fecs", "FrontEndCSharp.Overrides.System.Console.fecs.csproj"),
            Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.System.Private.CoreLib", "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs", "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.csproj"),
        };
        var project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <DebugType>portable</DebugType>
              </PropertyGroup>
              <ItemGroup>
            """
            + Environment.NewLine
            + string.Join(Environment.NewLine, projectReferences.Select(ProjectReference))
            + Environment.NewLine
            + "  </ItemGroup>" + Environment.NewLine
            + "</Project>" + Environment.NewLine;
        File.WriteAllText(Path.Combine(fixtureRoot, "CompilerPerformanceFixture.csproj"), project);

        var source = new StringBuilder();
        source.AppendLine("using Interop.JS.Browser;");
        source.AppendLine("using Interop.JS.React;");
        source.AppendLine("namespace CompilerPerformanceFixture;");
        source.AppendLine("public interface IRoutedPage { string Route { get; } int Score(int seed); ReactNode Render(); }");
        source.AppendLine("public abstract class RoutedPage : IRoutedPage");
        source.AppendLine("{");
        source.AppendLine("    public abstract string Route { get; }");
        source.AppendLine("    public abstract int Score(int seed);");
        source.AppendLine("    public abstract ReactNode Render();");
        source.AppendLine("    protected int Normalize(int value) => Math.Abs(value % 10007);");
        source.AppendLine("    protected int Finish(int value) => Normalize((value * 31) ^ 17);");
        source.AppendLine("}");
        for (var index = 0; index < pageCount; index++)
        {
            var name = index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            source.AppendLine($"public sealed class Page{name} : RoutedPage");
            source.AppendLine("{");
            source.AppendLine($"    public override string Route => \"/page-{name}\";");
            source.AppendLine($"    public override int Score(int seed) => Finish(Step7(seed + {index + 1}));");
            for (var step = 0; step < 8; step++)
            {
                var expression = step == 0
                    ? $"(value * {index % 17 + 3}) ^ {index * 13 + 7}"
                    : $"Step{step - 1}(value) + {index + step}";
                source.AppendLine($"    private int Step{step}(int value) => {expression};");
            }
            source.AppendLine("    public override ReactNode Render()");
            source.AppendLine("    {");
            source.AppendLine($"        var props = ReactProps.Create().SetString(\"data-route\", \"/page-{name}\");");
            source.AppendLine($"        return React.CreateElement(\"section\", props, React.Text(\"Page {name}\"));");
            source.AppendLine("    }");
            source.AppendLine("}");
        }
        source.AppendLine("public static class Program");
        source.AppendLine("{");
        source.AppendLine("    public static void Main()");
        source.AppendLine("    {");
        source.AppendLine("        IRoutedPage[] pages =");
        source.AppendLine("        [");
        for (var index = 0; index < pageCount; index++)
        {
            source.AppendLine($"            new Page{index:D3}(),");
        }
        source.AppendLine("        ];");
        source.AppendLine("        var nodes = new ReactNode[pages.Length];");
        source.AppendLine("        var checksum = 0;");
        source.AppendLine("        for (var index = 0; index < pages.Length; index++)");
        source.AppendLine("        {");
        source.AppendLine("            checksum += pages[index].Score(index);");
        source.AppendLine("            nodes[index] = pages[index].Render();");
        source.AppendLine("        }");
        source.AppendLine("        var host = Document.GetElementById(\"app\") ?? throw new InvalidOperationException(\"Missing app host.\");");
        source.AppendLine("        host.SetAttribute(\"data-checksum\", checksum.ToString());");
        source.AppendLine("        ReactRoot.Create(host).Render(React.CreateElement(\"main\", null, nodes));");
        source.AppendLine("    }");
        source.AppendLine("}");
        File.WriteAllText(Path.Combine(fixtureRoot, "Program.cs"), source.ToString());
    }

    private static void RunDotNet(string workingDirectory, params string[] arguments) =>
        BoundedProcess.Run("dotnet", workingDirectory, BuildTimeout, arguments);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private sealed class PerformanceReport
    {
        public int FormatVersion { get; init; }
        public List<PhaseMeasurement> Phases { get; init; } = [];
        public Dictionary<string, long> Metrics { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class PhaseMeasurement
    {
        public string Name { get; init; } = string.Empty;
        public int InvocationCount { get; init; }
        public double ElapsedMilliseconds { get; init; }
        public long AllocatedBytes { get; init; }
    }

    private sealed class SampleResult
    {
        public double TotalMilliseconds { get; init; }
        public required Dictionary<string, PhaseMeasurement> Phases { get; init; }
        public required Dictionary<string, long> Metrics { get; init; }
        public required string IndexHash { get; init; }
        public required string MsilHash { get; init; }
    }

    private sealed class Options
    {
        public string Label { get; private init; } = "run";
        public int PageCount { get; private init; } = 256;
        public int SampleCount { get; private init; } = 5;
        public string? Workspace { get; private init; }
        public string? CompilerPath { get; private init; }
        public FixtureKind Fixture { get; private init; } = FixtureKind.Generated;
        public bool Bundle { get; private init; }
        public string FixtureDisplayName => Fixture == FixtureKind.Generated
            ? $"{PageCount} routed React pages"
            : "FrontEndCSharp.ReactDemo";

        public static Options Parse(string[] args)
        {
            var label = "run";
            var pageCount = 256;
            var sampleCount = 5;
            string? workspace = null;
            string? compilerPath = null;
            var fixture = FixtureKind.Generated;
            var bundle = false;
            for (var index = 0; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"Option '{args[index]}' requires a value.");
                }
                switch (args[index])
                {
                    case "--label":
                        label = args[index + 1];
                        break;
                    case "--pages":
                        pageCount = PositiveInteger(args[index], args[index + 1]);
                        break;
                    case "--samples":
                        sampleCount = PositiveInteger(args[index], args[index + 1]);
                        break;
                    case "--workspace":
                        workspace = args[index + 1];
                        break;
                    case "--compiler":
                        compilerPath = args[index + 1];
                        break;
                    case "--fixture":
                        fixture = args[index + 1] switch
                        {
                            "generated" => FixtureKind.Generated,
                            "react-demo" => FixtureKind.ReactDemo,
                            _ => throw new ArgumentException(
                                "--fixture must be 'generated' or 'react-demo'."),
                        };
                        break;
                    case "--bundle":
                        bundle = bool.TryParse(args[index + 1], out var parsedBundle)
                            ? parsedBundle
                            : throw new ArgumentException("--bundle must be 'true' or 'false'.");
                        break;
                    default:
                        throw new ArgumentException($"Unknown compiler-performance option '{args[index]}'.");
                }
            }
            if (string.IsNullOrWhiteSpace(label)
                || label is "." or ".."
                || label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || Path.GetFileName(label) != label)
            {
                throw new ArgumentException("--label must be a valid file-name segment.");
            }
            return new Options
            {
                Label = label,
                PageCount = pageCount,
                SampleCount = sampleCount,
                Workspace = workspace,
                CompilerPath = compilerPath,
                Fixture = fixture,
                Bundle = bundle,
            };
        }

        private static int PositiveInteger(string option, string value) =>
            int.TryParse(value, out var parsed) && parsed > 0
                ? parsed
                : throw new ArgumentException($"{option} must be a positive integer.");
    }

    private enum FixtureKind
    {
        Generated,
        ReactDemo,
    }
}
