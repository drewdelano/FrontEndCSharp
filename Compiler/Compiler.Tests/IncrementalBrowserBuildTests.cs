namespace Compiler.Tests;

public sealed class IncrementalBrowserBuildTests
{
    [Test]
    public void MsBuildSkipsCompilerAndBundlerForANoOpBrowserBuild()
    {
        using var fixture = new IncrementalBuildFixture();
        fixture.Build();
        fixture.Build();
        Assert.Equal(1, fixture.InvocationCount);
    }

    [TestCase("application")]
    [TestCase("dependency")]
    [TestCase("pdb")]
    [TestCase("manifest")]
    [TestCase("companion")]
    [TestCase("compiler-payload")]
    [TestCase("runtime-payload")]
    [TestCase("configuration")]
    public void MsBuildInvalidatesEachBrowserCompilerInputClass(string inputClass)
    {
        using var fixture = new IncrementalBuildFixture();
        fixture.Build();
        fixture.Mutate(inputClass);
        fixture.Build(inputClass == "configuration"
            ? $"/p:FrontEndCSharpOutputPath={fixture.AlternateOutputPath}"
            : null);
        Assert.Equal(2, fixture.InvocationCount);
    }

    private sealed class IncrementalBuildFixture : IDisposable
    {
        private readonly string root;
        private readonly string configuration;
        private readonly string hostProject;
        private readonly string output;
        private readonly Dictionary<string, string> inputs;

        public IncrementalBuildFixture()
        {
            root = Path.Combine(Path.GetTempPath(), "FrontEndCSharp-Incremental-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var repository = RuntimeOverrideToolingTests.RepositoryRoot;
            var compilerSource = Path.Combine(
                repository, "Compiler", "Compiler.Tests", "Fixtures", "IncrementalCompilerFixture",
                "bin", configuration, "net8.0");
            var compiler = Path.Combine(root, "compiler");
            CopyDirectory(compilerSource, compiler);
            var runtimeInput = Write(compiler, Path.Combine("JsRuntime", "runtime.js"), "runtime-one");
            var compilerPayloadInput = Write(compiler, "payload.dat", "payload-one");

            var frontEndProject = Path.Combine(root, "Browser.csproj");
            File.WriteAllText(frontEndProject, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
            var targetDirectory = Path.Combine(root, "bin", configuration, "net8.0");
            inputs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["application"] = Write(targetDirectory, "Browser.dll", "app-one"),
                ["dependency"] = Write(targetDirectory, "Dependency.dll", "dependency-one"),
                ["pdb"] = Write(targetDirectory, "Browser.pdb", "pdb-one"),
                ["manifest"] = Write(targetDirectory, "Browser.deps.json", "deps-one"),
                ["companion"] = Write(targetDirectory, "Browser.fecs.dll", "companion-one"),
                ["compiler-payload"] = compilerPayloadInput,
                ["runtime-payload"] = runtimeInput,
            };

            output = Path.Combine(root, "wwwroot", "js");
            hostProject = Path.Combine(root, "Host.csproj");
            var tasks = Path.Combine(
                repository, "Compiler", "Packaging", "FrontEndCSharp.Build.Tasks", "bin",
                configuration, "net8.0", "FrontEndCSharp.Build.Tasks.dll");
            var targets = Path.Combine(repository, "Compiler", "Packaging", "Assets", "FrontEndCSharp.targets");
            File.WriteAllText(hostProject, $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <FrontEndCSharpProject>{{frontEndProject}}</FrontEndCSharpProject>
                    <FrontEndCSharpOutputPath>{{output}}</FrontEndCSharpOutputPath>
                    <FrontEndCSharpCompilerPath>{{Path.Combine(compiler, "IncrementalCompilerFixture.dll")}}</FrontEndCSharpCompilerPath>
                    <FrontEndCSharpBuildTasksPath>{{tasks}}</FrontEndCSharpBuildTasksPath>
                  </PropertyGroup>
                  <Import Project="{{targets}}" />
                </Project>
                """);
        }

        public string AlternateOutputPath => Path.Combine(root, "alternate-output");
        public int InvocationCount => int.Parse(File.ReadAllText(Path.Combine(root, "compiler-invocations.txt")));

        public void Build(string? extraArgument = null)
        {
            var arguments = new List<string>
            {
                "msbuild", hostProject, "/t:GenerateFrontEndCSharpBrowserOutput", "/v:q",
                "/p:BuildingProject=true", $"/p:Configuration={configuration}",
            };
            if (extraArgument != null)
            {
                arguments.Add(extraArgument);
            }
            TestProcess.Run("dotnet", root, TimeSpan.FromSeconds(10), arguments.ToArray());
        }

        public void Mutate(string inputClass)
        {
            if (inputClass == "configuration")
            {
                Directory.CreateDirectory(AlternateOutputPath);
                foreach (var name in new[] { "index.js", "index.js.map", "msil.bin" })
                {
                    File.Copy(
                        Path.Combine(output, name),
                        Path.Combine(AlternateOutputPath, name));
                }
            }
            else
            {
                File.AppendAllText(inputs[inputClass], "-changed");
            }
        }

        public void Dispose() => Directory.Delete(root, recursive: true);

        private static string Write(string directory, string name, string contents)
        {
            var path = Path.Combine(directory, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            return path;
        }

        private static void CopyDirectory(string source, string destination)
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var output = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.Copy(file, output);
            }
        }
    }
}
