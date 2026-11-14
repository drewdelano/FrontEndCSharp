namespace FrontEndCSharp.Fecsx.Syntax;

public enum FecsxComponentBodyKind
{
    Expression,
    Block,
}

public sealed class FecsxComponentDeclarationSyntax
{
    public FecsxComponentDeclarationSyntax(
        string name,
        string propsTypeName,
        string propsParameterName,
        string childrenParameterName,
        FecsxTextSpan declarationSpan,
        FecsxTextSpan nameSpan,
        FecsxComponentBodyKind bodyKind,
        int expressionOffset,
        int bodyOffset,
        int bodyEnd,
        int expressionEnd,
        FecsxChildSyntax? body)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        PropsTypeName = propsTypeName ?? throw new ArgumentNullException(nameof(propsTypeName));
        PropsParameterName = propsParameterName ?? throw new ArgumentNullException(nameof(propsParameterName));
        ChildrenParameterName = childrenParameterName ?? throw new ArgumentNullException(nameof(childrenParameterName));
        DeclarationSpan = declarationSpan;
        NameSpan = nameSpan;
        BodyKind = bodyKind;
        ExpressionOffset = expressionOffset;
        BodyOffset = bodyOffset;
        BodyEnd = bodyEnd;
        ExpressionEnd = expressionEnd;
        Body = body;
    }

    public string Name { get; }
    public string PropsTypeName { get; }
    public string PropsParameterName { get; }
    public string ChildrenParameterName { get; }
    public FecsxTextSpan DeclarationSpan { get; }
    public FecsxTextSpan NameSpan { get; }
    public FecsxComponentBodyKind BodyKind { get; }
    public int ExpressionOffset { get; }
    public int BodyOffset { get; }
    public int BodyEnd { get; }
    public int ExpressionEnd { get; }
    public FecsxChildSyntax? Body { get; }
}

public sealed class FecsxRenderDocumentSyntax
{
    public FecsxRenderDocumentSyntax(
        string source,
        string preamble,
        string? namespaceName,
        string facadeName,
        IReadOnlyList<FecsxComponentDeclarationSyntax> components,
        IReadOnlyList<FecsxDiagnostic> diagnostics)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Preamble = preamble ?? throw new ArgumentNullException(nameof(preamble));
        NamespaceName = namespaceName;
        FacadeName = facadeName ?? throw new ArgumentNullException(nameof(facadeName));
        Components = components ?? throw new ArgumentNullException(nameof(components));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public string Source { get; }
    public string Preamble { get; }
    public string? NamespaceName { get; }
    public string FacadeName { get; }
    public IReadOnlyList<FecsxComponentDeclarationSyntax> Components { get; }
    public FecsxComponentDeclarationSyntax? DefaultComponent =>
        Components.FirstOrDefault(component => string.Equals(component.Name, "Default", StringComparison.Ordinal));
    public IReadOnlyList<FecsxDiagnostic> Diagnostics { get; }

}
