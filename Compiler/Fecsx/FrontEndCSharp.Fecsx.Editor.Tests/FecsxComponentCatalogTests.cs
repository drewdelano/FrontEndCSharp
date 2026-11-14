using System.Reflection;
using FrontEndCSharp.Fecsx.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxComponentCatalogTests
{
    [Test]
    public async Task ReadsSiblingGeneratedComponentsFromTheOwningProject()
    {
        const string appSource = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(AppProps props, ReactNode[] children) =>
                <main>{props.Title}</main>;
            """;
        const string taskCardSource = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(TaskCardProps props, ReactNode[] children) =>
                <article>{props.Name}</article>;
            """;

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FECSX component catalog", LanguageNames.CSharp)
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
                }
            }
            namespace Demo
            {
                public sealed class AppProps { public string Title { get; set; } = ""; }
                public sealed class TaskCardProps
                {
                    public string Name { get; set; } = "";
                    public int Priority { get; set; }
                }
            }
            """)).Project;
        project = project.AddAdditionalDocument(
            "App.fecsx",
            SourceText.From(appSource),
            filePath: "App.fecsx").Project;
        project = project.AddAdditionalDocument(
            "TaskCard.fecsx",
            SourceText.From(taskCardSource),
            filePath: "TaskCard.fecsx").Project;

        var owningCompilation = await project.GetCompilationAsync();

        Assert.NotNull(owningCompilation);
        var catalog = FecsxComponentCatalog.Create(owningCompilation!);
        Assert.Contains("TaskCard", catalog.ComponentNames);
        Assert.True(catalog.TryGetProperties("TaskCard", out var properties));
        Assert.Contains(properties, property =>
            property.Name == "Name" && property.ValueKind == FecsxCompletionValueKind.String);
        Assert.Contains(properties, property =>
            property.Name == "Priority" && property.ValueKind == FecsxCompletionValueKind.Number);

        var projection = FecsxCSharpProjector.Create(appSource);
        var projectedProject = FecsxCSharpProject.WithoutSourceGenerator(project)
            .AddDocument("App.fecsx.editor.g.cs", SourceText.From(projection.Text))
            .Project;
        var projectedCompilation = await projectedProject.GetCompilationAsync();

        Assert.NotNull(projectedCompilation);
        Assert.Null(projectedCompilation!.GetTypeByMetadataName("Demo.TaskCard"));
        Assert.DoesNotContain(projectedCompilation.GetDiagnostics(), diagnostic =>
            diagnostic.Id == "CS0101" || diagnostic.Id == "CS0229" || diagnostic.Id == "CS0119");
    }

    private sealed class TestAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }
}
