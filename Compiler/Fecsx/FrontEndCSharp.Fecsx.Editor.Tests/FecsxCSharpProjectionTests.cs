using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxCSharpProjectionTests
{
    [Test]
    public void ProjectsPreambleAndComponentDeclarationWithoutProjectingMarkup()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <section>{props.Store.EnergyLevel}</section>;
            """;

        var projection = FecsxCSharpProjector.Create(source);

        foreach (var text in new[] { "using", "Interop.JS.React", "ReactNode", "Default", "StoreProps", "props" })
        {
            var sourcePosition = source.IndexOf(text, StringComparison.Ordinal);
            Assert.True(projection.TryMapSourcePosition(sourcePosition, out var projectedPosition));
            Assert.Equal(text, projection.Text.Substring(projectedPosition, text.Length));
        }

        Assert.False(projection.TryMapSourcePosition(source.IndexOf("section", StringComparison.Ordinal), out _));
        var propsSourceStart = source.IndexOf("props.Store", StringComparison.Ordinal);
        Assert.True(projection.TryMapSourcePosition(propsSourceStart, out var propsProjectedStart));
        Assert.True(projection.TryMapProjectedSpan(
            new FecsxTextSpan(propsProjectedStart, "props.Store".Length),
            out var propsSourceSpan));
        Assert.Equal(new FecsxTextSpan(propsSourceStart, "props.Store".Length), propsSourceSpan);
        Assert.False(projection.TryMapProjectedSpan(
            new FecsxTextSpan(projection.Segments[0].ProjectedSpan.End - 1, 2),
            out _));
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(projection.Text).GetDiagnostics(),
            diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Test]
    public void ProjectsEveryEmbeddedExpressionIntoParseableCSharpWithBidirectionalMappings()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(Props props, ReactNode[] children) =>
                <TaskCard Name={props.Name}>{children}</TaskCard>;
            """;

        var projection = FecsxCSharpProjector.Create(source);

        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(projection.Text).GetDiagnostics(),
            diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Equal(
            new[] { "props.Name", "children" },
            projection.Segments
                .Select(segment => source.Substring(segment.SourceSpan.Start, segment.SourceSpan.Length))
                .Where(text => text == "props.Name" || text == "children"));
        foreach (var segment in projection.Segments)
        {
            var sourceText = source.Substring(segment.SourceSpan.Start, segment.SourceSpan.Length);
            var projectedText = projection.Text.Substring(segment.ProjectedSpan.Start, segment.ProjectedSpan.Length);
            Assert.Equal(sourceText, projectedText);
            Assert.True(projection.TryMapSourcePosition(segment.SourceSpan.Start + 1, out var projected));
            Assert.Equal(segment.ProjectedSpan.Start + 1, projected);
            Assert.True(projection.TryMapProjectedPosition(projected, out var roundTrip));
            Assert.Equal(segment.SourceSpan.Start + 1, roundTrip);
            if (!projection.Segments.Any(other => other.SourceSpan.Start == segment.SourceSpan.End))
            {
                Assert.False(projection.TryMapSourcePosition(segment.SourceSpan.End, out _));
            }
            Assert.False(projection.TryMapProjectedPosition(segment.ProjectedSpan.End, out _));
        }
    }

    [Test]
    public void ProjectsAWrappedMarkupLambdaAsParseableCSharp()
    {
        const string source = """
            using Interop.JS.MobX.React;
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                MobXReact.Observe(() =>
                    <section>{props.Store.EnergyLevel}</section>
                );
            """;

        var projection = FecsxCSharpProjector.Create(source);

        Assert.Contains("MobXReact.Observe(() =>", projection.Text, StringComparison.Ordinal);
        Assert.Contains("default!", projection.Text, StringComparison.Ordinal);
        var propsSourceStart = source.IndexOf("props.Store", StringComparison.Ordinal);
        Assert.True(projection.TryMapSourcePosition(propsSourceStart, out var propsProjectedStart));
        Assert.Equal("props.Store", projection.Text.Substring(propsProjectedStart, "props.Store".Length));
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(projection.Text).GetDiagnostics(),
            diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Test]
    public void MapsCompletionCaretsAtTheEndOfCSharpSegmentsButNotInsideMarkup()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(Props props, ReactNode[] children) =>
                <section>{props.Store}</section>;
            """;

        var projection = FecsxCSharpProjector.Create(source);
        var expressionEnd = source.IndexOf("props.Store", StringComparison.Ordinal) + "props.Store".Length;

        Assert.True(projection.TryMapSourceCaretPosition(expressionEnd, out var projectedExpressionEnd));
        Assert.Equal("props.Store", projection.Text.Substring(projectedExpressionEnd - "props.Store".Length, "props.Store".Length));
        Assert.True(projection.TryMapSourceCaretPosition(source.IndexOf("ReactNode Default", StringComparison.Ordinal), out _));
        Assert.False(projection.TryMapSourceCaretPosition(source.IndexOf("section", StringComparison.Ordinal), out _));
    }

    [Test]
    public void MapsACompletionReplacementAndNamespaceImportBackToTheAuthoredDocument()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(Props props, ReactNode[] children) =>
                <section>{Lis}</section>;
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var sourceIdentifier = source.IndexOf("Lis", StringComparison.Ordinal);
        Assert.True(projection.TryMapSourcePosition(sourceIdentifier, out var projectedIdentifier));
        var projectedEdits = new[]
        {
            new FecsxTextEdit(new FecsxTextSpan(0, 0), "using System.Collections.Generic;\n"),
            new FecsxTextEdit(new FecsxTextSpan(projectedIdentifier, 3), "List"),
        };

        var projectedCaretPosition = projectedIdentifier + projectedEdits[0].NewText.Length + "List".Length;
        Assert.True(projection.TryMapProjectedEdits(
            projectedEdits,
            projectedCaretPosition,
            out var sourceEdits,
            out var sourceCaretPosition));

        Assert.Equal(new FecsxTextSpan(0, 0), sourceEdits[0].Span);
        Assert.Equal(new FecsxTextSpan(sourceIdentifier, 3), sourceEdits[1].Span);
        Assert.Equal(projectedEdits.Select(edit => edit.NewText), sourceEdits.Select(edit => edit.NewText));
        Assert.Equal(sourceIdentifier + projectedEdits[0].NewText.Length + "List".Length, sourceCaretPosition);
    }

    [Test]
    public void RejectsACompletionEditThatTouchesProjectionOnlyScaffolding()
    {
        const string source = """
            namespace Demo;
            public static ReactNode Default(Props props, ReactNode[] children) =>
                <section>{props.Name}</section>;
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var scaffold = projection.Text.IndexOf("__FecsxEditorProjection", StringComparison.Ordinal);

        Assert.False(projection.TryMapProjectedEdits(
            new[] { new FecsxTextEdit(new FecsxTextSpan(scaffold, 1), "X") },
            out var sourceEdits));
        Assert.Empty(sourceEdits);
    }

    [Test]
    public void ProjectsDefaultAndNamedComponentDeclarationsAndExpressions()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(AppProps props, ReactNode[] children)
            {
                return <main>{props.Title}</main>;
            }
            public static ReactNode DarkMode(ThemeProps theme, ReactNode[] children) =>
                <aside>{theme.Contrast}</aside>;
            """;

        var projection = FecsxCSharpProjector.Create(source);

        Assert.Contains("ReactNode Default(AppProps props", projection.Text, StringComparison.Ordinal);
        Assert.Contains("return default!;", projection.Text, StringComparison.Ordinal);
        Assert.Contains("ReactNode DarkMode(ThemeProps theme", projection.Text, StringComparison.Ordinal);
        foreach (var expression in new[] { "props.Title", "theme.Contrast" })
        {
            var sourcePosition = source.IndexOf(expression, StringComparison.Ordinal);
            Assert.True(projection.TryMapSourcePosition(sourcePosition, out var projectedPosition));
            Assert.Equal(expression, projection.Text.Substring(projectedPosition, expression.Length));
        }
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(projection.Text).GetDiagnostics(),
            diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }
}
