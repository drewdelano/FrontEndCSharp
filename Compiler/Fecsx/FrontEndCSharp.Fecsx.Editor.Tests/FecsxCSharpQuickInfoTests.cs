using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.Text;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxCSharpQuickInfoTests
{
    [Test]
    public async Task ProjectedQuickInfoBindsALocalPropsTypeAndMapsItsSpanBackToFecsx()
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
        var project = workspace.AddProject("FECSX local quick info", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        project = project.AddDocument("React.cs", SourceText.From("""
            namespace Interop.JS.React { public sealed class ReactNode { } }
            """)).Project;
        var document = project.AddDocument("App.fecsx.editor.g.cs", SourceText.From(projection.Text));
        var service = Assert.IsAssignableFrom<QuickInfoService>(QuickInfoService.GetService(document));

        var item = await service.GetQuickInfoAsync(document, projectedPosition);

        Assert.NotNull(item);
        Assert.Contains(item.Sections, section => section.Text.Contains("AppProps", StringComparison.Ordinal));
        Assert.True(projection.TryMapProjectedSpan(
            new FecsxTextSpan(item.Span.Start, item.Span.Length),
            out var sourceSpan));
        Assert.Equal("AppProps", source.Substring(sourceSpan.Start, sourceSpan.Length));
    }

    [Test]
    public async Task ProjectedQuickInfoBindsAPropsMemberAndMapsItsSpanBackToFecsx()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <main />;
            public static ReactNode Details(StoreProps props, ReactNode[] children) =>
                <section>{props.Store.EnergyLevel}</section>;
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var sourcePosition = source.IndexOf("EnergyLevel", StringComparison.Ordinal) + 2;
        Assert.True(projection.TryMapSourceCaretPosition(sourcePosition, out var projectedPosition));

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FECSX quick info", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        project = project.AddDocument("Support.cs", SourceText.From("""
            namespace Interop.JS.React { public sealed class ReactNode { } }
            namespace Demo
            {
                public sealed class StoreProps { public Store Store { get; } = new Store(); }
                public sealed class Store { public int EnergyLevel { get; set; } }
            }
            """)).Project;
        var document = project.AddDocument("Panel.fecsx.editor.g.cs", SourceText.From(projection.Text));
        var service = Assert.IsAssignableFrom<QuickInfoService>(QuickInfoService.GetService(document));

        var item = await service.GetQuickInfoAsync(document, projectedPosition);

        Assert.NotNull(item);
        Assert.Contains(item.Sections, section => section.Text.Contains("EnergyLevel", StringComparison.Ordinal));
        Assert.True(projection.TryMapProjectedSpan(
            new FecsxTextSpan(item.Span.Start, item.Span.Length),
            out var sourceSpan));
        Assert.Equal("EnergyLevel", source.Substring(sourceSpan.Start, sourceSpan.Length));
    }
}
