namespace Compiler.Tests;

public sealed class JavaScriptRuntimeTests
{
    [Test]
    public void RepositoryContainsNoJavaScriptTestEntrypoints()
    {
        var repositoryRoot = FindRepositoryRoot();

        var result = TestProcess.Run(
            "git",
            repositoryRoot,
            TimeSpan.FromSeconds(5),
            "ls-files",
            "*.test.js",
            "*.test.mjs",
            "*.test.cjs");

        Assert.True(
            string.IsNullOrWhiteSpace(result.Output),
            $"JavaScript test entry points must be owned by a .NET test harness:{Environment.NewLine}{result.Output}");
    }

    [Test]
    public void DotNetTestProjectsUseTheNUnitTimingPolicy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var legacyInfrastructurePath = Path.Combine(repositoryRoot, "TestInfrastructure");
        Assert.False(Directory.Exists(legacyInfrastructurePath));

        var policyPath = Path.Combine(
            repositoryRoot,
            "Compiler",
            "Tests",
            "NUnitTestInfrastructure.cs");
        var policy = File.ReadAllText(policyPath);
        Assert.Contains("TestActionAttribute", policy, StringComparison.Ordinal);
        Assert.Contains("ActionTargets.Test", policy, StringComparison.Ordinal);
        Assert.Contains("MaximumMilliseconds = 10_000", policy, StringComparison.Ordinal);
        Assert.Contains("SlowMilliseconds = 5_000", policy, StringComparison.Ordinal);
        Assert.Contains("[assembly: LevelOfParallelism(4)]", policy, StringComparison.Ordinal);
        Assert.Contains("[assembly: Parallelizable(ParallelScope.Fixtures)]", policy, StringComparison.Ordinal);
        Assert.Contains("[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]", policy, StringComparison.Ordinal);

