using System.Diagnostics;
using System.Security;
using System.Xml.Linq;

namespace FrontEndCSharp.Fecsx.Tests;

public sealed class FecsxBuildPackageTests
{
    [Test]
    public void BuildPackageExposesTheAttributeCasingPropertyToTheGenerator()
    {
        var repositoryRoot = FindRepositoryRoot();
        var props = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Compiler",
            "Fecsx",
            "FrontEndCSharp.FECS.Internals.Fecsx.Build",
            "buildTransitive",
            "FrontEndCSharp.FECS.Internals.Fecsx.Build.props"));

        Assert.Contains(props.Descendants(), element =>
            element.Name.LocalName == "CompilerVisibleProperty"
            && (string?)element.Attribute("Include") ==
                "FrontEndCSharpFecsxAttributeCasing");
    }

    [Test]
    public async Task PackageBuildsFecsxAdditionalFilesAndExposesGeneratedSourceUnderObj()
    {
        var repositoryRoot = FindRepositoryRoot();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"FrontEndCSharp_FecsxBuild_{Guid.NewGuid():N}");
        var packageDirectory = Path.Combine(temporaryRoot, "packages");
        var consumerDirectory = Path.Combine(temporaryRoot, "consumer");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(consumerDirectory);

        try
        {
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            await RunDotNet(
                repositoryRoot,
                "pack",
                Path.Combine(
                    repositoryRoot,
                    "Compiler",
                    "Fecsx",
                    "FrontEndCSharp.FECS.Internals.Fecsx.Build",
                    "FrontEndCSharp.FECS.Internals.Fecsx.Build.csproj"),
                "--configuration",
                configuration,
                "--no-build",
                "--no-restore",
                "-o",
                packageDirectory);

            File.WriteAllText(Path.Combine(consumerDirectory, "NuGet.Config"), $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="fecsx" value="{{SecurityElement.Escape(packageDirectory)}}" />
                  </packageSources>
                </configuration>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "Consumer.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <RestorePackagesPath>{{SecurityElement.Escape(Path.Combine(temporaryRoot, "global-packages"))}}</RestorePackagesPath>
                    <FrontEndCSharpFecsxAttributeCasing>PascalCase</FrontEndCSharpFecsxAttributeCasing>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="FrontEndCSharp.FECS.Internals.Fecsx.Build" Version="0.1.0" />
                    <Reference Include="FrontEndCSharp.Overrides.Interop.JS.React">
                      <HintPath>{{SecurityElement.Escape(Path.Combine(AppContext.BaseDirectory, "FrontEndCSharp.Overrides.Interop.JS.React.dll"))}}</HintPath>
                    </Reference>
                    <Reference Include="FrontEndCSharp.Overrides.Interop.JS.Browser">
                      <HintPath>{{SecurityElement.Escape(Path.Combine(AppContext.BaseDirectory, "FrontEndCSharp.Overrides.Interop.JS.Browser.dll"))}}</HintPath>
                    </Reference>
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "GreetingProps.cs"), "namespace Consumer; public sealed class GreetingProps { public string Name { get; set; } = \"World\"; }");
            File.WriteAllText(Path.Combine(consumerDirectory, "Greeting.fecsx"), "using Interop.JS.React; namespace Consumer; public static ReactNode Default(GreetingProps props, ReactNode[] children) => <section ClassName=\"greeting\">Hello {props.Name}</section>;");

            await RunDotNet(consumerDirectory, "build", "Consumer.csproj", "--configfile", "NuGet.Config");

            var generated = Assert.Single(Directory.GetFiles(Path.Combine(consumerDirectory, "obj"), "*.fecsx.g.cs", SearchOption.AllDirectories));
            var generatedSource = File.ReadAllText(generated);
            Assert.Contains("public static class Greeting", generatedSource);
            Assert.Contains("React.CreateElement(\"section\"", generatedSource);
            Assert.Contains("__props.ClassName = \"greeting\";", generatedSource);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static async Task RunDotNet(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail("dotnet did not exit within 10 seconds.");
        }

        var output = string.Concat(await Task.WhenAll(standardOutput, standardError)
            .WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(process.ExitCode == 0, output);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory !=  null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
