using System.Xml.Linq;
using FrontEndCSharp.Tailwind;

namespace FrontEndCSharp.Tailwind.Tests;

public sealed class TailwindCompilerTests
{
    [Test]
    public void Build_props_expose_tailwind_availability_to_project_aware_editors()
    {
        var props = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "Fecscn",
            "FrontEndCSharp.FECS.Internals.Tailwind",
            "buildTransitive",
            "FrontEndCSharp.FECS.Internals.Tailwind.props"));
        var targets = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "Fecscn",
            "FrontEndCSharp.FECS.Internals.Tailwind",
            "buildTransitive",
            "FrontEndCSharp.FECS.Internals.Tailwind.targets"));
        var visibleProperties = props.Descendants()
            .Where(element => element.Name.LocalName == "CompilerVisibleProperty")
            .Select(element => (string?)element.Attribute("Include"))
            .ToArray();

        Assert.Contains("FrontEndCSharpTailwindAvailable", visibleProperties);
        Assert.Contains("FrontEndCSharpTailwindEnabled", visibleProperties);
        Assert.Contains("FrontEndCSharpTailwindEditorCatalog", visibleProperties);
        Assert.Contains("FrontEndCSharpProject", visibleProperties);
        Assert.Contains(props.Descendants(), element =>
            element.Name.LocalName == "FrontEndCSharpTailwindAvailable" &&
            string.Equals(element.Value, "true", StringComparison.OrdinalIgnoreCase));
        var editorCatalog = targets.Descendants()
            .Single(element => element.Name.LocalName == "FrontEndCSharpTailwindEditorCatalog")
            .Value;
        var cacheDirectory = targets.Descendants()
            .Single(element => element.Name.LocalName == "FrontEndCSharpTailwindCacheDirectory")
            .Value;
        Assert.Contains("$(BaseIntermediateOutputPath)", cacheDirectory, StringComparison.Ordinal);
        Assert.Contains("$(Configuration)", cacheDirectory, StringComparison.Ordinal);
        Assert.Contains("$(TargetFramework)", cacheDirectory, StringComparison.Ordinal);
        Assert.Contains("$(FrontEndCSharpTailwindCacheDirectory)", editorCatalog, StringComparison.Ordinal);
        Assert.Contains("editor-catalog.tsv.gz", editorCatalog, StringComparison.Ordinal);
    }

    [Test]
    public async Task Compile_generates_an_editor_catalog_from_the_effective_v4_theme()
    {
        using var fixture = new TemporaryTailwindFixture();
        var source = fixture.Write(
            "Components/Panel.fecsx",
            "<div className=\"tablet:block drop-shadow-card\" />");
        fixture.Write(
            "Styles/tokens.css",
            """
            @theme {
              --breakpoint-tablet: 50rem;
              --drop-shadow-card: 0 12px 24px rgb(0 0 0 / 0.18);
            }
            @custom-variant dark (&:where(.dark, .dark *));
            """);
        var theme = fixture.Write("Styles/fecscn.css", "@import \"./tokens.css\";");
        var request = fixture.CreateRequest([source]) with { ThemeInputPath = theme };

        Assert.Equal(TailwindCompileResult.Built, await TailwindCompiler.CompileAsync(request));

        var catalogPath = Path.Combine(request.CacheDirectory, "editor-catalog.tsv.gz");
        Assert.True(File.Exists(catalogPath));
        var catalog = ReadGzipText(catalogPath);
        var dropShadow = Assert.Single(
            catalog.Split('\n'),
            line => line.StartsWith("drop-shadow-card\t", StringComparison.Ordinal));
        Assert.Contains("--tw-drop-shadow-size: drop-shadow(", dropShadow, StringComparison.Ordinal);
        Assert.Contains("12px 24px", dropShadow, StringComparison.Ordinal);
        Assert.Contains("tablet:\t@media (width >= 50rem)", catalog, StringComparison.Ordinal);
        Assert.Contains("max-tablet:\t@media (width < 50rem)", catalog, StringComparison.Ordinal);
        Assert.Contains("dark:\t&:where(.dark, .dark *)", catalog, StringComparison.Ordinal);

    }

    [Test]
    public async Task Imported_v4_theme_changes_rebuild_the_project_editor_catalog()
    {
        using var fixture = new TemporaryTailwindFixture();
        var source = fixture.Write("Components/Panel.fecsx", "<div className=\"block\" />");
        var tokens = fixture.Write(
            "Styles/tokens.css",
            "@theme { --drop-shadow-card: 0 12px 24px rgb(0 0 0 / 0.18); }");
        var theme = fixture.Write("Styles/fecscn.css", "@import \"./tokens.css\";");
        var request = fixture.CreateRequest([source]) with { ThemeInputPath = theme };
        var catalogPath = Path.Combine(request.CacheDirectory, "editor-catalog.tsv.gz");

        Assert.Equal(TailwindCompileResult.Built, await TailwindCompiler.CompileAsync(request));
        await File.AppendAllTextAsync(
            tokens,
            Environment.NewLine + "@theme { --drop-shadow-soft: 0 3px 8px rgb(0 0 0 / 0.12); }");
        Assert.Equal(TailwindCompileResult.Built, await TailwindCompiler.CompileAsync(request));
        Assert.Contains("drop-shadow-soft\t", ReadGzipText(catalogPath), StringComparison.Ordinal);
    }

    [Test]
    public async Task Missing_project_editor_catalog_is_rebuilt_without_recompiling_css()
    {
        using var fixture = new TemporaryTailwindFixture();
        var source = fixture.Write("Components/Panel.fecsx", "<div className=\"block\" />");
        var request = fixture.CreateRequest([source]);
        var catalogPath = Path.Combine(request.CacheDirectory, "editor-catalog.tsv.gz");

        Assert.Equal(TailwindCompileResult.Built, await TailwindCompiler.CompileAsync(request));
        var outputTimestamp = File.GetLastWriteTimeUtc(request.OutputPath);
        File.Delete(catalogPath);

        Assert.Equal(TailwindCompileResult.Built, await TailwindCompiler.CompileAsync(request));
        Assert.True(File.Exists(catalogPath));
        Assert.Equal(outputTimestamp, File.GetLastWriteTimeUtc(request.OutputPath));
    }

    [Test]
    public void Editor_catalog_is_regenerated_from_the_pinned_tailwind_engine()
    {
        using var fixture = new TemporaryTailwindFixture();
        var generated = Path.Combine(fixture.Root, "TailwindClassCatalog.tsv.gz");
        var checkedIn = Path.Combine(
            FindRepositoryRoot(),
            "Compiler",
            "Fecsx",
            "FrontEndCSharp.Fecsx.Editor",
            "Assets",
            "TailwindClassCatalog.tsv.gz");

        TailwindEditorCatalogGenerator.Generate(fixture.ToolRoot, generated);

        Assert.Equal(File.ReadAllBytes(checkedIn), File.ReadAllBytes(generated));
        var catalog = ReadGzipText(generated);
        Assert.Contains("#\t4.3.5\t", catalog, StringComparison.Ordinal);
        Assert.Contains("\t0.25rem\n", catalog, StringComparison.Ordinal);
        Assert.Contains("m-1\t\tmargin: var(--spacing);\tm-", catalog, StringComparison.Ordinal);
        Assert.Contains("m-auto\t\tmargin: auto;\tm-", catalog, StringComparison.Ordinal);
        Assert.Contains("-col-end-1\t\tgrid-column-end: calc(1 * -1);\t-col-end-", catalog, StringComparison.Ordinal);
        Assert.Contains("#families\t", catalog, StringComparison.Ordinal);
        Assert.Contains("m-\tmargin\tmargin: 13px;\tmargin: var(--value);", catalog, StringComparison.Ordinal);
        Assert.Contains("grow\t\tflex-grow: 1;\t\n", catalog, StringComparison.Ordinal);
        Assert.Contains("bg-auto\t\tbackground-size: auto;\t\n", catalog, StringComparison.Ordinal);
        Assert.Contains("bg-clip-text\t\tbackground-clip: text;\tbg-clip-", catalog, StringComparison.Ordinal);
        Assert.Contains("bg-amber-\tbackground-color\t\t", catalog, StringComparison.Ordinal);
    }

    [Test]
    public async Task Compile_discovers_fecsx_and_cs_and_is_incremental()
    {
        using var fixture = new TemporaryTailwindFixture();
        var fecsx = fixture.Write("Components/Panel.fecsx", "<div className=\"bg-red-500\" />");
        var cs = fixture.Write("Components/Classes.cs", "const string ClassName = \"text-blue-500\";");
        var request = fixture.CreateRequest([fecsx, cs]);

        Assert.Equal(TailwindCompileResult.Built, await TailwindCompiler.CompileAsync(request));
        var css = await File.ReadAllTextAsync(request.OutputPath);
        Assert.Contains(".bg-red-500", css, StringComparison.Ordinal);
        Assert.Contains(".text-blue-500", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".m-96", css, StringComparison.Ordinal);
        Assert.True(css.Length < 20_000, "Release CSS should be tree-shaken and minified.");

        var timestamp = File.GetLastWriteTimeUtc(request.OutputPath);
        Assert.Equal(TailwindCompileResult.Skipped, await TailwindCompiler.CompileAsync(request));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(request.OutputPath));

        await File.AppendAllTextAsync(cs, "\nconst string Changed = \"m-96\";");
        Assert.Equal(TailwindCompileResult.Built, await TailwindCompiler.CompileAsync(request));
        Assert.Contains(".m-96", await File.ReadAllTextAsync(request.OutputPath), StringComparison.Ordinal);

        File.Delete(request.OutputPath);
        Assert.Equal(TailwindCompileResult.Built, await TailwindCompiler.CompileAsync(request));
    }

    [Test]
    public async Task Compile_rejects_sources_outside_the_project()
    {
        using var fixture = new TemporaryTailwindFixture();
        var outside = Path.GetTempFileName();
        try
        {
            var request = fixture.CreateRequest([outside]);
            var exception = await Assert.ThrowsAsync<TailwindBuildException>(
                () => TailwindCompiler.CompileAsync(request));
            Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Test]
    public async Task Packaged_payload_builds_without_package_manager_or_network_access()
    {
        using var fixture = new TemporaryTailwindFixture();
        var source = fixture.Write("Components/Offline.fecsx", "<div className=\"grid\" />");
        var archive = Path.Combine(AppContext.BaseDirectory, "TailwindTestPayload.zip");
        Assert.True(File.Exists(archive), "The build must prepare the packaged Tailwind payload fixture.");
        var request = fixture.CreateRequest([source]) with
        {
            ToolRoot = null,
            ToolArchive = archive
        };
        var originalRegistry = Environment.GetEnvironmentVariable("npm_config_registry");
        Environment.SetEnvironmentVariable("npm_config_registry", "http://127.0.0.1:1/");
        try
        {
            Assert.Equal(TailwindCompileResult.Built, await TailwindCompiler.CompileAsync(request));
            Assert.Contains(".grid", await File.ReadAllTextAsync(request.OutputPath), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("npm_config_registry", originalRegistry);
        }
    }

    private sealed class TemporaryTailwindFixture : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp.Tailwind.Tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryTailwindFixture() => Directory.CreateDirectory(root);

        public string Root => root;

        public string ToolRoot => Path.Combine(
            FindRepositoryRoot(), "Fecscn", "FrontEndCSharp.FECS.Internals.Tailwind", "JavaScriptPackages");

        public string Write(string relativePath, string content)
        {
            var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public TailwindCompileRequest CreateRequest(IReadOnlyList<string> sources) => new(
            ProjectDirectory: root,
            ToolRoot: ToolRoot,
            ToolArchive: null,
            CacheDirectory: Path.Combine(root, "obj", "fecscn", "tailwind"),
            ThemeInputPath: null,
            OutputPath: Path.Combine(root, "wwwroot", "css", "fecscn.css"),
            StatePath: Path.Combine(root, "obj", "fecscn", "tailwind.state"),
            Configuration: "Release",
            Sources: sources);

        public void Dispose() => Directory.Delete(root, recursive: true);

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string ReadGzipText(string path)
    {
        using var input = File.OpenRead(path);
        using var compressed = new System.IO.Compression.GZipStream(
            input,
            System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(compressed);
        return reader.ReadToEnd();
    }
}
