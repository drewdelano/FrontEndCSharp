using System.Collections.Immutable;
using System.Reflection;
using FrontEndCSharp.Fecsx.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxCSharpProjectTests
{
    [TestCase(false, null, false)]
    [TestCase(true, "FrontEndCSharp.Fecsx", false)]
    [TestCase(true, "Project analyzer", false)]
    [TestCase(true, "Project analyzer", true)]
    public async Task RemovesTheBuildGeneratorBeforeAddingAnAuthoredProjection(
        bool pathlessReference,
        string? display,
        bool opaqueId)
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;

            public sealed class AppProps
            {
                public OrbitStore Store { get; set; } = null!;
            }

            public static ReactNode Default(AppProps props, ReactNode[] children) =>
                <main>{props.Store}</main>;
            """;
        var projection = FecsxCSharpProjector.Create(source);

        using var workspace = new AdhocWorkspace();
        var analyzerReference = new AnalyzerFileReference(
            typeof(FecsxGenerator).Assembly.Location,
            new TestAnalyzerAssemblyLoader());
        var project = workspace.AddProject("FECSX projected compilation", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Func<>).Assembly.Location))
            .AddAnalyzerReference(pathlessReference
                ? new PathlessAnalyzerReference(analyzerReference, display!, opaqueId)
                : analyzerReference);
        project = project.AddDocument("React.cs", SourceText.From("""
            namespace Interop.JS.React
            {
                public class ReactNode { }
                public sealed class ReactElement : ReactNode { }
                public sealed class ReactComponent<T> { }
                public static class React
                {
                    public static ReactComponent<T> Component<T>(System.Func<T, ReactNode[], ReactNode> render) => new();
                    public static ReactElement CreateElement<T>(ReactComponent<T> component, T props, ReactNode[] children) => new();
                }
            }
            """)).Project;
        project = project.AddDocument(
            "OrbitStore.cs",
            SourceText.From("namespace Demo; public sealed class OrbitStore { }"))
            .Project;
        project = project.AddAdditionalDocument("App.fecsx", SourceText.From(source), filePath: "App.fecsx").Project;
        project = project.AddDocument("App.fecsx.editor.g.cs", SourceText.From(projection.Text)).Project;

        var duplicatedCompilation = await project.GetCompilationAsync();
        Assert.NotNull(duplicatedCompilation);
        Assert.Contains(duplicatedCompilation!.GetDiagnostics(), diagnostic => diagnostic.Id == "CS0101");

        var projectedProject = FecsxCSharpProject.WithoutSourceGenerator(project);
        var projectedCompilation = await projectedProject.GetCompilationAsync();

        Assert.NotNull(projectedCompilation);
        Assert.DoesNotContain(projectedCompilation!.GetDiagnostics(), diagnostic =>
            diagnostic.Id is "CS0101" or "CS0229" or "CS0119");
        Assert.DoesNotContain(projectedProject.AnalyzerReferences, reference =>
            string.Equals(
                Path.GetFileNameWithoutExtension(reference.FullPath),
                "FrontEndCSharp.Fecsx",
                StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TestAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }

    private sealed class PathlessAnalyzerReference : AnalyzerReference
    {
        private readonly AnalyzerReference _inner;

        private readonly string _display;

        private readonly bool _opaqueId;

        public PathlessAnalyzerReference(AnalyzerReference inner, string display, bool opaqueId = false)
        {
            _inner = inner;
            _display = display;
            _opaqueId = opaqueId;
        }

        public override string FullPath => null!;
        public override string Display => _display;
        public override object Id => _opaqueId ? this : _inner.Id;

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) =>
            _inner.GetAnalyzers(language);

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() =>
            _inner.GetAnalyzersForAllLanguages();

        public override ImmutableArray<ISourceGenerator> GetGenerators(string language) =>
            _inner.GetGenerators(language);

        public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() =>
            _inner.GetGeneratorsForAllLanguages();
    }
}
