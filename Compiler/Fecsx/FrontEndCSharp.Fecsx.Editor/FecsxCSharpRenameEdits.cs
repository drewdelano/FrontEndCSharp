using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FrontEndCSharp.Fecsx.Editor;

public static class FecsxCSharpRenameEdits
{
    public static bool TryMapChanges(
        FecsxCSharpProjection projection,
        IEnumerable<TextChange> projectedChanges,
        out IReadOnlyList<FecsxTextEdit> sourceEdits)
    {
        if (projection == null) throw new ArgumentNullException(nameof(projection));
        if (projectedChanges == null) throw new ArgumentNullException(nameof(projectedChanges));

        var mapped = new List<FecsxTextEdit>();
        foreach (var change in projectedChanges)
        {
            var projectedSpan = new FecsxTextSpan(change.Span.Start, change.Span.Length);
            if (projection.TryMapProjectedSpan(projectedSpan, out var sourceSpan))
            {
                mapped.Add(new FecsxTextEdit(sourceSpan, change.NewText ?? string.Empty));
                continue;
            }

            // Roslyn can rename a symbol both in authored FECSX and in generated
            // projection scaffolding (for example the synthetic __Render props
            // parameter). Scaffolding has no authored destination and must be
            // ignored. An edit that crosses a mapping boundary is ambiguous and
            // still aborts the transactional rename.
            if (projection.Segments.Any(segment =>
                projectedSpan.Start < segment.ProjectedSpan.End &&
                segment.ProjectedSpan.Start < projectedSpan.End))
            {
                sourceEdits = Array.Empty<FecsxTextEdit>();
                return false;
            }
        }

        sourceEdits = mapped.AsReadOnly();
        return true;
    }

    public static string Apply(string source, IReadOnlyList<FecsxTextEdit> edits)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (edits == null) throw new ArgumentNullException(nameof(edits));

        var result = source;
        foreach (var edit in edits.OrderByDescending(edit => edit.Span.Start))
        {
            result = result.Remove(edit.Span.Start, edit.Span.Length)
                .Insert(edit.Span.Start, edit.NewText);
        }

        return result;
    }
}
