namespace FrontEndCSharp.Fecsx;

public enum IntrinsicPropertyKind
{
    String,
    Boolean,
    Number,
    EventHandler,
}

public sealed class IntrinsicPropertyDescriptor
{
    internal IntrinsicPropertyDescriptor(string markupName, string propertyName, string reactName, IntrinsicPropertyKind kind)
    {
        MarkupName = markupName;
        PropertyName = propertyName;
        ReactName = reactName;
        Kind = kind;
    }

    public string MarkupName { get; }
    public string PropertyName { get; }
    public string ReactName { get; }
    public IntrinsicPropertyKind Kind { get; }
}

public sealed class IntrinsicElementDescriptor
{
    private readonly IReadOnlyDictionary<string, IntrinsicPropertyDescriptor> _propertiesByAuthoredName;

    internal IntrinsicElementDescriptor(
        string tagName,
        string propsTypeName,
        IReadOnlyList<IntrinsicPropertyDescriptor> declaredProperties,
        IReadOnlyList<IntrinsicPropertyDescriptor> properties)
    {
        TagName = tagName;
        PropsTypeName = propsTypeName;
        DeclaredProperties = declaredProperties;
        Properties = properties;
        var propertiesByAuthoredName = new Dictionary<string, IntrinsicPropertyDescriptor>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            propertiesByAuthoredName.Add(property.MarkupName, property);
            if (!string.Equals(property.MarkupName, property.PropertyName, StringComparison.Ordinal))
            {
                propertiesByAuthoredName.Add(property.PropertyName, property);
            }
        }
        _propertiesByAuthoredName = propertiesByAuthoredName;
    }

    public string TagName { get; }
    public string PropsTypeName { get; }
    public IReadOnlyList<IntrinsicPropertyDescriptor> DeclaredProperties { get; }
    public IReadOnlyList<IntrinsicPropertyDescriptor> Properties { get; }

    public bool TryGetProperty(string markupName, out IntrinsicPropertyDescriptor property) =>
        _propertiesByAuthoredName.TryGetValue(markupName, out property!);
}

public static class IntrinsicElementSchema
{
    private static readonly IntrinsicPropertyDescriptor[] GlobalPropertyArray =
    {
        String("id", "Id"),
        String("className", "ClassName"),
        String("title", "Title"),
        String("role", "Role"),
        String("data-slot", "DataSlot"),
        String("aria-label", "AriaLabel"),
        String("aria-current", "AriaCurrent"),
        Boolean("aria-disabled", "AriaDisabled"),
        Boolean("aria-hidden", "AriaHidden"),
        Boolean("hidden", "Hidden"),
        Number("tabIndex", "TabIndex"),
        Event("onBlur", "OnBlur"),
        Event("onChange", "OnChange"),
        Event("onClick", "OnClick"),
        Event("onFocus", "OnFocus"),
        Event("onInput", "OnInput"),
        Event("onSubmit", "OnSubmit"),
    };

    private static readonly IntrinsicElementDescriptor[] ElementArray =
    {
        Element("a", "AnchorProps", String("href", "Href"), String("target", "Target"), String("rel", "Rel")),
        Element("article", "ArticleProps"),
        Element("aside", "AsideProps"),
        Element("blockquote", "BlockquoteProps", String("cite", "Cite")),
        Element("button", "ButtonProps", String("type", "Type"), Boolean("disabled", "Disabled")),
        Element("cite", "CiteProps"),
        Element("div", "DivProps"),
        Element("em", "EmProps"),
        Element("footer", "FooterProps"),
        Element("form", "FormProps", String("action", "Action"), String("method", "Method")),
        Element("h1", "Heading1Props"),
        Element("h2", "Heading2Props"),
        Element("h3", "Heading3Props"),
        Element("header", "HeaderProps"),
        Element("img", "ImageProps", String("src", "Src"), String("alt", "Alt"), Number("width", "Width"), Number("height", "Height")),
        Element("input", "InputProps", String("type", "Type"), String("value", "Value"), String("placeholder", "Placeholder"), Boolean("checked", "Checked"), Boolean("disabled", "Disabled"), Boolean("readOnly", "ReadOnly"), Boolean("required", "Required"), Number("maxLength", "MaxLength"), Number("minLength", "MinLength"), Number("size", "Size")),
        Element("label", "LabelProps", String("htmlFor", "HtmlFor")),
        Element("li", "ListItemProps", Number("value", "Value")),
        Element("main", "MainProps"),
        Element("nav", "NavProps"),
        Element("ol", "OrderedListProps", Number("start", "Start"), Boolean("reversed", "Reversed")),
        Element("option", "OptionProps", String("value", "Value"), Boolean("disabled", "Disabled"), Boolean("selected", "Selected")),
        Element("p", "ParagraphProps"),
        Element("section", "SectionProps"),
        Element("select", "SelectProps", String("value", "Value"), Boolean("disabled", "Disabled"), Boolean("multiple", "Multiple"), Boolean("required", "Required"), Number("size", "Size")),
        Element("span", "SpanProps"),
        Element("strong", "StrongProps"),
        Element("table", "TableProps"),
        Element("tbody", "TableBodyProps"),
        Element("td", "TableCellProps", Number("colSpan", "ColSpan"), Number("rowSpan", "RowSpan")),
        Element("textarea", "TextAreaProps", String("value", "Value"), String("placeholder", "Placeholder"), Boolean("disabled", "Disabled"), Boolean("readOnly", "ReadOnly"), Boolean("required", "Required"), Number("rows", "Rows"), Number("cols", "Cols")),
        Element("th", "TableHeaderCellProps", Number("colSpan", "ColSpan"), Number("rowSpan", "RowSpan"), String("scope", "Scope")),
        Element("thead", "TableHeadProps"),
        Element("tr", "TableRowProps"),
        Element("ul", "UnorderedListProps"),
    };

    private static readonly IReadOnlyDictionary<string, IntrinsicElementDescriptor> ElementsByTagName =
        ElementArray.ToDictionary(element => element.TagName, StringComparer.Ordinal);

    public static IReadOnlyList<IntrinsicPropertyDescriptor> GlobalProperties => GlobalPropertyArray;
    public static IReadOnlyList<IntrinsicElementDescriptor> Elements => ElementArray;

    public static bool TryGetElement(string tagName, out IntrinsicElementDescriptor element) =>
        ElementsByTagName.TryGetValue(tagName, out element!);

    private static IntrinsicElementDescriptor Element(string tagName, string propsTypeName, params IntrinsicPropertyDescriptor[] declaredProperties) =>
        new IntrinsicElementDescriptor(
            tagName,
            propsTypeName,
            declaredProperties,
            GlobalPropertyArray.Concat(declaredProperties).ToArray());

    private static IntrinsicPropertyDescriptor String(string markupName, string propertyName) =>
        new IntrinsicPropertyDescriptor(markupName, propertyName, markupName, IntrinsicPropertyKind.String);

    private static IntrinsicPropertyDescriptor Boolean(string markupName, string propertyName) =>
        new IntrinsicPropertyDescriptor(markupName, propertyName, markupName, IntrinsicPropertyKind.Boolean);

    private static IntrinsicPropertyDescriptor Number(string markupName, string propertyName) =>
        new IntrinsicPropertyDescriptor(markupName, propertyName, markupName, IntrinsicPropertyKind.Number);

    private static IntrinsicPropertyDescriptor Event(string markupName, string propertyName) =>
        new IntrinsicPropertyDescriptor(markupName, propertyName, markupName, IntrinsicPropertyKind.EventHandler);
}
