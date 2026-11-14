using System.Reflection;
using FrontEndCSharp.Fecsx.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxComponentNavigationTests
{
    private const string Source = """
        using Interop.JS.React;
        namespace Demo;

        public static ReactNode Default(AppProps props, ReactNode[] children) =>
            <main>
                <TaskCard />
                <App.Footer></App.Footer>
            </main>;

        public static ReactNode Footer(AppProps props, ReactNode[] children) =>
            <footer />;
        """;

    [TestCase("<TaskCard", "TaskCard")]
    [TestCase("<App.Footer", "App.Footer")]
    [TestCase("</App.Footer", "App.Footer")]
    public void FindsManagedComponentTagsAtTheCaret(string marker, string expected)
    {
        var position = Source.IndexOf(marker, StringComparison.Ordinal) + marker.Length - 2;

        var found = FecsxComponentNavigation.TryGetManagedTag(
            Source,
            position,
            out var tagName);

        Assert.True(found);
        Assert.Equal(expected, tagName);
    }

    [TestCase("<main", "main")]
    [TestCase("props", "props")]
    public void IgnoresIntrinsicTagsAndOrdinaryCSharp(string marker, string expected)
    {
        var position = Source.IndexOf(marker, StringComparison.Ordinal) + 1;

        Assert.False(FecsxComponentNavigation.TryGetManagedTag(
            Source,
            position,
            out var tagName));
        Assert.Equal(string.Empty, tagName);
        Assert.Equal(expected, marker.TrimStart('<'));
    }

    [Test]
    public void MapsGeneratedFacadeTypesToDefaultAndNamedAuthoredDeclarations()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            namespace Demo;
            public static class App
            {
                public static object Component => null!;
                public static class Footer
                {
                    public static object Component => null!;
                }
            }
            """);
        var compilation = CSharpCompilation.Create("Navigation", new[] { tree });
        var app = compilation.GetTypeByMetadataName("Demo.App");
        var footer = app?.GetTypeMembers("Footer").SingleOrDefault();
        Assert.NotNull(app);
        Assert.NotNull(footer);

        Assert.True(FecsxComponentNavigation.TryGetDeclaration(
            app!,
            "C:/src/App.fecsx",
            Source,
            out var defaultSpan));
        Assert.Equal("Default", Source.Substring(defaultSpan.Start, defaultSpan.Length));

        Assert.True(FecsxComponentNavigation.TryGetDeclaration(
            footer!,
            "C:/src/App.fecsx",
            Source,
            out var footerSpan));
        Assert.Equal("Footer", Source.Substring(footerSpan.Start, footerSpan.Length));

        Assert.False(FecsxComponentNavigation.TryGetDeclaration(
            footer!,
            "C:/src/Other.fecsx",
            Source,
            out _));
    }

    [Test]
    public void TypeResolutionProbeKeepsOnlyTheAuthoredImportsAndNamespace()
    {
        var probe = FecsxComponentNavigation.CreateTypeResolutionProbe(
            Source,
            "App.Footer",
            out var tagSpan);

        Assert.Contains("using Interop.JS.React;", probe);
        Assert.Contains("namespace Demo;", probe);
        Assert.Contains("_ = App.Footer.Component;", probe);
        Assert.DoesNotContain("class AppProps", probe);
        var root = CSharpSyntaxTree.ParseText(probe).GetRoot();
        var tag = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(
            tagSpan.Start,
            tagSpan.Length));
        Assert.IsAssignableFrom<ExpressionSyntax>(tag);
        Assert.Equal("App.Footer", tag.ToString());
    }

    [TestCase("TaskCard", "TaskCard.fecsx", "Default")]
    [TestCase("App.Footer", "App.fecsx", "Footer")]
    public async Task GeneratedComponentTypeResolvesBackToItsAuthoredDeclaration(
        string tagName,
        string expectedFile,
        string expectedDeclaration)
    {
        const string taskCardSource = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(AppProps props, ReactNode[] children) =>
                <article />;
            """;
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Component navigation", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Func<>).Assembly.Location))
            .AddAnalyzerReference(new AnalyzerFileReference(
                typeof(FecsxGenerator).Assembly.Location,
                new TestAnalyzerAssemblyLoader()));
        project = project.AddDocument("Support.cs", SourceText.From("""
            namespace Interop.JS.React
            {
                public class ReactNode { }
                public sealed class ReactElement : ReactNode { }
                public sealed class ReactComponent<T> { }
                public static class React
                {
                    public static ReactComponent<T> Component<T>(System.Func<T, ReactNode[], ReactNode> render) => new();
                    public static ReactElement CreateElement<T>(ReactComponent<T> component, T props, ReactNode[] children) => new();
                    public static ReactElement CreateElement(string name, object? props, ReactNode[] children) => new();
                }
            }
            """)).Project;
        project = project.AddAdditionalDocument(
            "App.fecsx",
            SourceText.From(Source),
            filePath: "C:/src/App.fecsx").Project;
        project = project.AddAdditionalDocument(
            "TaskCard.fecsx",
            SourceText.From(taskCardSource),
            filePath: "C:/src/TaskCard.fecsx").Project;
        var probe = FecsxComponentNavigation.CreateTypeResolutionProbe(
            Source,
            tagName,
            out var tagSpan);
        var probeDocument = project.AddDocument("Probe.cs", SourceText.From(probe));

        var model = await probeDocument.GetSemanticModelAsync();
        var root = await probeDocument.GetSyntaxRootAsync();
        Assert.NotNull(model);
        Assert.NotNull(root);
        var tagNode = root!.FindNode(new TextSpan(tagSpan.Start, tagSpan.Length));
        var componentType = model!.GetSymbolInfo(tagNode).Symbol as INamedTypeSymbol;
        Assert.NotNull(componentType);

        var target = project.AdditionalDocuments.Single(document => document.Name == expectedFile);
        var targetSource = (await target.GetTextAsync()).ToString();
        Assert.True(FecsxComponentNavigation.TryGetDeclaration(
            componentType!,
            target.FilePath!,
            targetSource,
            out var declarationSpan));
        Assert.Equal(
            expectedDeclaration,
            targetSource.Substring(declarationSpan.Start, declarationSpan.Length));
    }

    private sealed class TestAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }
}
