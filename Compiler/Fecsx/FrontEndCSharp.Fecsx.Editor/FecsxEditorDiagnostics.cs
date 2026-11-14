using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;

namespace FrontEndCSharp.Fecsx.Editor;

public sealed class FecsxEditorDiagnostic
{
    public FecsxEditorDiagnostic(
        string id,
        string message,
        FecsxTextSpan span,
        DiagnosticSeverity severity)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Span = span;
        Severity = severity;
    }

    public string Id { get; }
    public string Message { get; }
    public FecsxTextSpan Span { get; }
    public DiagnosticSeverity Severity { get; }
}

public static class FecsxEditorDiagnostics
{
    public static IReadOnlyList<FecsxEditorDiagnostic> GetNativeDiagnostics(string source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        return FecsxDocumentDiagnostics
            .GetDiagnostics(FecsxRenderDocumentParser.Parse(source))
            .Select(diagnostic => new FecsxEditorDiagnostic(
                diagnostic.Id,
                diagnostic.Message,
                diagnostic.Span,
                DiagnosticSeverity.Error))
            .ToArray();
    }

    public static IReadOnlyList<FecsxEditorDiagnostic> MapRoslynDiagnostics(
        FecsxCSharpProjection projection,
        IEnumerable<Diagnostic> diagnostics)
    {
        if (projection == null) throw new ArgumentNullException(nameof(projection));
        if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));

        var mapped = new List<FecsxEditorDiagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Location.SourceTree == null ||
                diagnostic.Severity is not (DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            {
                continue;
            }

            var projectedSpan = diagnostic.Location.SourceSpan;
            if (projection.TryMapProjectedSpan(
                new FecsxTextSpan(projectedSpan.Start, projectedSpan.Length),
                out var sourceSpan))
            {
                mapped.Add(new FecsxEditorDiagnostic(
                    diagnostic.Id,
                    diagnostic.GetMessage(),
                    sourceSpan,
                    diagnostic.Severity));
            }
        }

        return mapped;
    }
}
