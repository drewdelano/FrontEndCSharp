using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace FrontEndCSharp.Fecsx;

/// <summary>
/// Applies the semantic JSX text rules after parsing has preserved the authored
/// text and spans. This follows TypeScript 5.2.2's JSX transform rather than an
/// HTML parser: single-line whitespace is significant, indentation around line
/// breaks is removed, meaningful lines are joined by one space, and only
/// semicolon-terminated JSX entity forms are decoded.
/// </summary>
internal static class FecsxJsxText
{
    private static readonly Regex EntityPattern = new Regex(
        @"&((#((\d+)|x([\da-fA-F]+)))|(\w+));",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ECMAScript);

    public static string? NormalizeChildText(string text)
    {
        string? result = null;
        var firstNonWhitespace = 0;
        var lastNonWhitespace = -1;

        for (var index = 0; index < text.Length; index++)
        {
            var value = text[index];
            if (IsLineBreak(value))
            {
                if (firstNonWhitespace != -1 && lastNonWhitespace != -1)
                {
                    result = AddLine(
                        result,
                        text.Substring(firstNonWhitespace, lastNonWhitespace - firstNonWhitespace + 1));
                }

                firstNonWhitespace = -1;
            }
            else if (!IsSingleLineWhitespace(value))
            {
                lastNonWhitespace = index;
                if (firstNonWhitespace == -1)
                {
                    firstNonWhitespace = index;
                }
            }
        }

        return firstNonWhitespace != -1
            ? AddLine(result, text.Substring(firstNonWhitespace))
            : result;
    }

    public static string DecodeEntities(string text) =>
        EntityPattern.Replace(text, match =>
        {
            if (match.Groups[4].Success)
            {
                return DecodeCodePoint(match, match.Groups[4].Value, NumberStyles.None);
            }

            if (match.Groups[5].Success)
            {
                return DecodeCodePoint(match, match.Groups[5].Value, NumberStyles.AllowHexSpecifier);
            }

            var decoded = WebUtility.HtmlDecode(match.Value);
            return string.Equals(decoded, match.Value, StringComparison.Ordinal) ? match.Value : decoded;
        });

    private static string AddLine(string? current, string line)
    {
        var decoded = DecodeEntities(line);
        return current == null ? decoded : current + " " + decoded;
    }

    private static string DecodeCodePoint(
        Match match,
        string digits,
        NumberStyles styles)
    {
        if (!int.TryParse(digits, styles, CultureInfo.InvariantCulture, out var codePoint)
            || codePoint < 0
            || codePoint > 0x10FFFF)
        {
            return match.Value;
        }

        if (codePoint <= char.MaxValue)
        {
            return new string((char)codePoint, 1);
        }

        var value = codePoint - 0x10000;
        return new string(new[]
        {
            (char)((value >> 10) + 0xD800),
            (char)((value & 0x3FF) + 0xDC00),
        });
    }

    private static bool IsLineBreak(char value) =>
        value == '\r' || value == '\n' || value == '\u2028' || value == '\u2029';

    private static bool IsSingleLineWhitespace(char value) =>
        !IsLineBreak(value) && (char.IsWhiteSpace(value) || value == '\uFEFF');
}
