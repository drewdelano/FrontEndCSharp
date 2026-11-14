namespace FrontEndCSharp.Fecsx;

public enum FecsxAttributeCasing
{
    CamelCase,
    PascalCase,
}

public static class FecsxAttributeNaming
{
    public const string ProjectPropertyName = "FrontEndCSharpFecsxAttributeCasing";
    public const string AnalyzerConfigPropertyName = "build_property." + ProjectPropertyName;

    public static bool TryParse(string? value, out FecsxAttributeCasing casing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            casing = FecsxAttributeCasing.CamelCase;
            return true;
        }
        if (string.Equals(value, "camelCase", StringComparison.OrdinalIgnoreCase))
        {
            casing = FecsxAttributeCasing.CamelCase;
            return true;
        }
        if (string.Equals(value, "PascalCase", StringComparison.OrdinalIgnoreCase))
        {
            casing = FecsxAttributeCasing.PascalCase;
            return true;
        }

        casing = FecsxAttributeCasing.CamelCase;
        return false;
    }

    public static string PreferredIntrinsicName(
        IntrinsicPropertyDescriptor property,
        FecsxAttributeCasing casing)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        return casing == FecsxAttributeCasing.PascalCase
            ? property.PropertyName
            : property.MarkupName;
    }

    public static string PreferredComponentName(
        string propertyName,
        FecsxAttributeCasing casing)
    {
        if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));
        return casing == FecsxAttributeCasing.CamelCase
            ? LowerFirstCharacter(propertyName)
            : propertyName;
    }

    public static string CSharpPropertyName(string authoredName)
    {
        if (authoredName == null) throw new ArgumentNullException(nameof(authoredName));
        return UpperFirstCharacter(authoredName);
    }

    public static string ExpectedComponentName(
        string authoredName,
        FecsxAttributeCasing casing)
    {
        if (authoredName == null) throw new ArgumentNullException(nameof(authoredName));
        return PreferredComponentName(CSharpPropertyName(authoredName), casing);
    }

    public static bool IsPreferredComponentName(
        string authoredName,
        FecsxAttributeCasing casing) =>
        string.Equals(
            authoredName,
            ExpectedComponentName(authoredName, casing),
            StringComparison.Ordinal);

    public static bool MatchesComponentProperty(string authoredName, string propertyName) =>
        string.Equals(authoredName, propertyName, StringComparison.Ordinal)
        || string.Equals(
            authoredName,
            LowerFirstCharacter(propertyName),
            StringComparison.Ordinal);

    private static string LowerFirstCharacter(string value)
    {
        if (value.Length == 0 || !char.IsUpper(value[0])) return value;
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string UpperFirstCharacter(string value)
    {
        if (value.Length == 0 || !char.IsLower(value[0])) return value;
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
}
