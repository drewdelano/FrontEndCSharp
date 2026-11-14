using FrontEndCSharp.Fecsx.Syntax;

namespace FrontEndCSharp.Fecsx;

internal static class FecsxIntrinsicValidator
{
    public static IReadOnlyList<FecsxDiagnostic> Validate(
        FecsxChildSyntax root,
        FecsxAttributeCasing attributeCasing)
    {
        var diagnostics = new List<FecsxDiagnostic>();
        ValidateChild(root, attributeCasing, diagnostics);
        return diagnostics;
    }

    private static void ValidateChild(
        FecsxChildSyntax child,
        FecsxAttributeCasing attributeCasing,
        List<FecsxDiagnostic> diagnostics)
    {
        if (child is FecsxFragmentSyntax fragment)
        {
            foreach (var fragmentChild in fragment.Children)
            {
                ValidateChild(fragmentChild, attributeCasing, diagnostics);
            }
            return;
        }

        if (child is not FecsxElementSyntax element)
        {
            return;
        }

        if (FecsxLowerer.IsManagedComponent(element.Name))
        {
            ValidateManagedElement(element, attributeCasing, diagnostics);
        }
        else
        {
            ValidateIntrinsicElement(element, attributeCasing, diagnostics);
        }

        foreach (var elementChild in element.Children)
        {
            ValidateChild(elementChild, attributeCasing, diagnostics);
        }
    }

    private static void ValidateManagedElement(
        FecsxElementSyntax element,
        FecsxAttributeCasing attributeCasing,
        List<FecsxDiagnostic> diagnostics)
    {
        foreach (var attribute in element.Attributes)
        {
            if (!FecsxAttributeNaming.IsPreferredComponentName(attribute.Name, attributeCasing))
            {
                AddCasingDiagnostic(
                    attribute,
                    FecsxAttributeNaming.ExpectedComponentName(attribute.Name, attributeCasing),
                    attributeCasing,
                    diagnostics);
            }
        }
    }

    private static void ValidateIntrinsicElement(
        FecsxElementSyntax element,
        FecsxAttributeCasing attributeCasing,
        List<FecsxDiagnostic> diagnostics)
    {
        if (!IntrinsicElementSchema.TryGetElement(element.Name, out var descriptor))
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX4001",
                $"Intrinsic element '{element.Name}' is not present in the canonical FECSX HTML schema.",
                element.NameSpan));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in element.Attributes)
        {
            if (!descriptor.TryGetProperty(attribute.Name, out var property))
            {
                diagnostics.Add(new FecsxDiagnostic(
                    "FECSX4002",
                    $"Attribute '{attribute.Name}' is not valid on intrinsic element '{element.Name}'.",
                    attribute.NameSpan));
                continue;
            }

            if (!seen.Add(property.PropertyName))
            {
                diagnostics.Add(new FecsxDiagnostic("FECSX4005", $"Attribute '{attribute.Name}' is specified more than once.", attribute.NameSpan));
                continue;
            }

            var preferredName = FecsxAttributeNaming.PreferredIntrinsicName(property, attributeCasing);
            if (!string.Equals(attribute.Name, preferredName, StringComparison.Ordinal))
            {
                AddCasingDiagnostic(
                    attribute,
                    preferredName,
                    attributeCasing,
                    diagnostics);
                continue;
            }

            if (attribute is FecsxBooleanAttributeSyntax && property.Kind != IntrinsicPropertyKind.Boolean)
            {
                diagnostics.Add(new FecsxDiagnostic(
                    "FECSX4003",
                    $"Attribute '{attribute.Name}' requires a value.",
                    attribute.NameSpan));
            }
            else if (attribute is FecsxStringAttributeSyntax && property.Kind != IntrinsicPropertyKind.String)
            {
                diagnostics.Add(new FecsxDiagnostic(
                    "FECSX4004",
                    $"Attribute '{attribute.Name}' requires a C# expression value of type '{GetTypeName(property.Kind)}'.",
                    attribute.Span));
            }
        }
    }

    private static void AddCasingDiagnostic(
        FecsxAttributeSyntax attribute,
        string preferredName,
        FecsxAttributeCasing attributeCasing,
        List<FecsxDiagnostic> diagnostics) =>
        diagnostics.Add(new FecsxDiagnostic(
            "FECSX4006",
            $"Attribute '{attribute.Name}' does not match the configured {GetCasingName(attributeCasing)} FECSX casing; use '{preferredName}'.",
            attribute.NameSpan));

    private static string GetCasingName(FecsxAttributeCasing attributeCasing) =>
        attributeCasing == FecsxAttributeCasing.PascalCase ? "PascalCase" : "camelCase";

    private static string GetTypeName(IntrinsicPropertyKind kind) =>
        kind == IntrinsicPropertyKind.Boolean ? "bool"
        : kind == IntrinsicPropertyKind.Number ? "double"
        : kind == IntrinsicPropertyKind.EventHandler ? "Action<DomEvent>"
        : "string";
}
