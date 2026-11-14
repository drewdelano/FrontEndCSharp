using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FrontEndCSharp.Fecscn.Commands;
using FrontEndCSharp.Fecscn.Configuration;
using FrontEndCSharp.Fecscn.Registry;

namespace FrontEndCSharp.Fecscn.Tests;

public sealed class CommandWorkflowTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "FrontEndCSharp.Fecscn.CommandTests", Guid.NewGuid().ToString("N"));

    [Test]
    public void Discovery_finds_one_host_browser_pair_and_rejects_ambiguity()
    {
        var first = CreateProjectPair("First");
        var discovered = FecscnProjectDiscovery.Discover(first.HostProjectPath);
        Assert.Equal(first.HostProjectPath, discovered.HostProjectPath);
        Assert.Equal(first.BrowserProjectPath, discovered.BrowserProjectPath);

        Assert.Equal(first, FecscnProjectDiscovery.Discover(first.BrowserDirectory));

        CreateProjectPair("Second");
        var exception = Assert.Throws<FecscnCommandException>(
            () => FecscnProjectDiscovery.Discover(root));
        Assert.Contains("multiple", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public void Init_is_idempotent_and_preserves_existing_project_content()
    {
        var project = CreateProjectPair("App");
        var before = File.ReadAllText(project.HostProjectPath);

        var first = FecscnInitializer.Initialize(project, "https://registry.example.test/r");
        var afterFirst = File.ReadAllText(project.HostProjectPath);
        var second = FecscnInitializer.Initialize(project, "https://registry.example.test/r");

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(afterFirst, File.ReadAllText(project.HostProjectPath));
        Assert.Contains("UnrelatedProperty", afterFirst, StringComparison.Ordinal);
        Assert.Contains("FrontEndCSharp.Tailwind", afterFirst, StringComparison.Ordinal);
        Assert.DoesNotContain("FrontEndCSharp.Fecscn.Runtime", File.ReadAllText(project.BrowserProjectPath), StringComparison.Ordinal);
        Assert.DoesNotContain("Interop.JS.Shadcn.BaseUI", File.ReadAllText(project.BrowserProjectPath), StringComparison.Ordinal);
        Assert.DoesNotContain("Interop.JS.Shadcn.Cn", File.ReadAllText(project.BrowserProjectPath), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(project.HostDirectory, "fecscn.json")));
        Assert.True(File.Exists(Path.Combine(project.BrowserDirectory, "Styles", "fecscn.css")));
        Assert.DoesNotContain("FrontEndCSharp.Tailwind", before, StringComparison.Ordinal);
    }

    [Test]
    public async Task Add_resolves_dependencies_and_installs_atomically()
    {
        var project = CreateProjectPair("App");
        FecscnInitializer.Initialize(project, "https://registry.example.test/r");
        var source = new MemoryRegistrySource(
            Item("utils", ("Components/UI/ClassNames.cs", "csharp", "namespace App; public static class ClassNames { }")),
            Item("button", ("Components/UI/Button.fecsx", "fecsx", "using Interop.JS.React; namespace App; public static ReactNode Default(ButtonProps props, ReactNode[] children) => <button>{children}</button>;") ) with
            {
                RegistryDependencies = ["utils"],
                Packages = [new("Example.Package", "1.2.3")]
            });

        var plan = await FecscnRegistryResolver.ResolveAsync(source, ["button"]);
        Assert.Equal(["utils", "button"], plan.Items.Select(item => item.Name));

        FecscnInstaller.Install(project, plan);
        Assert.True(File.Exists(Path.Combine(project.BrowserDirectory, "Components", "UI", "Button.fecsx")));
        Assert.Contains("Example.Package", File.ReadAllText(project.BrowserProjectPath), StringComparison.Ordinal);

        var changed = Path.Combine(project.BrowserDirectory, "Components", "UI", "Button.fecsx");
        File.WriteAllText(changed, "local edit");
        var unrelatedTarget = Path.Combine(project.BrowserDirectory, "Components", "UI", "ClassNames.cs");
        File.Delete(unrelatedTarget);
        var exception = Assert.Throws<FecscnCommandException>(() => FecscnInstaller.Install(project, plan));
        Assert.Contains("overwrite", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(unrelatedTarget));
        Assert.Equal("local edit", File.ReadAllText(changed));
    }

    [Test]
    public async Task Add_rejects_bad_digest_cycle_and_target_escape_before_writes()
    {
        var project = CreateProjectPair("App");
        var cycleA = Item("a", ("Components/UI/A.cs", "csharp", "A")) with { RegistryDependencies = ["b"] };
        var cycleB = Item("b", ("Components/UI/B.cs", "csharp", "B")) with { RegistryDependencies = ["a"] };
        Assert.Contains(
            "cycle",
            (await Assert.ThrowsAsync<FecscnCommandException>(
                () => FecscnRegistryResolver.ResolveAsync(new MemoryRegistrySource(cycleA, cycleB), ["a"]))).Message,
            StringComparison.OrdinalIgnoreCase);

        var badDigest = Item("bad", ("Components/UI/Bad.cs", "csharp", "bad"));
        badDigest = badDigest with { Files = [badDigest.Files[0] with { Sha256 = Hash("different") }] };
        Assert.Contains(
            "digest",
            (await Assert.ThrowsAsync<FecscnCommandException>(
                () => FecscnRegistryResolver.ResolveAsync(new MemoryRegistrySource(badDigest), ["bad"]))).Message,
            StringComparison.OrdinalIgnoreCase);

        var escape = Item("escape", ("../Escape.cs", "csharp", "escape"));
        var plan = await FecscnRegistryResolver.ResolveAsync(new MemoryRegistrySource(escape), ["escape"]);
        Assert.Throws<FecscnCommandException>(() => FecscnInstaller.Install(project, plan));
        Assert.False(File.Exists(Path.Combine(project.BrowserDirectory, "Escape.cs")));
    }

    [Test]
    public async Task Command_host_fetches_and_adds_from_the_configured_registry()
    {
        var project = CreateProjectPair("Cli");
        FecscnInitializer.Initialize(project, "https://registry.example.test/r");
        var item = Item("card", ("Components/UI/Card.cs", "csharp", "namespace App; public sealed class Card { }"));
        using var client = new HttpClient(new RegistryHandler(item));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await FecscnCommandHost.RunAsync(
            ["add", "card", "--project", project.HostProjectPath],
            root,
            output,
            error,
            client);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.Contains("Added card", output.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(project.BrowserDirectory, "Components", "UI", "Card.cs")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private FecscnProject CreateProjectPair(string name)
    {
        var hostDirectory = Path.Combine(root, name, name + ".Host");
        var browserDirectory = Path.Combine(root, name, name + ".Browser");
        Directory.CreateDirectory(hostDirectory);
        Directory.CreateDirectory(browserDirectory);
        var hostProject = Path.Combine(hostDirectory, name + ".Host.csproj");
        var browserProject = Path.Combine(browserDirectory, name + ".Browser.csproj");
        File.WriteAllText(hostProject, $$"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <UnrelatedProperty>keep-me</UnrelatedProperty>
                <FrontEndCSharpProject>$(MSBuildProjectDirectory)\..\{{name}}.Browser\{{name}}.Browser.csproj</FrontEndCSharpProject>
              </PropertyGroup>
              <ItemGroup><PackageReference Include="FrontEndCSharp.Build" Version="0.1.19" /></ItemGroup>
            </Project>
            """);
        File.WriteAllText(browserProject, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="FrontEndCSharp.Fecsx.Build" Version="0.1.0" PrivateAssets="all" />
                <PackageReference Include="Interop.JS.React" Version="19.3.1" />
              </ItemGroup>
            </Project>
            """);
        return new(hostProject, browserProject);
    }

    private static PublishedRegistryItem Item(
        string name,
        params (string Target, string Type, string Content)[] files) =>
        new(
            Version: 1,
            Name: name,
            Type: "registry:ui",
            Upstream: new($"https://ui.shadcn.com/r/styles/base-nova/{name}.json", Hash(name), "components"),
            Dependencies: [],
            RegistryDependencies: [],
            Files: files.Select(file => new PublishedRegistryFile(
                Path: file.Target,
                Target: file.Target,
                Type: file.Type,
                Sha256: Hash(file.Content),
                Content: file.Content,
                ContentBase64: null)).ToArray(),
            Packages: []);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class MemoryRegistrySource(params PublishedRegistryItem[] items) : IFecscnRegistrySource
    {
        private readonly IReadOnlyDictionary<string, PublishedRegistryItem> items =
            items.ToDictionary(item => item.Name, StringComparer.Ordinal);

        public Task<PublishedRegistryItem> GetItemAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(items.TryGetValue(name, out var item)
                ? item
                : throw new FecscnCommandException($"Missing item '{name}'."));
    }

    private sealed class RegistryHandler(PublishedRegistryItem item) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("https://registry.example.test/r/card.json", request.RequestUri!.AbsoluteUri);
            var json = JsonSerializer.Serialize(item, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
