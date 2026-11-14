using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;

namespace FrontEndCSharp.Fecsx.Editor;

/// <summary>
/// Editor-neutral JSX operations adapted from TypeScript 5.2.2. FECSX syntax,
/// intrinsic metadata, and C# projection replace TypeScript's AST and checker.
/// </summary>
public sealed class FecsxEditorService
{
    private const string JsxTagWordPattern = "[a-zA-Z0-9:\\-\\._$]*";

    public FecsxTextSpan? GetTailwindClassCompletionSpan(string source, int position) =>
        FecsxTailwindCompletion.GetContext(source, position)?.ReplacementSpan;

    public IReadOnlyList<FecsxTailwindCompletionItem> GetTailwindClassCompletions(
        string source,
        int position,
        FecsxTailwindClassCatalog? projectCatalog = null) =>
        FecsxTailwindCompletion.GetCompletions(source, position, projectCatalog);

    public bool NeedsTailwindClassCompletionRefresh(string source, int position) =>
        FecsxTailwindCompletion.NeedsDynamicRefresh(source, position);

    public FecsxTextEdit? GetClosingTagCompletion(string source, int position)
    {
        var document = Parse(source, position);
        var candidate = Descendants(document.Body)
            .Where(node => IsUnclosedAt(node, document.Offset, position)
                && IsClosingTagCompletionContext(node, document.Offset, position))
            .OrderByDescending(node => OpeningSpan(node).Start)
            .FirstOrDefault();
        if (candidate is FecsxElementSyntax element)
        {
            return new FecsxTextEdit(new FecsxTextSpan(position, 0), $"</{element.Name}>");
        }

        return candidate is FecsxFragmentSyntax
            ? new FecsxTextEdit(new FecsxTextSpan(position, 0), "</>")
            : null;
    }

    public FecsxTextEdit? GetClosingTagNameCompletion(string source, int position)
    {
        ValidatePosition(source, position);
        var closeStart = source.LastIndexOf("</", Math.Max(0, position - 1), StringComparison.Ordinal);
        if (closeStart < 0 || closeStart + 2 > position)
        {
            return null;
        }

        var prefix = source.Substring(closeStart + 2, position - closeStart - 2);
        if (prefix.Any(character => !IsTagNamePart(character)))
        {
            return null;
        }

        var document = Parse(source, position);
        var element = Descendants(document.Body)
            .OfType<FecsxElementSyntax>()
            .Where(item => document.Offset + item.NameSpan.Start < closeStart
                && item.Name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderByDescending(item => item.NameSpan.Start)
            .FirstOrDefault();
        return element == null
            ? null
            : new FecsxTextEdit(new FecsxTextSpan(closeStart + 2, prefix.Length), element.Name);
    }

    public FecsxLinkedEditingInfo? GetLinkedEditingRanges(string source, int position)
    {
        var document = Parse(source, position);
        foreach (var node in Descendants(document.Body))
        {
            if (node is FecsxElementSyntax element && element.ClosingNameSpan is { } closingName)
            {
                var opening = Absolute(element.NameSpan, document.Offset);
                var closing = Absolute(closingName, document.Offset);
                if ((ContainsInclusive(opening, position) || ContainsInclusive(closing, position))
                    && string.Equals(
                        source.Substring(opening.Start, opening.Length),
                        source.Substring(closing.Start, closing.Length),
                        StringComparison.Ordinal))
                {
                    return new FecsxLinkedEditingInfo(new[] { opening, closing }, JsxTagWordPattern);
                }
            }
            else if (node is FecsxFragmentSyntax fragment && fragment.ClosingTagSpan is { } closingTag)
            {
                var openingPosition = document.Offset + fragment.OpeningTagSpan.Start + 1;
                var closingPosition = document.Offset + closingTag.Start + 2;
                if (position == openingPosition || position == closingPosition)
                {
                    return new FecsxLinkedEditingInfo(
                        new[] { new FecsxTextSpan(openingPosition, 0), new FecsxTextSpan(closingPosition, 0) },
                        JsxTagWordPattern);
                }
            }
        }

        return null;
    }

    public FecsxTextEdit? GetLinkedTagNameEdit(string source, FecsxTextSpan changedSpan, string newText)
    {
        if (changedSpan.End > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(changedSpan));
        }

        var linked = GetLinkedEditingRanges(source, changedSpan.Start);
        if (linked == null || linked.Ranges.Count != 2)
        {
            return null;
        }

        var sourceRange = linked.Ranges.FirstOrDefault(range =>
            range.Length > 0 && range.Start <= changedSpan.Start && changedSpan.End <= range.End);
        if (sourceRange.Length == 0)
        {
            return null;
        }

        var targetRange = linked.Ranges.First(range => range.Start != sourceRange.Start);
        return new FecsxTextEdit(
            new FecsxTextSpan(
                targetRange.Start + changedSpan.Start - sourceRange.Start,
                changedSpan.Length),
            newText);
    }

