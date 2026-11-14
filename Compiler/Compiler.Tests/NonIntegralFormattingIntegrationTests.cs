using System.Globalization;
using System.Text;

namespace Compiler.Tests;

public sealed class NonIntegralFormattingIntegrationTests : IntegrationTestBase
{
    [Test]
    public void FloatingPointAndDecimalStandardFormatsMatchDesktopDotNet()
    {
        AssertParity(
        [
            new("Half.One", Half.One, "G"),
            new("Half.MaxValue", Half.MaxValue, "E2"),
            new("Half.NegativeZero", Half.NegativeZero, "R"),
            new("Half.NaN", Half.NaN, "G"),
            new("Half.PositiveInfinity", Half.PositiveInfinity, "G"),
            new("-0.0f", -0.0f, "R"),
            new("1.2345678f", 1.2345678f, "G"),
            new("float.MaxValue", float.MaxValue, "E3"),
            new("0.00001f", 0.00001f, "G4"),
            new("float.NaN", float.NaN, "G"),
            new("float.NegativeInfinity", float.NegativeInfinity, "G"),
            new("-0.0d", -0.0d, "R"),
            new("1.2345678901234567d", 1.2345678901234567d, "G"),
            new("double.MaxValue", double.MaxValue, "E6"),
            new("1.0e-10d", 1.0e-10d, "G5"),
            new("double.NaN", double.NaN, "G"),
            new("double.PositiveInfinity", double.PositiveInfinity, "G"),
            new("1.2300m", 1.2300m, "G"),
            new("decimal.MaxValue", decimal.MaxValue, "E6"),
            new("-1234567.890123m", -1234567.890123m, "C3"),
            new("0.0125m", 0.0125m, "P2")
        ]);
    }

    [Test]
    public void FloatingPointAndDecimalCustomFormatsMatchDesktopDotNet()
    {
        AssertParity(
        [
            new("Half.MaxValue", Half.MaxValue, "#,##0.00"),
            new("-12.5f", -12.5f, "positive;[negative];zero"),
            new("12345.678f", 12345.678f, "#,##0.00"),
            new("0.0375f", 0.0375f, "%#0.000"),
            new("9876543210.125d", 9876543210.125d, "0.0000E+00"),
            new("0.0000123456789d", 0.0000123456789d, "0.###E+0"),
            new("-0.0d", -0.0d, "positive;negative;zero"),
            new("123456789.0123456789012345678m", 123456789.0123456789012345678m, "#,##0.############################"),
            new("-42.1250m", -42.1250m, "0.00;(0.00);'zero'"),
            new("100000000m", 100000000m, "#,##0,, 'million'"),
            new("0m", 0m, "positive;negative;'zero'")
        ]);
    }

    [Test]
    public void NonIntegralPublicOverloadsAndInvalidFormatsMatchDesktopDotNet()
    {
        var cases = new FormatCase[]
        {
            new("Half.MaxValue", Half.MaxValue, "F1"),
            new("-7.25f", -7.25f, "F1"),
            new("-7.25d", -7.25d, "F1"),
            new("-7.250m", -7.250m, "F1")
        };
        var source = new StringBuilder("using System.Globalization;\n");
        var expected = new List<string>(cases.Length * 5);
        for (var index = 0; index < cases.Length; index++)
        {
            var testCase = cases[index];
            source.Append("var value").Append(index).Append(" = ")
                .Append(testCase.Expression).AppendLine(";");
            source.Append("Console.WriteLine(value").Append(index).AppendLine(".ToString());");
            source.Append("Console.WriteLine(value").Append(index)
                .AppendLine(".ToString(CultureInfo.InvariantCulture));");
            source.Append("Console.WriteLine(value").Append(index).Append(".ToString(")
                .Append(Literal(testCase.Format)).AppendLine("));");
            source.Append("Console.WriteLine(value").Append(index).Append(".ToString(")
                .Append(Literal(testCase.Format)).AppendLine(", CultureInfo.InvariantCulture));");
            source.Append("try { Console.WriteLine(value").Append(index)
                .AppendLine(".ToString(\"Q\", CultureInfo.InvariantCulture)); } "
                    + "catch (FormatException) { Console.WriteLine(\"FormatException\"); }");

            expected.Add(testCase.Value.ToString(null, CultureInfo.InvariantCulture));
            expected.Add(testCase.Value.ToString(null, CultureInfo.InvariantCulture));
            expected.Add(testCase.Value.ToString(testCase.Format, CultureInfo.InvariantCulture));
            expected.Add(testCase.Value.ToString(testCase.Format, CultureInfo.InvariantCulture));
            Assert.Throws<FormatException>(() => testCase.Value.ToString("Q", CultureInfo.InvariantCulture));
            expected.Add("FormatException");
        }

        Assert.Equal(string.Join('\n', expected), CompileAndRun(source.ToString()));
    }

    private void AssertParity(IReadOnlyList<FormatCase> cases)
    {
        var source = new StringBuilder("using System.Globalization;\n");
        var expected = new List<string>(cases.Count * 2);
        for (var index = 0; index < cases.Count; index++)
        {
            var testCase = cases[index];
            source.Append("var value").Append(index).Append(" = ")
                .Append(testCase.Expression).AppendLine(";");
            source.Append("Console.WriteLine(value").Append(index).Append(".ToString(")
                .Append(Literal(testCase.Format)).AppendLine(", CultureInfo.InvariantCulture));");
            source.Append("Console.WriteLine($\"{value").Append(index).Append(':')
                .Append(testCase.Format.Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal))
                .AppendLine("}\");");

            var formatted = testCase.Value.ToString(testCase.Format, CultureInfo.InvariantCulture);
            expected.Add(formatted);
            expected.Add(formatted);
        }

        Assert.Equal(string.Join('\n', expected), CompileAndRun(source.ToString()));
    }

    private static string Literal(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private sealed record FormatCase(string Expression, IFormattable Value, string Format);
}
