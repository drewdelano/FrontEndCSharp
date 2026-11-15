using System.Xml.Linq;
using System.Collections.Concurrent;

namespace Compiler.Tests;

public class BuildTargetsTests
{
    [Test]
    public void IntermediateOutputUsesShortRepositoryAndProjectKeys()
    {
        var repositoryRoot = FindRepositoryRoot();
        var props = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var baseIntermediateOutputPath = props
            .Descendants("BaseIntermediateOutputPath")
            .Single();

        Assert.Equal(
            "$(_FrontEndCSharpRepositoryRoot)obj\\root-$(_FrontEndCSharpRepositoryRootKey)\\project-$(_FrontEndCSharpProjectPathKey)\\",
            baseIntermediateOutputPath.Value);
        Assert.NotEmpty(props.Descendants("_FrontEndCSharpProjectPathHash"));
        Assert.NotEmpty(props.Descendants("_FrontEndCSharpProjectPathKey"));

        var representativeOutputPath = Path.Combine(
            @"C:\git\FrontEndCSharp.Private",
            "obj",
            "root-12345678",
            "project-12345678",
            "Debug",
            "net8.0",
            "FrontEndCSharp.Overrides.System.ComponentModel.Annotations.fecs.dll");
        Assert.True(
            representativeOutputPath.Length < 260,
            $"Representative intermediate output path is {representativeOutputPath.Length} characters.");
    }

    [Test]
    public void CompilerJavaScriptRootContainsOnlyUniversalRuntimeModules()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compilerRuntimeDirectory = Path.Combine(repositoryRoot, "Compiler", "JsRuntime");