    public IReadOnlyList<FecsxCompletionItem> GetAttributeCompletions(
        string source,
        int position,
        IFecsxComponentMetadataProvider? componentMetadata = null,
        FecsxAttributeCasing attributeCasing = FecsxAttributeCasing.CamelCase)
    {
        var document = Parse(source, position);
        var element = Descendants(document.Body)
            .OfType<FecsxElementSyntax>()
            .Where(item => ContainsInclusive(Absolute(item.OpeningTagSpan, document.Offset), position))
            .OrderByDescending(item => item.OpeningTagSpan.Start)
            .FirstOrDefault();
        if (element == null || IsInsideAttributeValue(element, document.Offset, position))
        {
            return Array.Empty<FecsxCompletionItem>();
        }

        var activeAttribute = element.Attributes.FirstOrDefault(attribute =>
            ContainsInclusive(Absolute(attribute.NameSpan, document.Offset), position));
        var replacementSpan = activeAttribute == null
            ? new FecsxTextSpan(position, 0)
            : Absolute(activeAttribute.NameSpan, document.Offset);
        if (char.IsLower(element.Name[0]) && IntrinsicElementSchema.TryGetElement(element.Name, out var intrinsic))
        {
            return intrinsic.Properties
                .Where(property => !element.Attributes.Any(attribute =>
                    !ReferenceEquals(attribute, activeAttribute)
                    && intrinsic.TryGetProperty(attribute.Name, out var existingProperty)
                    && string.Equals(
                        existingProperty.PropertyName,
                        property.PropertyName,
                        StringComparison.Ordinal)))
                .Select(property =>
                {
                    var name = FecsxAttributeNaming.PreferredIntrinsicName(property, attributeCasing);
                    var completionProperty = new FecsxCompletionProperty(name, ConvertKind(property.Kind));
                    return new FecsxCompletionItem(
                        name,
                        GetAttributeInsertion(completionProperty),
                        replacementSpan,
                        completionProperty.ValueKind);
                })
                .ToArray();
        }
        IReadOnlyList<FecsxCompletionProperty> properties;
        if (componentMetadata == null
            || !componentMetadata.TryGetProperties(element.Name, out properties!))
        {
            return Array.Empty<FecsxCompletionItem>();
        }

        return properties
            .Where(property => !element.Attributes.Any(attribute =>
                !ReferenceEquals(attribute, activeAttribute)
                && FecsxAttributeNaming.MatchesComponentProperty(attribute.Name, property.Name)))
            .Select(property =>
            {
                var name = FecsxAttributeNaming.PreferredComponentName(property.Name, attributeCasing);
                var completionProperty = new FecsxCompletionProperty(name, property.ValueKind);
                return new FecsxCompletionItem(
                    name,
                    GetAttributeInsertion(completionProperty),
                    replacementSpan,
                    property.ValueKind);
            })
            .ToArray();
    }

    public FecsxTextSpan? GetOpeningTagNameCompletionSpan(string source, int position)
    {
        var document = Parse(source, position);
        var start = position;
        while (start > document.Offset && IsTagNamePart(source[start - 1]))
        {
            start--;
        }

        if (start <= document.Offset || source[start - 1] != '<')
        {
            return null;
        }

        var end = position;
        while (end < source.Length && IsTagNamePart(source[end]))
        {
            end++;
        }

        return new FecsxTextSpan(start, end - start);
    }

    public FecsxTextSpan? GetAttributeNameCompletionSpan(string source, int position)
    {
        var document = Parse(source, position);
        var element = Descendants(document.Body)
            .OfType<FecsxElementSyntax>()
            .Where(item => ContainsInclusive(Absolute(item.OpeningTagSpan, document.Offset), position))
            .OrderByDescending(item => item.OpeningTagSpan.Start)
            .FirstOrDefault();
        if (element == null || IsInsideAttributeValue(element, document.Offset, position))
        {
            return null;
        }

        var activeAttribute = element.Attributes.FirstOrDefault(attribute =>
            ContainsInclusive(Absolute(attribute.NameSpan, document.Offset), position));
        return activeAttribute == null
            ? new FecsxTextSpan(position, 0)
            : Absolute(activeAttribute.NameSpan, document.Offset);
    }

