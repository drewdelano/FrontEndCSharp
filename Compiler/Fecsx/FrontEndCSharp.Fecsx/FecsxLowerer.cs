using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace FrontEndCSharp.Fecsx;

internal static class FecsxLowerer
{
    public static string Lower(
        FecsxChildSyntax child,
        Func<FecsxExpressionSyntax, string>? lowerExpression = null)
    {
        if (child is FecsxElementSyntax element)
        {
            return IsManagedComponent(element.Name)
                ? LowerManagedElement(element, lowerExpression)
                : LowerIntrinsicElement(element, lowerExpression);
        }

        if (child is FecsxFragmentSyntax fragment)
        {
            return "global::Interop.JS.React.React.CreateFragment(" + LowerChildren(fragment.Children, lowerExpression) + ")";
        }

        if (child is FecsxTextSyntax text)
        {
            return LowerText(text.Text)
                ?? "global::Interop.JS.React.React.Text(string.Empty)";
        }

        if (child is FecsxExpressionSyntax expression)
        {
            return expression.IsEmpty
                ? "global::Interop.JS.React.React.Text(string.Empty)"
                : "global::Interop.JS.React.React.Node(" + LowerExpression(expression, lowerExpression) + ")";
        }

        throw new InvalidOperationException($"Unsupported FECSX child node '{child.GetType().Name}'.");
    }

    private static string LowerIntrinsicElement(
        FecsxElementSyntax element,
        Func<FecsxExpressionSyntax, string>? lowerExpression) =>
        "global::Interop.JS.React.React.CreateElement("
        + SymbolDisplay.FormatLiteral(element.Name, true)
        + ", "
        + LowerIntrinsicProps(element.Name, element.Attributes, lowerExpression)
        + ", "
        + LowerChildren(element.Children, lowerExpression)
        + ")";

    private static string LowerManagedElement(
        FecsxElementSyntax element,
        Func<FecsxExpressionSyntax, string>? lowerExpression)
    {
        var assignments = element.Attributes.Select(attribute => LowerManagedAssignment(attribute, lowerExpression));
        var configure = "__props => { " + string.Join(" ", assignments) + " }";
        return element.Name
            + ".Create("
            + configure
            + ", "
            + LowerChildren(element.Children, lowerExpression)
            + ")";
    }

    private static string LowerManagedAssignment(
        FecsxAttributeSyntax attribute,
        Func<FecsxExpressionSyntax, string>? lowerExpression)
    {
        if (attribute is FecsxBooleanAttributeSyntax)
        {
            return "__props." + FecsxAttributeNaming.CSharpPropertyName(attribute.Name) + " = true;";
        }

        if (attribute is FecsxStringAttributeSyntax text)
        {
            return "__props." + FecsxAttributeNaming.CSharpPropertyName(attribute.Name) + " = "
                + SymbolDisplay.FormatLiteral(FecsxJsxText.DecodeEntities(text.Value), true)
                + ";";
        }

        if (attribute is FecsxExpressionAttributeSyntax expression)
        {
            return "__props." + FecsxAttributeNaming.CSharpPropertyName(attribute.Name) + " = " + LowerExpression(expression.Expression, lowerExpression) + ";";
        }

        throw new InvalidOperationException($"Unsupported FECSX attribute node '{attribute.GetType().Name}'.");
    }

    private static string LowerIntrinsicProps(
        string elementName,
        IReadOnlyList<FecsxAttributeSyntax> attributes,
        Func<FecsxExpressionSyntax, string>? lowerExpression)
    {
        if (attributes.Count == 0)
        {
            return "null";
        }

        if (!IntrinsicElementSchema.TryGetElement(elementName, out var element))
        {
            throw new InvalidOperationException($"Intrinsic element '{elementName}' was not validated against the canonical schema.");
        }

        var assignments = new List<string>();
        foreach (var attribute in attributes)
        {
            if (!element.TryGetProperty(attribute.Name, out var property))
            {
                throw new InvalidOperationException($"Intrinsic attribute '{attribute.Name}' was not validated against the canonical schema.");
            }

            if (attribute is FecsxBooleanAttributeSyntax)
            {
                assignments.Add("__props." + property.PropertyName + " = true;");
                continue;
            }

            if (attribute is FecsxStringAttributeSyntax text)
            {
                assignments.Add("__props." + property.PropertyName + " = "
                    + SymbolDisplay.FormatLiteral(FecsxJsxText.DecodeEntities(text.Value), true) + ";");
                continue;
            }

            var expression = (FecsxExpressionAttributeSyntax)attribute;
            assignments.Add("__props." + property.PropertyName + " = " + LowerExpression(expression.Expression, lowerExpression) + ";");
        }

        return "global::FrontEndCSharp.Fecsx.Html." + element.PropsTypeName
            + ".Create(__props => { " + string.Join(" ", assignments) + " })";
    }

    private static string LowerChildren(
        IReadOnlyList<FecsxChildSyntax> children,
        Func<FecsxExpressionSyntax, string>? lowerExpression)
    {
        var lowered = children
            .Select(child => child is FecsxTextSyntax text
                ? LowerText(text.Text)
                : Lower(child, lowerExpression))
            .Where(child => child != null);
        return "new global::Interop.JS.React.ReactNode[] { "
            + string.Join(", ", lowered)
            + " }";
    }

    private static string? LowerText(string text)
    {
        var normalized = FecsxJsxText.NormalizeChildText(text);
        return normalized == null
            ? null
            : "global::Interop.JS.React.React.Text("
                + SymbolDisplay.FormatLiteral(normalized, true)
                + ")";
    }

    private static string LowerExpression(
        FecsxExpressionSyntax expression,
        Func<FecsxExpressionSyntax, string>? lowerExpression) =>
        lowerExpression == null ? expression.Text : lowerExpression(expression);

    internal static bool IsManagedComponent(string name)
    {
        var lastSegment = name.Substring(name.LastIndexOf('.') + 1);
        return lastSegment.Length != 0 && char.IsUpper(lastSegment[0]);
    }
}
