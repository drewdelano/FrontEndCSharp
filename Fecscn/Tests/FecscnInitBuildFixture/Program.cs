using System.Diagnostics;
using System.Text.Json;
using FrontEndCSharp.Fecscn.Commands;
using FrontEndCSharp.Fecscn.Registry;

var repositoryRoot = FindRepositoryRoot();
var fixtureRoot = Path.Combine(
    Path.GetTempPath(), "FrontEndCSharp.Fecscn.InitBuild", Guid.NewGuid().ToString("N"));
var packages = Path.Combine(fixtureRoot, "packages");
var appRoot = Path.Combine(fixtureRoot, "Sample");
var hostDirectory = Path.Combine(appRoot, "Sample.Host");
var browserDirectory = Path.Combine(appRoot, "Sample.Browser");
Directory.CreateDirectory(packages);
Directory.CreateDirectory(hostDirectory);
Directory.CreateDirectory(browserDirectory);

try
{
    await Run("dotnet", ["pack", Path.Combine(repositoryRoot, "Fecscn", "FrontEndCSharp.FECS.Internals.Tailwind", "FrontEndCSharp.FECS.Internals.Tailwind.csproj"), "-c", "Release", "-o", packages, "--verbosity", "minimal"], repositoryRoot);
    foreach (var packageProject in new[]
    {
        Path.Combine(repositoryRoot, "Compiler", "Fecsx", "FrontEndCSharp.FECS.Internals.Fecsx.Build", "FrontEndCSharp.FECS.Internals.Fecsx.Build.csproj"),
        Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.Browser", "FrontEndCSharp.Overrides.Interop.JS.Browser", "FrontEndCSharp.Overrides.Interop.JS.Browser.csproj"),
        Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.React", "FrontEndCSharp.Overrides.Interop.JS.React", "FrontEndCSharp.Overrides.Interop.JS.React.csproj"),
        Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI.csproj"),
        Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI.fecs", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI.fecs.csproj"),
        Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn.csproj"),
        Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn.fecs", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn.fecs.csproj"),
        Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.LucideIcons", "FrontEndCSharp.Overrides.Interop.JS.LucideIcons", "FrontEndCSharp.Overrides.Interop.JS.LucideIcons.csproj"),
        Path.Combine(repositoryRoot, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.LucideIcons", "FrontEndCSharp.Overrides.Interop.JS.LucideIcons.fecs", "FrontEndCSharp.Overrides.Interop.JS.LucideIcons.fecs.csproj")
    })
    {
        await Run("dotnet", ["pack", packageProject, "-c", "Release", "-o", packages, "--verbosity", "minimal"], repositoryRoot);
    }

    var browserProject = Path.Combine(browserDirectory, "Sample.Browser.csproj");
    var hostProject = Path.Combine(hostDirectory, "Sample.Host.csproj");
    await File.WriteAllTextAsync(browserProject, $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
          <ItemGroup>
            <PackageReference Include="FrontEndCSharp.FECS.Internals.Fecsx.Build" Version="0.1.0" PrivateAssets="all" />
            <PackageReference Include="FrontEndCSharp.Overrides.Interop.JS.React" Version="19.3.1" />
            <AdditionalFiles Include="**\*.fecsx" />
          </ItemGroup>
        </Project>
        """);
    await File.WriteAllTextAsync(hostProject, $$"""
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <FrontEndCSharpProject>$(MSBuildProjectDirectory)\..\Sample.Browser\Sample.Browser.csproj</FrontEndCSharpProject>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="FrontEndCSharp.FECS.Internals.Build" Version="0.1.0" Condition="'$(Never)' == 'true'" />
            <ProjectReference Include="$(FrontEndCSharpProject)" />
          </ItemGroup>
        </Project>
        """);
    await File.WriteAllTextAsync(Path.Combine(hostDirectory, "Program.cs"), """
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);
        var app = builder.Build();
        app.Run();
        """);
    await File.WriteAllTextAsync(Path.Combine(browserDirectory, "App.fecsx"), """
        using Interop.JS.React;
        namespace Sample;
        public sealed class AppProps { }
        public static ReactNode Default(AppProps props, ReactNode[] children) => <div className="bg-red-500">Ready</div>;
        """);

    var project = FecscnProjectDiscovery.Discover(hostProject);
    FecscnInitializer.Initialize(project, "https://registry.example.test/r");
    var registryOutput = Path.Combine(fixtureRoot, "registry");
    RegistryBuilder.BuildFromFile(
        Path.Combine(repositoryRoot, "Fecscn", "Registry", "registry.json"),
        registryOutput);
    var plan = await FecscnRegistryResolver.ResolveAsync(
        new DirectoryRegistrySource(registryOutput),
        ["checkbox"]);
    FecscnInstaller.Install(project, plan);
    var browserProjectContent = await File.ReadAllTextAsync(browserProject);
    if (!browserProjectContent.Contains("FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI", StringComparison.Ordinal)
        || !browserProjectContent.Contains("FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn", StringComparison.Ordinal)
        || !browserProjectContent.Contains("FrontEndCSharp.Overrides.Interop.JS.LucideIcons", StringComparison.Ordinal)
        || browserProjectContent.Contains("FrontEndCSharp.Fecscn.ReactImports", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "fecscn add did not install the pinned Base UI, cn, and Lucide adapter packages.");
    }
    var nugetConfig = Path.Combine(appRoot, "NuGet.Config");
    await File.WriteAllTextAsync(nugetConfig, $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="fixture" value="{{packages}}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
        </configuration>
        """);
    await Run("dotnet", ["restore", hostProject, "--configfile", nugetConfig, "--packages", Path.Combine(fixtureRoot, "nuget"), "--verbosity", "minimal"], appRoot);
    await Run("dotnet", ["build", hostProject, "-c", "Release", "--no-restore", "--verbosity", "minimal"], appRoot);
    var css = Path.Combine(hostDirectory, "wwwroot", "css", "fecscn.css");
    if (!File.Exists(css) || !(await File.ReadAllTextAsync(css)).Contains(".bg-red-500", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Initialized build did not produce the expected tree-shaken Tailwind CSS.");
    }
    var timestamp = File.GetLastWriteTimeUtc(css);
    await Run("dotnet", ["build", hostProject, "-c", "Release", "--no-restore", "--verbosity", "minimal"], appRoot);
    if (File.GetLastWriteTimeUtc(css) != timestamp)
    {
        throw new InvalidOperationException("Unchanged initialized build rewrote its Tailwind output.");
    }
    Console.WriteLine("fecscn init/add restore/build fixture passed.");
    return 0;
}
finally
{
    Directory.Delete(fixtureRoot, recursive: true);
}

static async Task Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = fileName,
        WorkingDirectory = workingDirectory,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
    };
    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"{fileName} failed.\n{await stdout}\n{await stderr}");
    }
    Console.Write(await stdout);
    Console.Error.Write(await stderr);
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln")))
    {
        directory = directory.Parent;
    }
    return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
}

internal sealed class DirectoryRegistrySource(string directory) : IFecscnRegistrySource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PublishedRegistryItem> GetItemAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(directory, name + ".json");
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<PublishedRegistryItem>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Registry item '{name}' was JSON null.");
    }
}
