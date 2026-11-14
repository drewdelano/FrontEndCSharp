using FrontEndCSharp.Fecsx.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxCSharpRenameTests
{
    [Test]
    public async Task PropsTypeRenameIgnoresTheGeneratedRenderSignature()
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
        Assert.True(projection.TryMapSourceCaretPosition(
            source.IndexOf("StoreProps", StringComparison.Ordinal) + 2,
            out var projectedPosition));

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FECSX props rename", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        project = project.AddDocument("Support.cs", SourceText.From(support)).Project;
        var document = project.AddDocument("First.fecsx.editor.g.cs", SourceText.From(projection.Text));
        var semanticModel = await document.GetSemanticModelAsync();
        Assert.NotNull(semanticModel);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, projectedPosition, workspace);
        Assert.NotNull(symbol);

        var changedSolution = await Renamer.RenameSymbolAsync(
            document.Project.Solution,
            symbol,
            new SymbolRenameOptions(),
            "RenamedProps",
            CancellationToken.None);
        var changes = await changedSolution.GetDocument(document.Id)!.GetTextChangesAsync(document);

        Assert.Equal(4, changes.Count());
        Assert.True(FecsxCSharpRenameEdits.TryMapChanges(projection, changes, out var edits));
        Assert.Equal(2, edits.Count);
        var updated = FecsxCSharpRenameEdits.Apply(source, edits);
        Assert.Contains("Default(RenamedProps props", updated);
        Assert.Contains("Details(RenamedProps props", updated);
    }

    [Test]
    public void RenameRejectsAnEditCrossingAnAuthoredProjectionBoundary()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <section />;
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var declaration = projection.Segments[1].ProjectedSpan;
        var crossingChange = new TextChange(
            new TextSpan(declaration.End - 1, 2),
            "x");

        Assert.False(FecsxCSharpRenameEdits.TryMapChanges(
            projection,
            new[] { crossingChange },
            out var edits));
        Assert.Empty(edits);
    }

    [Test]
    public async Task RoslynRenameMapsEveryProjectedEditBackToMultipleFecsxDocuments()
    {
        const string firstSource = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <section>{props.Store.EnergyLevel}</section>;
            """;
        const string secondSource = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <aside>{props.Store.EnergyLevel}</aside>;
            """;
        const string support = """
            namespace Interop.JS.React { public sealed class ReactNode { } }
            namespace Demo
            {
                public sealed class StoreProps { public Store Store { get; } = new Store(); }
                public sealed class Store { public int EnergyLevel { get; set; } }
            }
            """;
        var firstProjection = FecsxCSharpProjector.Create(firstSource);
        var secondProjection = FecsxCSharpProjector.Create(secondSource);
        Assert.True(firstProjection.TryMapSourceCaretPosition(
            firstSource.IndexOf("EnergyLevel", StringComparison.Ordinal) + 2,
            out var projectedPosition));

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FECSX rename", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        project = project.AddDocument("Support.cs", SourceText.From(support)).Project;
        var firstDocument = project.AddDocument("First.fecsx.editor.g.cs", SourceText.From(firstProjection.Text));
        var secondDocument = firstDocument.Project.AddDocument("Second.fecsx.editor.g.cs", SourceText.From(secondProjection.Text));
        firstDocument = secondDocument.Project.GetDocument(firstDocument.Id)!;
        var semanticModel = await firstDocument.GetSemanticModelAsync();
        Assert.NotNull(semanticModel);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, projectedPosition, workspace);
        Assert.NotNull(symbol);

        var changedSolution = await Renamer.RenameSymbolAsync(
            secondDocument.Project.Solution,
            symbol,
            new SymbolRenameOptions(),
            "PowerLevel",
            CancellationToken.None);

        var changedFirst = changedSolution.GetDocument(firstDocument.Id)!;
        var changedSecond = changedSolution.GetDocument(secondDocument.Id)!;
        var firstChanges = await changedFirst.GetTextChangesAsync(firstDocument);
        var secondChanges = await changedSecond.GetTextChangesAsync(secondDocument);
        Assert.True(FecsxCSharpRenameEdits.TryMapChanges(
            firstProjection,
            firstChanges,
            out var firstEdits), string.Join(", ", firstChanges.Select(change => change.ToString())));
        Assert.True(FecsxCSharpRenameEdits.TryMapChanges(
            secondProjection,
            secondChanges,
            out var secondEdits));

        Assert.Contains("props.Store.PowerLevel", FecsxCSharpRenameEdits.Apply(firstSource, firstEdits));
        Assert.Contains("props.Store.PowerLevel", FecsxCSharpRenameEdits.Apply(secondSource, secondEdits));
    }
}
