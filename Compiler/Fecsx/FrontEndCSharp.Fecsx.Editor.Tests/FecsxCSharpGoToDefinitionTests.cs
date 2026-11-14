using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxCSharpGoToDefinitionTests
{
    [Test]
    public async Task ProjectedLocalPropsTypeDefinitionMapsBackToTheSameFecsxFile()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public sealed class AppProps { }
            public static ReactNode Default(AppProps props, ReactNode[] children) =>
                <main />;
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var sourcePosition = source.LastIndexOf("AppProps", StringComparison.Ordinal) + 2;
        Assert.True(projection.TryMapSourceCaretPosition(sourcePosition, out var projectedPosition));

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FECSX local definition", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        project = project.AddDocument("React.cs", SourceText.From("""
            namespace Interop.JS.React { public sealed class ReactNode { } }
            """)).Project;
        project = project.AddDocument(
            "App.fecsx.g.cs",
            SourceText.From("namespace Demo; public sealed class AppProps { }"),
            filePath: "/generated/App.fecsx.g.cs").Project;
        var projectedDocument = project.AddDocument(
            "App.fecsx.editor.g.cs",
            SourceText.From(projection.Text),
            filePath: "/src/App.fecsx.editor.g.cs");
        var semanticModel = await projectedDocument.GetSemanticModelAsync();
        Assert.NotNull(semanticModel);

        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(
            semanticModel,
            projectedPosition,
            workspace);

        Assert.NotNull(symbol);
        symbol = await SymbolFinder.FindSourceDefinitionAsync(
            symbol,
            projectedDocument.Project.Solution) ?? symbol;
        Assert.Contains(symbol.Locations, item => item.SourceTree?.FilePath == "/generated/App.fecsx.g.cs");
        var location = Assert.Single(
            symbol.Locations,
            item => item.SourceTree?.FilePath == "/src/App.fecsx.editor.g.cs");
        Assert.Equal("/src/App.fecsx.editor.g.cs", location.SourceTree?.FilePath);
        Assert.True(projection.TryMapProjectedSpan(
            new FecsxTextSpan(location.SourceSpan.Start, location.SourceSpan.Length),
            out var sourceSpan));
        Assert.Equal("AppProps", source.Substring(sourceSpan.Start, sourceSpan.Length));
        Assert.Equal(source.IndexOf("AppProps", StringComparison.Ordinal), sourceSpan.Start);
    }

    [Test]
    public async Task ProjectedPropsMemberResolvesToItsAuthoredCSharpDefinition()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <main />;
            public static ReactNode Details(StoreProps props, ReactNode[] children) =>
                <section>{props.Store.EnergyLevel}</section>;
            """;
        const string support = """
            namespace Interop.JS.React { public sealed class ReactNode { } }
            namespace Demo
            {
                public sealed class StoreProps { public Store Store { get; } = new Store(); }
                public sealed class Store { public int EnergyLevel { get; set; } }
            }
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var sourcePosition = source.LastIndexOf("EnergyLevel", StringComparison.Ordinal) + 2;
        Assert.True(projection.TryMapSourceCaretPosition(sourcePosition, out var projectedPosition));

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FECSX definition", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        project = project.AddDocument("Support.cs", SourceText.From(support), filePath: "/src/Support.cs").Project;
        var projectedDocument = project.AddDocument(
            "Panel.fecsx.editor.g.cs",
            SourceText.From(projection.Text),
            filePath: "/src/Panel.fecsx.editor.g.cs");
        var semanticModel = await projectedDocument.GetSemanticModelAsync();
        Assert.NotNull(semanticModel);

        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(
            semanticModel,
            projectedPosition,
            workspace);
        Assert.NotNull(symbol);
        var definition = await SymbolFinder.FindSourceDefinitionAsync(
            symbol,
            projectedDocument.Project.Solution);

        var location = Assert.Single((definition ?? symbol).Locations, item => item.IsInSource);
        Assert.Equal("/src/Support.cs", location.SourceTree?.FilePath);
        var span = location.SourceSpan;
        Assert.Equal("EnergyLevel", support.Substring(span.Start, span.Length));
    }
}
