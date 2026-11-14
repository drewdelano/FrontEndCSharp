using FrontEndCSharp.Fecsx.Syntax;

namespace FrontEndCSharp.Fecsx.Editor;

public sealed class FecsxTailwindClassCatalog
{
    private FecsxTailwindClassCatalog(TailwindClassCatalog catalog) => Catalog = catalog;

    internal TailwindClassCatalog Catalog { get; }

    public static FecsxTailwindClassCatalog FromCompressedBytes(byte[] content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        using var input = new MemoryStream(content, writable: false);
        return new FecsxTailwindClassCatalog(TailwindClassCatalog.Load(input));
    }
}

public sealed class FecsxTextEdit
{
    public FecsxTextEdit(FecsxTextSpan span, string newText)
    {
        Span = span;
        NewText = newText ?? throw new ArgumentNullException(nameof(newText));
    }

    public FecsxTextSpan Span { get; }
    public string NewText { get; }
}

public sealed class FecsxLinkedEditingInfo
{
    public FecsxLinkedEditingInfo(IReadOnlyList<FecsxTextSpan> ranges, string wordPattern)
    {
        Ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
        WordPattern = wordPattern ?? throw new ArgumentNullException(nameof(wordPattern));
    }

    public IReadOnlyList<FecsxTextSpan> Ranges { get; }
    public string WordPattern { get; }
}

public enum FecsxCompletionValueKind
{
    String,
    Boolean,
    Number,
    EventHandler,
    Expression,
}

public sealed class FecsxCompletionProperty
{
    public FecsxCompletionProperty(string name, FecsxCompletionValueKind valueKind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ValueKind = valueKind;
    }

    public string Name { get; }
    public FecsxCompletionValueKind ValueKind { get; }
}

public interface IFecsxComponentMetadataProvider
{
    bool TryGetProperties(string componentName, out IReadOnlyList<FecsxCompletionProperty> properties);
}

public sealed class FecsxCompletionItem
{
    public FecsxCompletionItem(
        string displayText,
        string insertText,
        FecsxTextSpan replacementSpan,
        FecsxCompletionValueKind valueKind)
    {
        DisplayText = displayText ?? throw new ArgumentNullException(nameof(displayText));
        InsertText = insertText ?? throw new ArgumentNullException(nameof(insertText));
        ReplacementSpan = replacementSpan;
        ValueKind = valueKind;
    }

    public string DisplayText { get; }
    public string InsertText { get; }
    public FecsxTextSpan ReplacementSpan { get; }
    public FecsxCompletionValueKind ValueKind { get; }
}

public sealed class FecsxTailwindCompletionItem
{
    private readonly Lazy<string> _description;

    public FecsxTailwindCompletionItem(
        string displayText,
        string insertText,
        FecsxTextSpan replacementSpan,
        string description,
        int caretOffsetFromEnd = 0,
        string? sortText = null)
        : this(
            displayText,
            insertText,
            replacementSpan,
            () => description,
            caretOffsetFromEnd,
            sortText)
    {
        if (description == null) throw new ArgumentNullException(nameof(description));
    }

    public FecsxTailwindCompletionItem(
        string displayText,
        string insertText,
        FecsxTextSpan replacementSpan,
        Func<string> descriptionFactory,
        int caretOffsetFromEnd = 0,
        string? sortText = null)
    {
        DisplayText = displayText ?? throw new ArgumentNullException(nameof(displayText));
        InsertText = insertText ?? throw new ArgumentNullException(nameof(insertText));
        ReplacementSpan = replacementSpan;
        if (descriptionFactory == null) throw new ArgumentNullException(nameof(descriptionFactory));
        _description = new Lazy<string>(descriptionFactory);
        SortText = sortText ?? displayText;
        if (caretOffsetFromEnd < 0 || caretOffsetFromEnd > insertText.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(caretOffsetFromEnd));
        }
        CaretOffsetFromEnd = caretOffsetFromEnd;
    }

    public string DisplayText { get; }
    public string InsertText { get; }
    public FecsxTextSpan ReplacementSpan { get; }
    public string Description => _description.Value;
    public string SortText { get; }
    public int CaretOffsetFromEnd { get; }
}

public enum FecsxClassificationKind
{
    MarkupDelimiter,
    TagName,
    AttributeName,
    AttributeValue,
    Text,
    ExpressionDelimiter,
    CSharpExpression,
}

public sealed class FecsxClassifiedSpan
{
    public FecsxClassifiedSpan(FecsxTextSpan span, FecsxClassificationKind kind)
    {
        Span = span;
        Kind = kind;
    }

    public FecsxTextSpan Span { get; }
    public FecsxClassificationKind Kind { get; }
}
