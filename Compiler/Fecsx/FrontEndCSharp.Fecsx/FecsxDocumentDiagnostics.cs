using FrontEndCSharp.Fecsx.Syntax;

namespace FrontEndCSharp.Fecsx;

public static class FecsxDocumentDiagnostics
{
    public static IReadOnlyList<FecsxDiagnostic> GetDiagnostics(
        FecsxRenderDocumentSyntax syntax,
        FecsxAttributeCasing attributeCasing = FecsxAttributeCasing.CamelCase)
    {
        if (syntax == null) throw new ArgumentNullException(nameof(syntax));

        var diagnostics = new List<FecsxDiagnostic>(syntax.Diagnostics);
        if (diagnostics.Count == 0)
        {
            foreach (var component in syntax.Components.Where(component => component.Body !=  null))
            {
                diagnostics.AddRange(FecsxIntrinsicValidator.Validate(component.Body!, attributeCasing).Select(diagnostic =>
                    new FecsxDiagnostic(
                        diagnostic.Id,
                        diagnostic.Message,
                        new FecsxTextSpan(
                            component.BodyOffset + diagnostic.Span.Start,
                            diagnostic.Span.Length))));
            }
        }

        return diagnostics;
    }
}
