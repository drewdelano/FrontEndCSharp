namespace Compiler.Tests;

using Compiler.Performance;
using Newtonsoft.Json.Linq;

public class ProgramTests
{
    [Test]
    public void DefaultCompilerLogShowsPhasesWithoutPerMethodProgress()
    {
        var output = RunCompiler(verbose: false);

        Assert.Contains("Reading call graph...", output, StringComparison.Ordinal);
        Assert.Contains("Analyzing possible runtime types...", output, StringComparison.Ordinal);
        Assert.Contains("Writing compiler output...", output, StringComparison.Ordinal);
        Assert.Contains("Publishing compiler output...", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Working on ", output, StringComparison.Ordinal);
    }

    [Test]
    public void VerboseCompilerLogShowsPerMethodProgress()
    {
        var output = RunCompiler(verbose: true);

        Assert.Contains("Reading call graph...", output, StringComparison.Ordinal);
        Assert.Contains("Working on ", output, StringComparison.Ordinal);
    }

    [Test]
    public void PerformanceReportRecordsAggregatedPhaseAndMetric()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"FrontEndCSharp_ProgramTests_{Guid.NewGuid():N}");
        var reportPath = Path.Combine(testRoot, "performance.json");

        try
        {
            using (var performance = CompilationPerformance.Start(reportPath))
            {
                using (CompilationPerformance.Measure("fixture-phase"))
                {
                    _ = new byte[1024 * 1024];
                }
                using (CompilationPerformance.Measure("fixture-phase"))
                {
                    _ = new byte[2 * 1024 * 1024];
                }
                CompilationPerformance.SetMetric("selected-methods", 42);
            }

            var report = JObject.Parse(File.ReadAllText(reportPath));
            Assert.Equal(1, report.Value<int>("FormatVersion"));
            Assert.Equal(42, report["Metrics"]!.Value<int>("selected-methods"));
            var phase = Assert.Single(report["Phases"]!);
            Assert.Equal("fixture-phase", phase.Value<string>("Name"));
            Assert.Equal(2, phase.Value<int>("InvocationCount"));
            Assert.True(phase.Value<double>("ElapsedMilliseconds") >= 0);
            Assert.True(phase.Value<long>("AllocatedBytes") >= 2 * 1024 * 1024);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Test]
    public void ResolvePathsReturnsAbsolutePathsForRelativeProjectInput()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"FrontEndCSharp_ProgramTests_{Guid.NewGuid():N}");
        var projectPath = Path.Combine(testRoot, "RelativeProject.csproj");
        var assemblyDirectory = Path.Combine(testRoot, "bin", "Debug", "net8.0");
        var assemblyPath = Path.Combine(assemblyDirectory, "RelativeProject.dll");

        Directory.CreateDirectory(assemblyDirectory);
        File.WriteAllText(projectPath, "<Project />");
        File.WriteAllBytes(assemblyPath, []);

        try
        {
            var relativeProjectPath = Path.GetRelativePath(Environment.CurrentDirectory, projectPath);

            var paths = Compiler.Program.ResolvePaths([relativeProjectPath]);

            Assert.Equal(Path.GetFullPath(assemblyPath), paths.BinaryInputPath);
            Assert.Equal(Path.Combine(testRoot, "wwwroot", "js"), paths.JavaScriptOutputPath);
            Assert.True(Path.IsPathFullyQualified(paths.BinaryInputPath));
            Assert.True(Path.IsPathFullyQualified(paths.JavaScriptOutputPath));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Test]
    public void CompilerRejectsUnknownSourceMapMode()
    {
        var originalError = Console.Error;
        var capturedError = new StringWriter();
        try
        {
            Console.SetError(capturedError);

            Assert.Equal(1, Compiler.Program.Main(["--source-map-mode", "everything"]));
            Assert.Contains(
                "--source-map-mode requires 'managed' or 'full'.",
                capturedError.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    private static string RunCompiler(bool verbose)
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(
            repositoryRoot,
            "Compiler",
            "Compiler.Tests",
            "Fixtures",
            "PortablePdbFixture",
            "PortablePdbFixture.csproj");
        var assemblyPath = typeof(PortablePdbFixture.PortableFixture).Assembly.Location;
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"FrontEndCSharp_ProgramLog_{Guid.NewGuid():N}");
        var arguments = new List<string>();
        if (verbose)
        {
            arguments.Add("--log-level");
            arguments.Add("verbose");
        }
        arguments.Add("--no-bundle");
        arguments.Add(projectPath);
        arguments.Add(outputPath);
        arguments.Add(assemblyPath);

        var originalOutput = Console.Out;
        var capturedOutput = new StringWriter();
        try
        {
            Console.SetOut(capturedOutput);
            Assert.Equal(0, Compiler.Program.Main(arguments.ToArray()));
            return capturedOutput.ToString();
        }
        finally
        {
            Console.SetOut(originalOutput);
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }
}
