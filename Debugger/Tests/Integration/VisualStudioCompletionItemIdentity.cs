using System.Text.RegularExpressions;

namespace FrontEndCSharp.Debugger.Integration;

internal static class VisualStudioCompletionItemIdentity
{
    private static readonly Regex PositionSuffix = new(
        @"\.\s+\d+\s+of\s+\d+$",
        RegexOptions.CultureInvariant);

    public static bool Matches(string expected, string automationId, string name) =>
        string.Equals(automationId, expected, StringComparison.Ordinal)
        || string.Equals(NormalizeName(name), expected, StringComparison.Ordinal)
        || (name.StartsWith(expected, StringComparison.Ordinal)
            && name.Length > expected.Length
            && name[expected.Length] == '.');

    public static string NormalizeName(string name) =>
        PositionSuffix.Replace(name.Trim(), string.Empty);
}
