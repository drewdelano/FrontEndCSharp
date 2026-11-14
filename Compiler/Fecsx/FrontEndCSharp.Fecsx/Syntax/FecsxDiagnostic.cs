namespace FrontEndCSharp.Fecsx.Syntax;

public sealed class FecsxDiagnostic
{
    public FecsxDiagnostic(string id, string message, FecsxTextSpan span)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Span = span;
    }

    public string Id { get; }

    public string Message { get; }

    public FecsxTextSpan Span { get; }
}
