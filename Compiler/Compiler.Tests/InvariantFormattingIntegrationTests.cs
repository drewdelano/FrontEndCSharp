using System.Globalization;
using System.Text;

namespace Compiler.Tests;

public sealed class InvariantFormattingIntegrationTests : IntegrationTestBase
{
    [Test]
    public void IntegralStandardFormatsMatchDesktopDotNet()
    {
        var cases = new[]
        {
            new IntegralFormatCase("(sbyte)-128", (sbyte)-128, "D4"),
            new IntegralFormatCase("(byte)255", (byte)255, "X4"),
            new IntegralFormatCase("(short)-32768", (short)-32768, "x"),
            new IntegralFormatCase("(ushort)65535", (ushort)65535, "B"),
            new IntegralFormatCase("int.MinValue", int.MinValue, "N0"),
            new IntegralFormatCase("uint.MaxValue", uint.MaxValue, "C0"),
            new IntegralFormatCase("long.MinValue", long.MinValue, "E3"),
            new IntegralFormatCase("ulong.MaxValue", ulong.MaxValue, "P0"),
            new IntegralFormatCase("(nint)(-42)", (nint)(-42), "D5"),
            new IntegralFormatCase("(nuint)42", (nuint)42, "X8"),
            new IntegralFormatCase("Int128.MinValue", Int128.MinValue, "D"),
            new IntegralFormatCase("UInt128.MaxValue", UInt128.MaxValue, "X")
        };

        AssertParity(cases);
    }

    [Test]
    public void IntegralCustomFormatsMatchDesktopDotNet()
    {
        var cases = new[]
        {
            new IntegralFormatCase("(sbyte)-42", (sbyte)-42, "00000"),
            new IntegralFormatCase("(byte)7", (byte)7, "'value='000"),
            new IntegralFormatCase("(short)-1234", (short)-1234, "#,##0;(#,##0);zero"),
            new IntegralFormatCase("(ushort)0", (ushort)0, "positive;negative;'zero'"),
            new IntegralFormatCase("123456789", 123456789, "#,#,,"),
            new IntegralFormatCase("37u", 37u, "%#0.00"),
            new IntegralFormatCase("-9_876_543_210L", -9_876_543_210L, "0.00E+00"),
            new IntegralFormatCase("12_345_678_901_234_567_890UL", 12_345_678_901_234_567_890UL, "#,##0.00‰"),
            new IntegralFormatCase("(nint)42", (nint)42, @"\#000"),
            new IntegralFormatCase("(nuint)123456", (nuint)123456, "##-##-##"),
            new IntegralFormatCase("Int128.MinValue", Int128.MinValue, "#,##0;[#,##0]"),
            new IntegralFormatCase("UInt128.MaxValue", UInt128.MaxValue, "0.000E+00")
        };

        AssertParity(cases);
    }

    [Test]
    public void IntegralPublicOverloadsAndInvalidFormatsMatchDesktopDotNet()
    {
        var cases = new[]
        {
            new IntegralFormatCase("(sbyte)-7", (sbyte)-7, "D3"),
            new IntegralFormatCase("(byte)7", (byte)7, "D3"),
            new IntegralFormatCase("(short)-7", (short)-7, "D3"),
            new IntegralFormatCase("(ushort)7", (ushort)7, "D3"),
            new IntegralFormatCase("-7", -7, "D3"),
            new IntegralFormatCase("7u", 7u, "D3"),
            new IntegralFormatCase("-7L", -7L, "D3"),
            new IntegralFormatCase("7UL", 7UL, "D3"),
            new IntegralFormatCase("(nint)(-7)", (nint)(-7), "D3"),
            new IntegralFormatCase("(nuint)7", (nuint)7, "D3"),
            new IntegralFormatCase("Int128.MinValue", Int128.MinValue, "D3"),
            new IntegralFormatCase("UInt128.MaxValue", UInt128.MaxValue, "D3")
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
                .Append(ToCSharpStringLiteral(testCase.Format)).AppendLine("));");
            source.Append("Console.WriteLine(value").Append(index).Append(".ToString(")
                .Append(ToCSharpStringLiteral(testCase.Format))
                .AppendLine(", CultureInfo.InvariantCulture));");
            source.Append("try { Console.WriteLine(value").Append(index)
                .AppendLine(".ToString(\"Q\", CultureInfo.InvariantCulture)); } "
                    + "catch (FormatException) { Console.WriteLine(\"FormatException\"); }");

            expected.Add(testCase.Value.ToString(null, CultureInfo.InvariantCulture));
            expected.Add(testCase.Value.ToString(null, CultureInfo.InvariantCulture));
            expected.Add(testCase.Value.ToString(testCase.Format, CultureInfo.InvariantCulture));
            expected.Add(testCase.Value.ToString(testCase.Format, CultureInfo.InvariantCulture));
            Assert.Throws<FormatException>(() =>
                testCase.Value.ToString("Q", CultureInfo.InvariantCulture));
            expected.Add("FormatException");
        }

        Assert.Equal(string.Join('\n', expected), CompileAndRun(source.ToString()));
    }

    private void AssertParity(IReadOnlyList<IntegralFormatCase> cases)
    {
        var source = new StringBuilder("using System.Globalization;\n");
        var expected = new List<string>(cases.Count * 2);
        for (var index = 0; index < cases.Count; index++)
        {
            var testCase = cases[index];
            var formatLiteral = ToCSharpStringLiteral(testCase.Format);
            source.Append("var value").Append(index).Append(" = ")
                .Append(testCase.Expression).AppendLine(";");
            source.Append("Console.WriteLine(value").Append(index).Append(".ToString(")
                .Append(formatLiteral).AppendLine(", CultureInfo.InvariantCulture));");
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

    private static string ToCSharpStringLiteral(string value) =>
        "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            + "\"";

    private sealed record IntegralFormatCase(
        string Expression,
        IFormattable Value,
        string Format);
}
