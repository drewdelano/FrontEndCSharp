using FrontEndCSharp.Fecsx.Syntax;
using System.Text;

namespace FrontEndCSharp.Fecsx.Editor;

public sealed class FecsxProjectionSegment
{
    public FecsxProjectionSegment(FecsxTextSpan sourceSpan, FecsxTextSpan projectedSpan)
    {
        SourceSpan = sourceSpan;
        ProjectedSpan = projectedSpan;
    }

    public FecsxTextSpan SourceSpan { get; }
    public FecsxTextSpan ProjectedSpan { get; }
}

public sealed class FecsxCSharpProjection
{
    internal FecsxCSharpProjection(string text, IReadOnlyList<FecsxProjectionSegment> segments)
    {
        Text = text;
        Segments = segments;
    }

    public string Text { get; }
    public IReadOnlyList<FecsxProjectionSegment> Segments { get; }

    public bool TryMapSourcePosition(int sourcePosition, out int projectedPosition) =>
        TryMap(sourcePosition, segment => segment.SourceSpan, segment => segment.ProjectedSpan, out projectedPosition);

    public bool TryMapSourceCaretPosition(int sourcePosition, out int projectedPosition)
    {
        if (TryMapSourcePosition(sourcePosition, out projectedPosition))
        {
            return true;
        }

        foreach (var segment in Segments)
        {
            if (sourcePosition == segment.SourceSpan.End)
            {
                projectedPosition = segment.ProjectedSpan.End;
                return true;
            }
        }

        projectedPosition = 0;
        return false;
    }

    public bool TryMapProjectedPosition(int projectedPosition, out int sourcePosition) =>
        TryMap(projectedPosition, segment => segment.ProjectedSpan, segment => segment.SourceSpan, out sourcePosition);

    public bool TryMapProjectedCaretPosition(int projectedPosition, out int sourcePosition)
    {
        if (TryMapProjectedPosition(projectedPosition, out sourcePosition))
        {
            return true;
        }

        foreach (var segment in Segments)
        {
            if (projectedPosition == segment.ProjectedSpan.End)
            {
                sourcePosition = segment.SourceSpan.End;
                return true;
            }
        }

        sourcePosition = 0;
        return false;
    }

    public bool TryMapProjectedSpan(FecsxTextSpan projectedSpan, out FecsxTextSpan sourceSpan)
    {
        foreach (var segment in Segments)
        {
            if (segment.ProjectedSpan.Start <= projectedSpan.Start &&
                projectedSpan.End <= segment.ProjectedSpan.End)
            {
                sourceSpan = new FecsxTextSpan(
                    segment.SourceSpan.Start + projectedSpan.Start - segment.ProjectedSpan.Start,
                    projectedSpan.Length);
                return true;
            }
        }

        sourceSpan = default;
        return false;
    }

    public bool TryMapProjectedEdits(
        IReadOnlyList<FecsxTextEdit> projectedEdits,
        out IReadOnlyList<FecsxTextEdit> sourceEdits)
    {
        if (projectedEdits == null) throw new ArgumentNullException(nameof(projectedEdits));
        var mapped = new List<FecsxTextEdit>(projectedEdits.Count);
        foreach (var edit in projectedEdits)
        {
            if (!TryMapProjectedSpan(edit.Span, out var sourceSpan))
            {
                sourceEdits = Array.Empty<FecsxTextEdit>();
                return false;
            }

            mapped.Add(new FecsxTextEdit(sourceSpan, edit.NewText));
        }

        sourceEdits = mapped.AsReadOnly();
        return true;
    }

    public bool TryMapProjectedEdits(
        IReadOnlyList<FecsxTextEdit> projectedEdits,
        int? postEditProjectedCaretPosition,
        out IReadOnlyList<FecsxTextEdit> sourceEdits,
        out int? postEditSourceCaretPosition)
    {
        if (!TryMapProjectedEdits(projectedEdits, out sourceEdits))
        {
            postEditSourceCaretPosition = null;
            return false;
        }

        if (postEditProjectedCaretPosition is not int projectedCaretPosition)
        {
            postEditSourceCaretPosition = null;
            return true;
        }

        var originalProjectedCaretPosition = MapPostEditPositionToOriginal(
            projectedEdits,
            projectedCaretPosition);
        if (!TryMapProjectedCaretPosition(originalProjectedCaretPosition, out var originalSourceCaretPosition))
        {
            sourceEdits = Array.Empty<FecsxTextEdit>();
            postEditSourceCaretPosition = null;
            return false;
        }

        postEditSourceCaretPosition = MapOriginalPositionToPostEdit(
            sourceEdits,
            originalSourceCaretPosition);
        return true;
    }

    private bool TryMap(
        int position,
        Func<FecsxProjectionSegment, FecsxTextSpan> from,
        Func<FecsxProjectionSegment, FecsxTextSpan> to,
        out int mapped)
    {
        foreach (var segment in Segments)
        {
            var source = from(segment);
            if (source.Start <= position && position < source.End)
            {
                mapped = to(segment).Start + position - source.Start;
                return true;
            }
        }

        mapped = 0;
        return false;
    }