        Assert.Equal(
            new[]
            {
                "bootstrap-msil.js",
                "managed-debugger-hooks.js",
                "managed-debugger.js",
                "managed-exceptions.js",
                "msil-binary.js",
                "msil-type-helpers.js",
                "msil.js",
                "task-runtime.js",
            },
            Directory.EnumerateFiles(compilerRuntimeDirectory, "*.js", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }

    [Test]
    public void CompilerHubDoesNotNameCompanionRuntimeAssets()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runtimeOverridesDirectory = Path.Combine(repositoryRoot, "Overrides");
        var companionAssetFileNames = EnumerateRepositoryFiles(
                runtimeOverridesDirectory,
                "*.js")
            .Where(path => string.Equals(
                new DirectoryInfo(Path.GetDirectoryName(path)!).Name,
                "RuntimeAssets",
                StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(companionAssetFileNames);

        var compilerSource = EnumerateRepositoryFiles(
                Path.Combine(repositoryRoot, "Compiler", "Core"),
                "*")
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(path => new { Path = path, Text = File.ReadAllText(path) })
            .ToArray();

        foreach (var assetFileName in companionAssetFileNames)
        {
            Assert.DoesNotContain(
                compilerSource,
                source => source.Text.Contains(assetFileName, StringComparison.Ordinal));
        }
        Assert.DoesNotContain(
            compilerSource,
            source => source.Text.Contains(".fecs-runtime-assets", StringComparison.Ordinal));
    }

    [Test]
    public void CrossCompanionRuntimeAssetImportsHaveDirectProjectDependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPaths = GetTrackedRepositoryFiles(
                repositoryRoot,
                "Overrides/**/*.fecs.csproj")
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                Path.GetFullPath,
                StringComparer.Ordinal);
        var importPattern = new System.Text.RegularExpressions.Regex(
            @"(?:\bfrom\s+|\bimport\s*)(?<quote>['""])(?<specifier>[^'""]+)\k<quote>",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        var importedCompanions = new ConcurrentBag<(string SourceProjectPath, string TargetCompanionName)>();
        var candidateJavaScriptPaths = TestProcess.Run(
                "git",
                repositoryRoot,
                TimeSpan.FromSeconds(5),
                "grep",
                "-Il",
                "-e",
                ".fecs/",
                "--",
                "Overrides/**/*.js")
            .Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.GetFullPath(Path.Combine(repositoryRoot, path)))
            .ToArray();

        Parallel.ForEach(candidateJavaScriptPaths, javaScriptPath =>
        {
            var sourceCompanionDirectory = new FileInfo(javaScriptPath).Directory;
            while (sourceCompanionDirectory != null
                && !sourceCompanionDirectory.Name.EndsWith(".fecs", StringComparison.Ordinal))
            {
                sourceCompanionDirectory = sourceCompanionDirectory.Parent;
            }
            if (sourceCompanionDirectory == null)
            {
                return;
            }

            foreach (System.Text.RegularExpressions.Match match in importPattern.Matches(
                File.ReadAllText(javaScriptPath)))
            {
                var targetCompanionName = match.Groups["specifier"].Value
                    .Split('/')
                    .FirstOrDefault(segment => segment.EndsWith(".fecs", StringComparison.Ordinal));
                if (targetCompanionName == null
                    || string.Equals(
                        targetCompanionName,
                        sourceCompanionDirectory.Name,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                importedCompanions.Add((
                    Path.Combine(
                        sourceCompanionDirectory.FullName,
                        sourceCompanionDirectory.Name + ".csproj"),
                    targetCompanionName));
            }
        });

        foreach (var sourceProject in importedCompanions.GroupBy(
            import => import.SourceProjectPath,
            StringComparer.OrdinalIgnoreCase))
        {
            var projectDirectory = Path.GetDirectoryName(sourceProject.Key)!;
            var project = XDocument.Load(sourceProject.Key);
            var referencedProjectPaths = project.Descendants("ProjectReference")
                    .Select(element => (string?)element.Attribute("Include"))
                    .OfType<string>()
                    .Select(include => Path.GetFullPath(
                        Path.Combine(projectDirectory, include)))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var targetCompanionName in sourceProject
                .Select(import => import.TargetCompanionName)
                .Distinct(StringComparer.Ordinal))
            {
                Assert.Contains(
                    projectPaths[targetCompanionName],
                    referencedProjectPaths,
                    StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [TestCase("FrontEndCSharp.sln")]
    [TestCase(@"Overrides\Overrides.sln")]
    public void SolutionsContainTheirDirectProjectReferenceClosure(string solutionRelativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        var solutionPath = Path.Combine(repositoryRoot, solutionRelativePath);
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        var solutionProjects = File.ReadLines(solutionPath)
            .Where(line => line.StartsWith("Project(", StringComparison.Ordinal))
            .Select(line => line.Split(',').ElementAtOrDefault(1)?.Trim().Trim('"'))
            .Where(path => path?.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) == true)
            .Select(path => Path.GetFullPath(Path.Combine(solutionDirectory, path!)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(solutionProjects);
        foreach (var sourceProjectPath in solutionProjects)
        {
            var project = XDocument.Load(sourceProjectPath);
            foreach (var include in project.Descendants("ProjectReference")
                .Select(element => (string?)element.Attribute("Include"))
                .OfType<string>()
                .Where(include => !include.Contains("$(", StringComparison.Ordinal)))
            {
                var targetProjectPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(sourceProjectPath)!,
                    include));
                Assert.True(
                    solutionProjects.Contains(targetProjectPath),
                    $"Solution '{solutionRelativePath}' is missing project reference "
                    + $"'{Path.GetRelativePath(repositoryRoot, targetProjectPath)}' from "
                    + $"'{Path.GetRelativePath(repositoryRoot, sourceProjectPath)}'.");
            }
        }
    }

    [TestCase("FrontEndCSharp.Overrides.System.Net.Http", "FrontEndCSharp.Overrides.HttpClientBrowserSmoke")]
    [TestCase("FrontEndCSharp.Overrides.System.Net.Http", "FrontEndCSharp.Overrides.HttpStreamingBrowserSmoke")]
    [TestCase("FrontEndCSharp.Overrides.System.Net.Http.Json", "FrontEndCSharp.Overrides.HttpJsonBrowserSmoke")]
    [TestCase("FrontEndCSharp.Overrides.System.Text.Json", "FrontEndCSharp.Overrides.SystemTextJsonBrowserSmoke")]
    [TestCase("FrontEndCSharp.Overrides.Newtonsoft.Json", "FrontEndCSharp.Overrides.NewtonsoftJsonBrowserSmoke")]
    public void BrowserSmokesEnforceTenSecondAbsoluteLimit(
        string packageName,
        string projectName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Overrides",
            packageName,
            "Tests",
            projectName,
            "Program.cs"));

        Assert.DoesNotContain("TimeSpan.FromSeconds(15)", program, StringComparison.Ordinal);
        Assert.DoesNotContain("TimeSpan.FromSeconds(20)", program, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromSeconds(10)", program, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromSeconds(5)", program, StringComparison.Ordinal);
    }

    [Test]
    public void LegacyHttpBrowserSmokeWasRemovedAfterCSharpMigration()
    {
        var repositoryRoot = FindRepositoryRoot();

        Assert.False(File.Exists(Path.Combine(
            repositoryRoot,
            "Compiler",
            "Compiler.Tests",
            "Integration",
            "http-client-browser-smoke.mjs")));
    }

    [Test]
    public void CompilerPayloadUsesMsBuildContentPipelineWithoutPostBuildShellCopies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compilerProject = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Compiler",
            "Core",
            "Compiler.csproj"));
        var testProject = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Compiler",
            "Compiler.Tests",
            "Compiler.Tests.csproj"));

        Assert.DoesNotContain(
            compilerProject.Descendants("Exec").Concat(testProject.Descendants("Exec")),
            element => ((string?)element.Attribute("Command"))?.Contains(
                "XCOPY",
                StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(
            testProject.Descendants("Target"),
            element => string.Equals(
                (string?)element.Attribute("Name"),
                "PostBuild",
                StringComparison.Ordinal));

        var runtimeContent = Assert.Single(
            compilerProject.Descendants("Content"),
            element => ((string?)element.Attribute("Include"))?.Contains(
                @"JsRuntime\**\*",
                StringComparison.Ordinal) == true);
        Assert.Equal("PreserveNewest", runtimeContent.Element("CopyToOutputDirectory")?.Value);

        var bundlerTarget = Assert.Single(
            compilerProject.Descendants("Target"),
            element => string.Equals(
                (string?)element.Attribute("Name"),
                "BuildJavaScriptBundler",
                StringComparison.Ordinal));
        Assert.Contains(
            "$(IntermediateOutputPath)",
            (string?)bundlerTarget.Attribute("Outputs"),
            StringComparison.Ordinal);

        var includeBundlerTarget = Assert.Single(
            compilerProject.Descendants("Target"),
            element => string.Equals(
                (string?)element.Attribute("Name"),
                "IncludeJavaScriptBundlerOutput",
                StringComparison.Ordinal));
        Assert.Equal(
            "BuildJavaScriptBundler",
            (string?)includeBundlerTarget.Attribute("DependsOnTargets"));
        Assert.Equal(
            "AssignTargetPaths",
            (string?)includeBundlerTarget.Attribute("BeforeTargets"));
        Assert.Null(includeBundlerTarget.Attribute("Inputs"));
        Assert.Null(includeBundlerTarget.Attribute("Outputs"));

        var bundlerOutput = Assert.Single(
            includeBundlerTarget.Descendants("_JavaScriptBundlerOutput"),
            element => ((string?)element.Attribute("Include"))?.Contains(
                @"$(IntermediateOutputPath)JsBundler\**\*",
                StringComparison.Ordinal) == true);
        Assert.Null(bundlerOutput.Element("CopyToOutputDirectory"));

        var bundlerContent = Assert.Single(
            includeBundlerTarget.Descendants("Content"),
            element => string.Equals(
                (string?)element.Attribute("Include"),
                "@(_JavaScriptBundlerOutput)",
                StringComparison.Ordinal));
        Assert.Contains(
            "%(_JavaScriptBundlerOutput.RecursiveDir)",
            bundlerContent.Element("Link")?.Value,
            StringComparison.Ordinal);
        Assert.Equal("PreserveNewest", bundlerContent.Element("CopyToOutputDirectory")?.Value);
    }

    [TestCase("$(FrontEndCSharpDebuggerArmPath)")]
    [TestCase("$(FrontEndCSharpBuildRequestPath)")]
    public void TransientBuildSentinelIsAlwaysDeclaredAsFastUpToDateInput(string include)
    {
        var repositoryRoot = FindRepositoryRoot();
        var targets = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Compiler",
            "Packaging",
            "Assets",
            "FrontEndCSharp.targets"));
        var input = Assert.Single(
            targets.Descendants("UpToDateCheckInput"),
            element => string.Equals(
                (string?)element.Attribute("Include"),
                include,
                StringComparison.Ordinal));

        Assert.Null(input.Attribute("Condition"));
    }

    [Test]
    public void SecondaryDemoCompilationForwardsDebuggerRendezvousArguments()
    {
        var repositoryRoot = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Demo",
            "FrontEndCSharp.Demo",
            "FrontEndCSharp.Demo.csproj"));
        var target = Assert.Single(
            project.Descendants("Target"),
            element => string.Equals(
                (string?)element.Attribute("Name"),
                "GenerateFrontEndCSharpSimpleDemoBrowserOutput",
                StringComparison.Ordinal));
        var compilerInvocation = Assert.Single(
            target.Elements("Exec"),
            element => ((string?)element.Attribute("Command"))?.Contains(
                "$(_FrontEndCSharpResolvedCompilerPath)",
                StringComparison.Ordinal) == true);

        Assert.Contains(
            "$(_FrontEndCSharpDebuggerArguments)",
            (string?)compilerInvocation.Attribute("Command"),
            StringComparison.Ordinal);
        Assert.Contains(
            "--log-level \"$(FrontEndCSharpLogLevel)\"",
            (string?)compilerInvocation.Attribute("Command"),
            StringComparison.Ordinal);
        Assert.Contains(
            "--source-map-mode \"$(FrontEndCSharpSourceMapMode)\"",
            (string?)compilerInvocation.Attribute("Command"),
            StringComparison.Ordinal);
    }

    [Test]
    public void DemoWebHostRemovesPathBoundStaticWebAssetOutputsBeforeBuild()
    {
        var repositoryRoot = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Demo",
            "FrontEndCSharp.Demo",
            "FrontEndCSharp.Demo.csproj"));

        var target = Assert.Single(
            project.Descendants("Target"),
            element => string.Equals(
                (string?)element.Attribute("Name"),
                "RemoveDemoStaticWebAssetOutputs",
                StringComparison.Ordinal));
        Assert.Equal("PrepareForBuild", (string?)target.Attribute("BeforeTargets"));
        Assert.Equal(
            new[]
            {
                "$(TargetDir)$(TargetName).staticwebassets.endpoints.json",
                "$(TargetDir)$(TargetName).staticwebassets.runtime.json",
            },
            target.Descendants("_DemoStaticWebAssetOutput")
                .Select(element => (string?)element.Attribute("Include"))
                .OfType<string>()
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray());
        var delete = Assert.Single(target.Descendants("Delete"));
        Assert.Equal("@(_DemoStaticWebAssetOutput)", (string?)delete.Attribute("Files"));
    }

    [Test]
    public void BuildIntegrationForwardsConfiguredCompilerLogLevel()
    {
        var repositoryRoot = FindRepositoryRoot();
        var targets = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Compiler",
            "Packaging",
            "Assets",
            "FrontEndCSharp.targets"));

        var logLevel = Assert.Single(targets.Descendants("FrontEndCSharpLogLevel"));
        Assert.Equal("normal", logLevel.Value);

        var compilerInvocation = Assert.Single(
            targets.Descendants("Exec"),
            element => ((string?)element.Attribute("Command"))?.Contains(
                "$(_FrontEndCSharpResolvedCompilerPath)",
                StringComparison.Ordinal) == true);
        Assert.Contains(
            "--log-level \"$(FrontEndCSharpLogLevel)\"",
            (string?)compilerInvocation.Attribute("Command"),
            StringComparison.Ordinal);
    }

    [Test]
    public void BuildIntegrationDefaultsForwardsAndFingerprintsSourceMapMode()
    {
        var repositoryRoot = FindRepositoryRoot();
        var targets = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Compiler",
            "Packaging",
            "Assets",
            "FrontEndCSharp.targets"));

        var sourceMapMode = Assert.Single(targets.Descendants("FrontEndCSharpSourceMapMode"));
        Assert.Equal("managed", sourceMapMode.Value);

        var compilerInvocation = Assert.Single(
            targets.Descendants("Exec"),
            element => ((string?)element.Attribute("Command"))?.Contains(
                "$(_FrontEndCSharpResolvedCompilerPath)",
                StringComparison.Ordinal) == true);
        Assert.Contains(
            "--source-map-mode \"$(FrontEndCSharpSourceMapMode)\"",
            (string?)compilerInvocation.Attribute("Command"),
            StringComparison.Ordinal);
        Assert.Contains(
            targets.Descendants("_FrontEndCSharpGenerationConfiguration"),
            element => string.Equals(
                (string?)element.Attribute("Include"),
                "SourceMapMode=$(FrontEndCSharpSourceMapMode)",
                StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory != null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the FrontEndCSharp repository root.");
    }

    private static IEnumerable<string> EnumerateRepositoryFiles(string root, string pattern)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();
            foreach (var path in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
            {
                yield return path;
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(
                directory,
                "*",
                SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(childDirectory);
                if (string.Equals(name, "node_modules", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                pendingDirectories.Push(childDirectory);
            }
        }
    }

    private static IEnumerable<string> GetTrackedRepositoryFiles(
        string repositoryRoot,
        params string[] patterns)
    {
        var result = TestProcess.Run(
            "git",
            repositoryRoot,
            TimeSpan.FromSeconds(5),
            ["ls-files", "--", .. patterns]);
        return result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(repositoryRoot, path));
    }
}
