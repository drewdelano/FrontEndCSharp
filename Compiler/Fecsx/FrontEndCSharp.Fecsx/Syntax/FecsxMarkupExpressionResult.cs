namespace FrontEndCSharp.Fecsx.Syntax;

public sealed class FecsxMarkupExpressionResult
{
    public FecsxMarkupExpressionResult(
        FecsxChildSyntax? root,
        int end,
        IReadOnlyList<FecsxDiagnostic> diagnostics)
    {
        Root = root;
        End = end;
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public FecsxChildSyntax? Root { get; }

    public int End { get; }

    public IReadOnlyList<FecsxDiagnostic> Diagnostics { get; }
}