    private static int MapPostEditPositionToOriginal(
        IReadOnlyList<FecsxTextEdit> edits,
        int postEditPosition)
    {
        var cumulativeDelta = 0;
        foreach (var edit in edits.OrderBy(edit => edit.Span.Start))
        {
            var postEditStart = edit.Span.Start + cumulativeDelta;
            if (postEditPosition < postEditStart)
            {
                break;
            }

            var postEditEnd = postEditStart + edit.NewText.Length;
            if (postEditPosition <= postEditEnd)
            {
                return edit.Span.Start + Math.Min(postEditPosition - postEditStart, edit.Span.Length);
            }

            cumulativeDelta += edit.NewText.Length - edit.Span.Length;
        }

        return postEditPosition - cumulativeDelta;
    }

    private static int MapOriginalPositionToPostEdit(
        IReadOnlyList<FecsxTextEdit> edits,
        int originalPosition)
    {
        var mapped = originalPosition;
        foreach (var edit in edits.OrderBy(edit => edit.Span.Start))
        {
            if (originalPosition < edit.Span.Start)
            {
                break;
            }

            if (originalPosition <= edit.Span.End)
            {
                return mapped - Math.Min(originalPosition - edit.Span.Start, edit.Span.Length) + edit.NewText.Length;
            }

            mapped += edit.NewText.Length - edit.Span.Length;
        }

        return mapped;
    }
}

public static class FecsxCSharpCompletionEdits
{
    public static bool TryGetPrimaryEdit(
        IReadOnlyList<FecsxTextEdit> edits,
        FecsxTextSpan completionSpan,
        out FecsxTextEdit? primaryEdit)
    {
        if (edits == null) throw new ArgumentNullException(nameof(edits));

        var candidates = edits
            .Where(edit => edit.Span.Start <= completionSpan.End && completionSpan.Start <= edit.Span.End)
            .ToArray();
        if (candidates.Length == 1)
        {
            primaryEdit = candidates[0];
            return true;
        }

        primaryEdit = default;
        return false;
    }
}

public static class FecsxCSharpProjector
{
    public static FecsxCSharpProjection Create(string source) =>
        Create(source, "__FecsxEditorProjection");

    public static FecsxCSharpProjection Create(string source, string projectionTypeName)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(projectionTypeName)) throw new ArgumentException("A projection type name is required.", nameof(projectionTypeName));
        var syntax = FecsxRenderDocumentParser.Parse(source);
        var builder = new StringBuilder();
        var segments = new List<FecsxProjectionSegment>();
        if (syntax.Preamble.Length > 0)
        {
            AppendMapped(builder, source, new FecsxTextSpan(0, syntax.Preamble.Length), segments);
            if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
            {
                builder.AppendLine();
            }
        }
        builder.Append("internal static class ").AppendLine(projectionTypeName)
            .AppendLine("{");

        for (var componentIndex = 0; componentIndex < syntax.Components.Count; componentIndex++)
        {
            var component = syntax.Components[componentIndex];
            var declarationHeaderSpan = new FecsxTextSpan(
                component.DeclarationSpan.Start,
                Math.Max(0, component.BodyOffset - component.DeclarationSpan.Start));
            builder.Append("    ");
            AppendMapped(builder, source, declarationHeaderSpan, segments);
            builder.Append("default!");
            AppendMapped(
                builder,
                source,
                new FecsxTextSpan(
                    component.BodyEnd,
                    component.ExpressionEnd - component.BodyEnd),
                segments);
            builder.AppendLine(";");
            if (component.BodyKind == FecsxComponentBodyKind.Block)
            {
                builder.AppendLine("    }");
            }
            builder
                .Append("    private static void __Render").Append(componentIndex).Append('(')
                .Append(component.PropsTypeName).Append(' ').Append(component.PropsParameterName)
                .Append(", global::Interop.JS.React.ReactNode[] ").Append(component.ChildrenParameterName).AppendLine(")")
                .AppendLine("    {");

            foreach (var expression in FecsxEditorService.Expressions(component.Body).Where(item => !item.IsEmpty))
            {
                builder.Append("        _ = (");
                var sourceSpan = new FecsxTextSpan(
                    component.BodyOffset + expression.ContentSpan.Start,
                    expression.ContentSpan.Length);
                AppendMapped(builder, source, sourceSpan, segments);
                builder.AppendLine(");");
            }

            builder.AppendLine("    }");
        }

        builder.AppendLine("}");
        return new FecsxCSharpProjection(builder.ToString(), segments.AsReadOnly());
    }

    private static void AppendMapped(
        StringBuilder builder,
        string source,
        FecsxTextSpan sourceSpan,
        List<FecsxProjectionSegment> segments)
    {
        if (sourceSpan.Length == 0)
        {
            return;
        }

        var projectedSpan = new FecsxTextSpan(builder.Length, sourceSpan.Length);
        builder.Append(source, sourceSpan.Start, sourceSpan.Length);
        segments.Add(new FecsxProjectionSegment(sourceSpan, projectedSpan));
    }
}
