using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using System.Collections.ObjectModel;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxCSharpCompletionTests
{
    [Test]
    public async Task ProjectedCompletionUsesTheNamedComponentsOwnPropsType()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(AppProps props, ReactNode[] children) =>
                <main>{props.Title}</main>;
            public static ReactNode DarkMode(ThemeProps theme, ReactNode[] children) =>
                <aside>{theme.}</aside>;
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var sourcePosition = source.IndexOf("theme.", StringComparison.Ordinal) + "theme.".Length;
        Assert.True(projection.TryMapSourceCaretPosition(sourcePosition, out var projectedPosition));

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FECSX named completion", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        project = project.AddDocument("Support.cs", SourceText.From("""
            namespace Interop.JS.React { public sealed class ReactNode { } }
            namespace Demo
            {
                public sealed class AppProps { public string Title { get; set; } = ""; }
                public sealed class ThemeProps { public int Contrast { get; set; } }
            }
            """)).Project;
        var document = project.AddDocument("App.fecsx.editor.g.cs", SourceText.From(projection.Text));
        var service = Assert.IsAssignableFrom<CompletionService>(CompletionService.GetService(document));

        var completions = await service.GetCompletionsAsync(document, projectedPosition);

        Assert.NotNull(completions);
        Assert.Contains(completions.ItemsList, item => item.DisplayText == "Contrast");
        Assert.DoesNotContain(completions.ItemsList, item => item.DisplayText == "Title");
    }

    [TestCase("props.Store.", "EnergyLevel")]
    [TestCase("Task", "TaskCard")]
    [TestCase("FrontEndCSharp.", "ReactDemo")]
    [TestCase("def", "default")]
    public async Task ProjectedCompletionSeesMembersSiblingComponentsAndNamespaces(
        string expression,
        string expected)
    {
        var source = $$"""
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <section>{ {{expression}} }</section>;
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var sourcePosition = source.IndexOf(expression, StringComparison.Ordinal) + expression.Length;
        Assert.True(projection.TryMapSourceCaretPosition(sourcePosition, out var projectedPosition));

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FECSX completion", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        project = project.AddDocument("Support.cs", SourceText.From("""
            namespace Interop.JS.React { public sealed class ReactNode { } }
            namespace Demo
            {
                public sealed class StoreProps { public Store Store { get; } = new Store(); }
                public sealed class Store { public int EnergyLevel { get; set; } }
                public static class TaskCard { public static object Create(System.Action<StoreProps> configure, params Interop.JS.React.ReactNode[] children) => new object(); }
            }
            namespace FrontEndCSharp.ReactDemo { public sealed class Marker { } }
            """)).Project;
        var document = project.AddDocument("Panel.fecsx.editor.g.cs", SourceText.From(projection.Text));
        var service = Assert.IsAssignableFrom<CompletionService>(CompletionService.GetService(document));

        var completions = await service.GetCompletionsAsync(document, projectedPosition);

        Assert.NotNull(completions);
        Assert.Contains(completions.ItemsList, item => item.DisplayText == expected);
    }

    [Test]
    public async Task RoslynImportCompletionMapsBothTheUsingAndReplacementIntoFecsx()
    {
        const string expression = "new ObservableCollectio";
        var source = $$"""
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <section>{ {{expression}} }</section>;
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var sourcePosition = source.IndexOf(expression, StringComparison.Ordinal) + expression.Length;
        Assert.True(projection.TryMapSourceCaretPosition(sourcePosition, out var projectedPosition));

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FECSX import completion", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(ObservableCollection<>).Assembly.Location));
        project = project.AddDocument("Support.cs", SourceText.From("""
            namespace Interop.JS.React { public sealed class ReactNode { } }
            namespace Demo { public sealed class StoreProps { } }
            """)).Project;
        var document = project.AddDocument("Panel.fecsx.editor.g.cs", SourceText.From(projection.Text));
        var service = Assert.IsAssignableFrom<CompletionService>(CompletionService.GetService(document));
        var completions = await service.GetCompletionsAsync(document, projectedPosition);

        var item = Assert.Single(completions!.ItemsList, item => item.DisplayText == "ObservableCollection");
        var change = await service.GetChangeAsync(document, item);
        var changes = change.TextChanges.IsDefaultOrEmpty
            ? [change.TextChange]
            : change.TextChanges;
        var projectedEdits = changes
            .Select(textChange => new FecsxTextEdit(
                new FecsxTextSpan(textChange.Span.Start, textChange.Span.Length),
                textChange.NewText ?? string.Empty))
            .ToArray();
        Assert.True(FecsxCSharpCompletionEdits.TryGetPrimaryEdit(
            projectedEdits,
            new FecsxTextSpan(item.Span.Start, item.Span.Length),
            out var primaryEdit));
        Assert.NotNull(primaryEdit);
        Assert.Equal("ObservableCollection", primaryEdit.NewText);
        var projectedCaretPosition = change.NewPosition ??
            primaryEdit.Span.Start + primaryEdit.NewText.Length +
                projectedEdits
                    .Where(edit => edit.Span.Start < primaryEdit.Span.Start)
                    .Sum(edit => edit.NewText.Length - edit.Span.Length);

        Assert.True(projection.TryMapProjectedEdits(
            projectedEdits,
            projectedCaretPosition,
            out var sourceEdits,
            out var sourceCaretPosition));
        Assert.Contains(sourceEdits, edit => edit.NewText.Contains("System.Collections.ObjectModel", StringComparison.Ordinal));
        Assert.Contains(sourceEdits, edit => edit.NewText == "ObservableCollection");
        var updatedSource = source;
        foreach (var edit in sourceEdits.OrderByDescending(edit => edit.Span.Start))
        {
            updatedSource = updatedSource.Remove(edit.Span.Start, edit.Span.Length)
                .Insert(edit.Span.Start, edit.NewText);
        }
        Assert.Equal(
            updatedSource.IndexOf("ObservableCollection", StringComparison.Ordinal) + "ObservableCollection".Length,
            sourceCaretPosition);
    }
}
