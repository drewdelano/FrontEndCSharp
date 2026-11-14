using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FrontEndCSharp.Fecsx.Syntax;

public abstract class FecsxSyntaxNode
{
    protected FecsxSyntaxNode(FecsxTextSpan span)
    {
        Span = span;
    }

    public FecsxTextSpan Span { get; }
}

public sealed class FecsxDocumentSyntax : FecsxSyntaxNode
{
    public FecsxDocumentSyntax(
        FecsxTextSpan span,
        IReadOnlyList<FecsxChildSyntax> children,
        IReadOnlyList<FecsxDiagnostic> diagnostics)
        : base(span)
    {
        Children = children ?? throw new ArgumentNullException(nameof(children));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public IReadOnlyList<FecsxChildSyntax> Children { get; }

    public IReadOnlyList<FecsxDiagnostic> Diagnostics { get; }
}

public abstract class FecsxChildSyntax : FecsxSyntaxNode
{
    protected FecsxChildSyntax(FecsxTextSpan span)
        : base(span)
    {
    }
}

public sealed class FecsxElementSyntax : FecsxChildSyntax
{
    public FecsxElementSyntax(
        FecsxTextSpan span,
        string name,
        FecsxTextSpan nameSpan,
        FecsxTextSpan openingTagSpan,
        IReadOnlyList<FecsxAttributeSyntax> attributes,
        IReadOnlyList<FecsxChildSyntax> children,
        bool isSelfClosing,
        FecsxTextSpan? closingNameSpan,
        FecsxTextSpan? closingTagSpan)
        : base(span)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NameSpan = nameSpan;
        OpeningTagSpan = openingTagSpan;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Children = children ?? throw new ArgumentNullException(nameof(children));
        IsSelfClosing = isSelfClosing;
        ClosingNameSpan = closingNameSpan;
        ClosingTagSpan = closingTagSpan;
    }

    public string Name { get; }

    public FecsxTextSpan NameSpan { get; }

    public FecsxTextSpan OpeningTagSpan { get; }

    public IReadOnlyList<FecsxAttributeSyntax> Attributes { get; }

    public IReadOnlyList<FecsxChildSyntax> Children { get; }

    public bool IsSelfClosing { get; }

    public FecsxTextSpan? ClosingNameSpan { get; }

    public FecsxTextSpan? ClosingTagSpan { get; }
}

public sealed class FecsxFragmentSyntax : FecsxChildSyntax
{
    public FecsxFragmentSyntax(
        FecsxTextSpan span,
        FecsxTextSpan openingTagSpan,
        IReadOnlyList<FecsxChildSyntax> children,
        FecsxTextSpan? closingTagSpan)
        : base(span)
    {
        OpeningTagSpan = openingTagSpan;
        Children = children ?? throw new ArgumentNullException(nameof(children));
        ClosingTagSpan = closingTagSpan;
    }

    public FecsxTextSpan OpeningTagSpan { get; }

    public IReadOnlyList<FecsxChildSyntax> Children { get; }

    public FecsxTextSpan? ClosingTagSpan { get; }
}

public sealed class FecsxTextSyntax : FecsxChildSyntax
{
    public FecsxTextSyntax(FecsxTextSpan span, string text)
        : base(span)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public string Text { get; }
}

public sealed class FecsxExpressionSyntax : FecsxChildSyntax
{
    public FecsxExpressionSyntax(
        FecsxTextSpan span,
        FecsxTextSpan contentSpan,
        string text,
        ExpressionSyntax? expression,
        bool isEmpty,
        bool isTerminated)
        : base(span)
    {
        ContentSpan = contentSpan;
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Expression = expression;
        IsEmpty = isEmpty;
        IsTerminated = isTerminated;
    }

    public FecsxTextSpan ContentSpan { get; }

    public string Text { get; }

    public ExpressionSyntax? Expression { get; }

    public bool IsEmpty { get; }

    public bool IsTerminated { get; }
}

public abstract class FecsxAttributeSyntax : FecsxSyntaxNode
{
    protected FecsxAttributeSyntax(FecsxTextSpan span, string name, FecsxTextSpan nameSpan)
        : base(span)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NameSpan = nameSpan;
    }

    public string Name { get; }

    public FecsxTextSpan NameSpan { get; }
}

public sealed class FecsxBooleanAttributeSyntax : FecsxAttributeSyntax
{
    public FecsxBooleanAttributeSyntax(FecsxTextSpan span, string name, FecsxTextSpan nameSpan)
        : base(span, name, nameSpan)
    {
    }
}

public sealed class FecsxStringAttributeSyntax : FecsxAttributeSyntax
{
    public FecsxStringAttributeSyntax(
        FecsxTextSpan span,
        string name,
        FecsxTextSpan nameSpan,
        string value,
        FecsxTextSpan valueSpan,
        char quote)
        : base(span, name, nameSpan)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        ValueSpan = valueSpan;
        Quote = quote;
    }

    public string Value { get; }

    public FecsxTextSpan ValueSpan { get; }

    public char Quote { get; }
}

public sealed class FecsxExpressionAttributeSyntax : FecsxAttributeSyntax
{
    public FecsxExpressionAttributeSyntax(
        FecsxTextSpan span,
        string name,
        FecsxTextSpan nameSpan,
        FecsxExpressionSyntax expression)
        : base(span, name, nameSpan)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public FecsxExpressionSyntax Expression { get; }
}