        var runtimeOverridePolicy = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Compiler",
            "RuntimeOverrides",
            "Tests",
            "RuntimeOverrideTest.props"));
        Assert.Contains("NUnitTestInfrastructure.cs", runtimeOverridePolicy, StringComparison.Ordinal);
        Assert.Contains("NUnit3TestAdapter", runtimeOverridePolicy, StringComparison.Ordinal);
        Assert.Contains(
            "<PackageReference Include=\"NUnit\"",
            runtimeOverridePolicy,
            StringComparison.Ordinal);

        var trackedProjects = TestProcess.Run(
            "git",
            repositoryRoot,
            TimeSpan.FromSeconds(5),
            "ls-files",
            "*.csproj").Output;
        var testProjects = trackedProjects
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))
            .Where(File.Exists)
            .Select(path => new RepositoryTextFile(path, File.ReadAllText(path)))
            .Where(project => project.Text.Contains("Microsoft.NET.Test.Sdk", StringComparison.Ordinal)
                || project.Text.Contains("RuntimeOverrideTest.props", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(testProjects);
        Assert.All(testProjects, project =>
        {
            Assert.DoesNotContain("xunit", project.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RunSettingsFilePath", project.Text, StringComparison.Ordinal);
            Assert.True(
                (project.Text.Contains("NUnitTestInfrastructure.cs", StringComparison.Ordinal)
                    && project.Text.Contains("NUnit3TestAdapter", StringComparison.Ordinal)
                    && project.Text.Contains("<PackageReference Include=\"NUnit\"", StringComparison.Ordinal))
                || project.Text.Contains("RuntimeOverrideTest.props", StringComparison.Ordinal),
                $"Test project does not use the NUnit timing policy: {project.Path}");
        });
    }

    [Test]
    public void RepositoryContainsOnlyApprovedLinkedCSharpSources()
    {
        var repositoryRoot = FindRepositoryRoot();
        var trackedMsBuildFiles = TestProcess.Run(
            "git",
            repositoryRoot,
            TimeSpan.FromSeconds(5),
            "ls-files",
            "*.csproj",
            "*.props",
            "*.targets").Output;
        var linkedItems = new List<LinkedCompileItem>();

        foreach (var projectPath in trackedMsBuildFiles.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries))
        {
            var fullProjectPath = Path.Combine(
                repositoryRoot,
                projectPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullProjectPath)) continue;
            var document = System.Xml.Linq.XDocument.Load(fullProjectPath);
            linkedItems.AddRange(document
                .Descendants()
                .Where(element => element.Name.LocalName == "Compile"
                    && !string.IsNullOrWhiteSpace(element.Attribute("Link")?.Value))
                .Select(element => new LinkedCompileItem(
                    projectPath.Replace('\\', '/'),
                    element.Attribute("Include")?.Value ?? string.Empty,
                    element.Attribute("Link")?.Value ?? string.Empty)));
        }

        var orderedItems = linkedItems.OrderBy(item => item.ProjectPath, StringComparer.Ordinal).ToArray();
        var testPolicyItems = orderedItems
            .Where(item => string.Equals(item.Link, "NUnitTestInfrastructure.cs", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(8, testPolicyItems.Length);
        Assert.All(testPolicyItems, linkedItem =>
        {
            if (linkedItem.ProjectPath.EndsWith("RuntimeOverrideTest.props", StringComparison.Ordinal))
            {
                Assert.Equal(
                    @"$(MSBuildThisFileDirectory)..\..\Tests\NUnitTestInfrastructure.cs",
                    linkedItem.Include);
                return;
            }

            Assert.EndsWith(
                @"Compiler\Tests\NUnitTestInfrastructure.cs",
                Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Path.Combine(repositoryRoot, linkedItem.ProjectPath))!,
                    linkedItem.Include)),
                StringComparison.OrdinalIgnoreCase);
        });

        Assert.Collection(
            orderedItems.Except(testPolicyItems),
            linkedItem =>
            {
                Assert.Equal(
                    "Compiler/Compiler.Tests/Fixtures/ProcessRunnerFixture/ProcessRunnerFixture.csproj",
                    linkedItem.ProjectPath);
                Assert.Equal(@"..\..\TestProcess.cs", linkedItem.Include);
                Assert.Equal("TestProcess.cs", linkedItem.Link);
            },
            linkedItem =>
            {
                Assert.Equal(
                    "Fecscn/Registry/CompileFixture/FrontEndCSharp.Fecscn.Registry.CompileFixture.csproj",
                    linkedItem.ProjectPath);
                Assert.Equal(@"..\Sources\**\*.cs", linkedItem.Include);
                Assert.Equal(@"Registry\%(RecursiveDir)%(Filename)%(Extension)", linkedItem.Link);
            },
            linkedItem =>
            {
                Assert.Equal(
                    "Fecscn/Tests/FecscnBrowserFixture/FecscnBrowserFixture.csproj",
                    linkedItem.ProjectPath);
                Assert.Equal(@"..\..\Registry\Sources\**\*.cs", linkedItem.Include);
                Assert.Equal(@"Registry\%(RecursiveDir)%(Filename)%(Extension)", linkedItem.Link);
            });
    }

    [TestCase("JsRuntime/arithmetic-exceptions.runtime.mjs")]
    [TestCase("JsRuntime/array-exceptions.runtime.mjs")]
    [TestCase("JsRuntime/checked-conversions.runtime.mjs")]
    [TestCase("JsRuntime/exception-interop.runtime.mjs")]
    [TestCase("JsRuntime/fixed-width-conversions.runtime.mjs")]
    [TestCase("JsRuntime/floating-conversions.runtime.mjs")]
    [TestCase("JsRuntime/managed-debugger.runtime.mjs")]
    [TestCase("JsRuntime/missing-member-exceptions.runtime.mjs")]
    [TestCase("JsRuntime/msil-binary.runtime.mjs")]
    [TestCase("JsRuntime/native-integers.runtime.mjs")]
    [TestCase("JsRuntime/null-receiver-exceptions.runtime.mjs")]
    [TestCase("JsRuntime/references.runtime.mjs")]
    [TestCase("JsRuntime/runtime-debugger-mode.runtime.mjs")]
    [TestCase("JsRuntime/runtime-type-identity.runtime.mjs")]
    [TestCase("JsRuntime/sizeof.runtime.mjs")]
    [TestCase("JsRuntime/static-fields.runtime.mjs")]
    [TestCase("JsRuntime/unsigned-comparisons.runtime.mjs")]
    [TestCase("JsRuntime/unsupported-opcodes.runtime.mjs")]
    public void RuntimeScenarioPassesUnderTheDotNetTestHost(string relativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        var scenarioPath = Path.Combine(
            repositoryRoot,
            "Compiler",
            "Compiler.Tests",
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(scenarioPath), $"Runtime scenario does not exist: {scenarioPath}");
        TestProcess.Run(
            "node",
            repositoryRoot,
            TimeSpan.FromSeconds(10),
            "--test",
            scenarioPath);
    }

    public static TestCaseDataCollection<string, string> CompanionRuntimeAssetScenarios
    {
        get
        {
            var repositoryRoot = FindRepositoryRoot();
            var data = new TestCaseDataCollection<string, string>();
            var runtimeOverridesDirectory = Path.Combine(repositoryRoot, "Overrides");
            foreach (var scenarioPath in Directory.EnumerateFiles(
                runtimeOverridesDirectory,
                "*.runtime.mjs",
                SearchOption.AllDirectories))
            {
                var scenarioDirectory = new DirectoryInfo(Path.GetDirectoryName(scenarioPath)!);
                if (!string.Equals(scenarioDirectory.Name, "JsRuntime", StringComparison.Ordinal)
                    || !string.Equals(scenarioDirectory.Parent?.Name, "Tests", StringComparison.Ordinal)
                    || scenarioDirectory.Parent?.Parent == null)
                {
                    continue;
                }

                var scenarioFileName = Path.GetFileName(scenarioPath);
                var assetFileName = scenarioFileName[..^".runtime.mjs".Length] + ".js";
                var assetPath = Assert.Single(Directory.EnumerateFiles(
                    scenarioDirectory.Parent.Parent.FullName,
                    assetFileName,
                    SearchOption.AllDirectories),
                    path => string.Equals(
                        new DirectoryInfo(Path.GetDirectoryName(path)!).Name,
                        "RuntimeAssets",
                        StringComparison.Ordinal));
                data.Add(
                    Path.GetRelativePath(repositoryRoot, scenarioPath),
                    Path.GetRelativePath(repositoryRoot, assetPath));
            }
            return data;
        }
    }

    [TestCaseSource(nameof(CompanionRuntimeAssetScenarios))]
    public void CompanionRuntimeAssetScenarioPassesUnderTheDotNetTestHost(
        string scenarioRelativePath,
        string assetRelativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        RunCompanionRuntimeAssetScenario(
            repositoryRoot,
            Path.Combine(repositoryRoot, scenarioRelativePath),
            Path.Combine(repositoryRoot, assetRelativePath));
    }

    private static void RunCompanionRuntimeAssetScenario(
        string repositoryRoot,
        string scenarioPath,
        string runtimeAssetPath)
    {
        Assert.True(File.Exists(runtimeAssetPath), $"Runtime asset does not exist: {runtimeAssetPath}");
        var stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-RuntimeAssetTest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var stagedRuntime = Path.Combine(stagingDirectory, "JsRuntime");
            CopyDirectory(
                Path.Combine(repositoryRoot, "Compiler", "JsRuntime"),
                stagedRuntime);
            var companionAssemblyName = new DirectoryInfo(
                Path.GetDirectoryName(runtimeAssetPath)!).Parent!.Name;
            var stagedAssetDirectory = Path.Combine(
                stagedRuntime,
                "runtime-assets",
                companionAssemblyName);
            CopyDirectory(
                Path.GetDirectoryName(runtimeAssetPath)!,
                stagedAssetDirectory);

            var stagedScenarioDirectory = Path.Combine(
                stagingDirectory,
                "Compiler.Tests",
                "JsRuntime");
            Directory.CreateDirectory(stagedScenarioDirectory);
            var stagedScenario = Path.Combine(
                stagedScenarioDirectory,
                Path.GetFileName(scenarioPath));
            File.Copy(scenarioPath, stagedScenario);

            TestProcess.Run(
                "node",
                stagingDirectory,
                TimeSpan.FromSeconds(10),
                "--test",
                stagedScenario);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(current.FullName, "Compiler")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }
}

internal sealed class LinkedCompileItem
{
    internal LinkedCompileItem(string projectPath, string include, string link)
    {
        ProjectPath = projectPath;
        Include = include;
        Link = link;
    }

    internal string ProjectPath { get; }
    internal string Include { get; }
    internal string Link { get; }
}

internal sealed class RepositoryTextFile
{
    internal RepositoryTextFile(string path, string text)
    {
        Path = path;
        Text = text;
    }

    internal string Path { get; }
    internal string Text { get; }
}