    public IReadOnlyList<FecsxTextSpan> GetBraceMatchingRanges(string source, int position)
    {
        var document = Parse(source, position);
        foreach (var expression in Expressions(document.Body))
        {
            var span = Absolute(expression.Span, document.Offset);
            if (expression.IsTerminated
                && (position == span.Start || position == span.Start + 1 || position == span.End - 1 || position == span.End))
            {
                return new[]
                {
                    new FecsxTextSpan(span.Start, 1),
                    new FecsxTextSpan(span.End - 1, 1),
                };
            }
        }

        return Array.Empty<FecsxTextSpan>();
    }

    public IReadOnlyList<FecsxTextEdit> CommentSelection(string source, FecsxTextSpan selection)
    {
        if (selection.End > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(selection));
        }

        var document = Parse(source, selection.Start);
        var isCSharp = selection.End <= document.Offset || Expressions(document.Body).Any(expression =>
        {
            var content = Absolute(expression.ContentSpan, document.Offset);
            return content.Start <= selection.Start && selection.End <= content.End;
        });
        return new[]
        {
            new FecsxTextEdit(new FecsxTextSpan(selection.Start, 0), isCSharp ? "/*" : "{/*"),
            new FecsxTextEdit(new FecsxTextSpan(selection.End, 0), isCSharp ? "*/" : "*/}"),
        };
    }

    public FecsxTextEdit? GetExpressionFormattingEdit(string source, int position)
    {
        var document = Parse(source, position);
        var expression = Expressions(document.Body).FirstOrDefault(item =>
            ContainsInclusive(Absolute(item.ContentSpan, document.Offset), position));
        if (expression?.Expression == null || !expression.IsTerminated)
        {
            return null;
        }

        var normalized = expression.Expression.NormalizeWhitespace().ToFullString();
        return new FecsxTextEdit(Absolute(expression.Span, document.Offset), "{" + normalized + "}");
    }

    public IReadOnlyList<FecsxClassifiedSpan> GetClassifications(string source)
    {
        var document = Parse(source, source.Length);
        var results = new List<FecsxClassifiedSpan>();
        foreach (var component in document.Syntax.Components)
        {
            Classify(component.Body, component.BodyOffset, results);
        }
        return results;
    }

    private static void Classify(FecsxChildSyntax? node, int offset, List<FecsxClassifiedSpan> results)
    {
        if (node == null)
        {
            return;
        }

        switch (node)
        {
            case FecsxElementSyntax element:
                Add(results, offset + element.OpeningTagSpan.Start, 1, FecsxClassificationKind.MarkupDelimiter);
                Add(results, Absolute(element.NameSpan, offset), FecsxClassificationKind.TagName);
                foreach (var attribute in element.Attributes)
                {
                    Add(results, Absolute(attribute.NameSpan, offset), FecsxClassificationKind.AttributeName);
                    if (attribute is FecsxStringAttributeSyntax textAttribute)
                    {
                        Add(results, Absolute(textAttribute.ValueSpan, offset), FecsxClassificationKind.AttributeValue);
                    }
                    else if (attribute is FecsxExpressionAttributeSyntax expressionAttribute)
                    {
                        Classify(expressionAttribute.Expression, offset, results);
                    }
                }
                foreach (var child in element.Children) Classify(child, offset, results);
                if (element.ClosingNameSpan is { } closingName)
                {
                    Add(results, offset + closingName.Start - 2, 2, FecsxClassificationKind.MarkupDelimiter);
                    Add(results, Absolute(closingName, offset), FecsxClassificationKind.TagName);
                }
                break;
            case FecsxFragmentSyntax fragment:
                Add(results, Absolute(fragment.OpeningTagSpan, offset), FecsxClassificationKind.MarkupDelimiter);
                foreach (var child in fragment.Children) Classify(child, offset, results);
                if (fragment.ClosingTagSpan is { } closingTag)
                {
                    Add(results, Absolute(closingTag, offset), FecsxClassificationKind.MarkupDelimiter);
                }
                break;
            case FecsxTextSyntax text:
                Add(results, Absolute(text.Span, offset), FecsxClassificationKind.Text);
                break;
            case FecsxExpressionSyntax expression:
                var expressionSpan = Absolute(expression.Span, offset);
                Add(results, expressionSpan.Start, 1, FecsxClassificationKind.ExpressionDelimiter);
                Add(results, Absolute(expression.ContentSpan, offset), FecsxClassificationKind.CSharpExpression);
                if (expression.IsTerminated) Add(results, expressionSpan.End - 1, 1, FecsxClassificationKind.ExpressionDelimiter);
                break;
        }
    }

    private static void Add(List<FecsxClassifiedSpan> results, FecsxTextSpan span, FecsxClassificationKind kind)
    {
        if (span.Length > 0) results.Add(new FecsxClassifiedSpan(span, kind));
    }

    private static void Add(List<FecsxClassifiedSpan> results, int start, int length, FecsxClassificationKind kind) =>
        Add(results, new FecsxTextSpan(start, length), kind);

    private static bool IsInsideAttributeValue(FecsxElementSyntax element, int offset, int position) =>
        element.Attributes.Any(attribute => attribute switch
        {
            FecsxStringAttributeSyntax text => ContainsInclusive(Absolute(text.ValueSpan, offset), position),
            FecsxExpressionAttributeSyntax expression => ContainsInclusive(Absolute(expression.Expression.Span, offset), position),
            _ => false,
        });

    private static bool IsUnclosedAt(FecsxChildSyntax node, int offset, int position)
    {
        if (node is FecsxElementSyntax element)
        {
            return !element.IsSelfClosing
                && element.ClosingTagSpan == null
                && offset + element.OpeningTagSpan.End <= position
                && position <= offset + element.Span.End;
        }

        return node is FecsxFragmentSyntax fragment
            && fragment.ClosingTagSpan == null
            && offset + fragment.OpeningTagSpan.End <= position
            && position <= offset + fragment.Span.End;
    }

    private static bool IsClosingTagCompletionContext(FecsxChildSyntax node, int offset, int position)
    {
        if (position == offset + OpeningSpan(node).End)
        {
            return true;
        }

        var children = node switch
        {
            FecsxElementSyntax element => element.Children,
            FecsxFragmentSyntax fragment => fragment.Children,
            _ => Array.Empty<FecsxChildSyntax>(),
        };
        return children.OfType<FecsxTextSyntax>()
            .Any(text => ContainsInclusive(Absolute(text.Span, offset), position));
    }

    private static FecsxTextSpan OpeningSpan(FecsxChildSyntax node) => node switch
    {
        FecsxElementSyntax element => element.OpeningTagSpan,
        FecsxFragmentSyntax fragment => fragment.OpeningTagSpan,
        _ => node.Span,
    };

    internal static IEnumerable<FecsxChildSyntax> Descendants(FecsxChildSyntax? root)
    {
        if (root == null) yield break;
        yield return root;
        var children = root switch
        {
            FecsxElementSyntax element => element.Children,
            FecsxFragmentSyntax fragment => fragment.Children,
            _ => Array.Empty<FecsxChildSyntax>(),
        };
        foreach (var child in children)
        {
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    internal static IEnumerable<FecsxExpressionSyntax> Expressions(FecsxChildSyntax? root)
    {
        foreach (var node in Descendants(root))
        {
            if (node is FecsxExpressionSyntax expression) yield return expression;
            if (node is FecsxElementSyntax element)
            {
                foreach (var attribute in element.Attributes.OfType<FecsxExpressionAttributeSyntax>())
                {
                    yield return attribute.Expression;
                }
            }
        }
    }

    private static FecsxCompletionValueKind ConvertKind(IntrinsicPropertyKind kind) => kind switch
    {
        IntrinsicPropertyKind.String => FecsxCompletionValueKind.String,
        IntrinsicPropertyKind.Boolean => FecsxCompletionValueKind.Boolean,
        IntrinsicPropertyKind.Number => FecsxCompletionValueKind.Number,
        IntrinsicPropertyKind.EventHandler => FecsxCompletionValueKind.EventHandler,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string GetAttributeInsertion(FecsxCompletionProperty property) => property.ValueKind switch
    {
        FecsxCompletionValueKind.Boolean => property.Name,
        FecsxCompletionValueKind.String => property.Name + "=\"\"",
        _ => property.Name + "={}",
    };

    private static bool ContainsInclusive(FecsxTextSpan span, int position) =>
        span.Start <= position && position <= span.End;

    private static FecsxTextSpan Absolute(FecsxTextSpan span, int offset) =>
        new FecsxTextSpan(offset + span.Start, span.Length);

    private static bool IsTagNamePart(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '$' or '-' or '.' or ':';

    private static ParsedDocument Parse(string source, int position)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        ValidatePosition(source, position);
        var syntax = FecsxRenderDocumentParser.Parse(source);
        var component = syntax.Components.FirstOrDefault(item =>
                item.DeclarationSpan.Start <= position && position <= item.DeclarationSpan.End)
            ?? syntax.DefaultComponent
            ?? syntax.Components.FirstOrDefault();
        return new ParsedDocument(syntax, component?.Body, component?.BodyOffset ?? 0);
    }

    private static void ValidatePosition(string source, int position)
    {
        if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));
    }

    private sealed class ParsedDocument
    {
        public ParsedDocument(FecsxRenderDocumentSyntax syntax, FecsxChildSyntax? body, int offset)
        {
            Syntax = syntax;
            Body = body;
            Offset = offset;
        }

        public FecsxRenderDocumentSyntax Syntax { get; }
        public FecsxChildSyntax? Body { get; }
        public int Offset { get; }
    }
}
