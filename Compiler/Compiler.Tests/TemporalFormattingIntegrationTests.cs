using System.Globalization;
using System.Text;

namespace Compiler.Tests;

public sealed class TemporalFormattingIntegrationTests : IntegrationTestBase
{
    // This full compiler parity gate is intentionally scheduled after parallel fixtures.
    [Test]
    [NonParallelizable]
    public void StandardFormatsAndInterpolationMatchDesktopDotNet()
    {
        var cases = new TemporalFormatCase[]
        {
            new(
                "new DateTime(2024, 2, 29, 23, 7, 5, 123, DateTimeKind.Unspecified).AddTicks(4567)",
                new DateTime(2024, 2, 29, 23, 7, 5, 123, DateTimeKind.Unspecified).AddTicks(4567),
                ["d", "D", "f", "F", "g", "G", "M", "O", "R", "s", "t", "T", "u", "U", "Y"]),
            new(
                "new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)",
                new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                ["O", "yyyyy-MM-dd HH:mm:ss.fffffff K"]),
            new(
                "new DateTime(9999, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9999)",
                new DateTime(9999, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9999),
                ["O", "yyyy-MM-dd HH:mm:ss.fffffff K"]),
            new(
                "new DateTimeOffset(2024, 2, 29, 23, 7, 5, 123, TimeSpan.FromMinutes(330)).AddTicks(4567)",
                new DateTimeOffset(2024, 2, 29, 23, 7, 5, 123, TimeSpan.FromMinutes(330)).AddTicks(4567),
                ["d", "D", "f", "F", "g", "G", "M", "O", "R", "s", "t", "T", "u", "Y"]),
            new(
                "new DateOnly(2024, 2, 29)",
                new DateOnly(2024, 2, 29),
                ["d", "D", "M", "O", "R", "Y"]),
            new(
                "new DateOnly(1, 1, 1)",
                new DateOnly(1, 1, 1),
                ["O", "R"]),
            new(
                "new DateOnly(9999, 12, 31)",
                new DateOnly(9999, 12, 31),
                ["O", "R"]),
            new(
                "new TimeOnly(23, 7, 5, 123).Add(TimeSpan.FromTicks(4567))",
                new TimeOnly(23, 7, 5, 123).Add(TimeSpan.FromTicks(4567)),
                ["t", "T", "O", "R"]),
            new(
                "TimeSpan.FromTicks(937841234567L)",
                TimeSpan.FromTicks(937841234567L),
                ["c", "t", "T", "g", "G"]),
            new(
                "TimeSpan.FromTicks(-937841234567L)",
                TimeSpan.FromTicks(-937841234567L),
                ["c", "g", "G"]),
            new(
                "TimeSpan.FromTicks(long.MinValue)",
                TimeSpan.FromTicks(long.MinValue),
                ["c", "g", "G"]),
            new(
                "TimeSpan.FromTicks(long.MaxValue)",
                TimeSpan.FromTicks(long.MaxValue),
                ["c", "g", "G"]),
        };

        AssertFormatParity(cases);
    }

    [Test]
    public void CustomFormatsMatchDesktopDotNet()
    {
        var dateTime = new DateTime(2024, 2, 29, 23, 7, 5, 123, DateTimeKind.Utc).AddTicks(4567);
        var dateTimeOffset = new DateTimeOffset(
            2024, 2, 29, 23, 7, 5, 123, TimeSpan.FromMinutes(-210)).AddTicks(4567);
        var cases = new TemporalFormatCase[]
        {
            new(
                "new DateTime(2024, 2, 29, 23, 7, 5, 123, DateTimeKind.Utc).AddTicks(4567)",
                dateTime,
                [
                    "yyyy/MM/dd ddd dddd", "M MM MMM MMMM", "y yy yyy yyyyy",
                    "h hh H HH m mm s ss t tt", "fffffff", "FFFFFFF",
                    "yyyy-MM-dd'T'HH:mm:ss.fffffffK", "'at' HH\\:mm", "%d",
                ]),
            new(
                "new DateTimeOffset(2024, 2, 29, 23, 7, 5, 123, TimeSpan.FromMinutes(-210)).AddTicks(4567)",
                dateTimeOffset,
                [
                    "yyyy-MM-dd'T'HH:mm:ss.fffffffK", "z zz zzz", "ddd, dd MMM yyyy",
                    "'offset='zzz 'clock='HH:mm:ss.FFFFFFF",
                ]),
            new(
                "new DateOnly(2024, 2, 29)",
                new DateOnly(2024, 2, 29),
                ["yyyy/MM/dd", "ddd 'day' dd MMMM yyyyy", "%d", "MM\\-dd\\-yyyy"]),
            new(
                "new TimeOnly(23, 7, 5, 123).Add(TimeSpan.FromTicks(4567))",
                new TimeOnly(23, 7, 5, 123).Add(TimeSpan.FromTicks(4567)),
                ["h:mm:ss.fffffff tt", "HH:mm:ss.FFFFFFF", "'clock='HH\\:mm", "%H"]),
            new(
                "TimeSpan.FromTicks(937841234567L)",
                TimeSpan.FromTicks(937841234567L),
                [@"d\.hh\:mm\:ss\.fffffff", @"dd\:h\:m\:s\.FFFFFFF", @"'elapsed='d\.hh\:mm", @"%d"]),
            new(
                "TimeSpan.FromTicks(-937841234567L)",
                TimeSpan.FromTicks(-937841234567L),
                [@"'minus 'd\.hh\:mm\:ss\.fffffff", @"dd\:hh\:mm\:ss"]),
        };

        AssertFormatParity(cases);
    }

    [Test]
    public void EveryPublicToStringOverloadUsesInvariantFormatting()
    {
        const string source = """
            using System.Globalization;

            static void WriteDateTime(DateTime value)
            {
                Console.WriteLine(value.ToString());
                Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine(value.ToString((string?)null));
                Console.WriteLine(value.ToString(null, CultureInfo.InvariantCulture));
            }
            static void WriteDateTimeOffset(DateTimeOffset value)
            {
                Console.WriteLine(value.ToString());
                Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine(value.ToString((string?)null));
                Console.WriteLine(value.ToString(null, CultureInfo.InvariantCulture));
            }
            static void WriteDateOnly(DateOnly value)
            {
                Console.WriteLine(value.ToString());
                Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine(value.ToString((string?)null));
                Console.WriteLine(value.ToString(null, CultureInfo.InvariantCulture));
            }
            static void WriteTimeOnly(TimeOnly value)
            {
                Console.WriteLine(value.ToString());
                Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine(value.ToString((string?)null));
                Console.WriteLine(value.ToString(null, CultureInfo.InvariantCulture));
            }
            static void WriteTimeSpan(TimeSpan value)
            {
                Console.WriteLine(value.ToString());
                Console.WriteLine(value.ToString((string?)null));
                Console.WriteLine(value.ToString(null, CultureInfo.InvariantCulture));
            }

            WriteDateTime(new DateTime(2024, 2, 29, 23, 7, 5, 123, DateTimeKind.Utc).AddTicks(4567));
            WriteDateTimeOffset(new DateTimeOffset(2024, 2, 29, 23, 7, 5, 123, TimeSpan.FromMinutes(330)).AddTicks(4567));
            WriteDateOnly(new DateOnly(2024, 2, 29));
            WriteTimeOnly(new TimeOnly(23, 7, 5, 123).Add(TimeSpan.FromTicks(4567)));
            WriteTimeSpan(TimeSpan.FromTicks(-937841234567L));
            """;

        var values = new IFormattable[]
        {
            new DateTime(2024, 2, 29, 23, 7, 5, 123, DateTimeKind.Utc).AddTicks(4567),
            new DateTimeOffset(2024, 2, 29, 23, 7, 5, 123, TimeSpan.FromMinutes(330)).AddTicks(4567),
            new DateOnly(2024, 2, 29),
            new TimeOnly(23, 7, 5, 123).Add(TimeSpan.FromTicks(4567)),
            TimeSpan.FromTicks(-937841234567L),
        };
        var expected = values.Take(4).SelectMany(value => Enumerable.Repeat(
                value.ToString(null, CultureInfo.InvariantCulture),
                4))
            .Concat(Enumerable.Repeat(
                values[4].ToString(null, CultureInfo.InvariantCulture),
                3));

        Assert.Equal(string.Join('\n', expected), CompileAndRun(source));
    }

    [Test]
    public void InvalidFormatsThrowManagedFormatException()
    {
        const string source = """
            using System.Globalization;

            static void Reject(Func<string> format)
            {
                try { Console.WriteLine(format()); }
                catch (FormatException) { Console.WriteLine("FormatException"); }
            }

            var dateTime = new DateTime(2024, 2, 29, 23, 7, 5, 123, DateTimeKind.Utc);
            Reject(() => dateTime.ToString("Q", CultureInfo.InvariantCulture));
            Reject(() => dateTime.ToString("ffffffff", CultureInfo.InvariantCulture));
            Reject(() => dateTime.ToString("'unterminated", CultureInfo.InvariantCulture));
            var dateTimeOffset = new DateTimeOffset(2024, 2, 29, 23, 7, 5, 0, TimeSpan.FromHours(2));
            Reject(() => dateTimeOffset.ToString("U", CultureInfo.InvariantCulture));
            Reject(() => dateTimeOffset.ToString("Q", CultureInfo.InvariantCulture));
            var dateOnly = new DateOnly(2024, 2, 29);
            Reject(() => dateOnly.ToString("T", CultureInfo.InvariantCulture));
            Reject(() => dateOnly.ToString("HH", CultureInfo.InvariantCulture));
            var timeOnly = new TimeOnly(23, 7, 5, 123);
            Reject(() => timeOnly.ToString("D", CultureInfo.InvariantCulture));
            Reject(() => timeOnly.ToString("yyyy", CultureInfo.InvariantCulture));
            var duration = TimeSpan.FromTicks(937841234567L);
            Reject(() => duration.ToString("Q", CultureInfo.InvariantCulture));
            Reject(() => duration.ToString("hhh", CultureInfo.InvariantCulture));
            Reject(() => duration.ToString("d:hh", CultureInfo.InvariantCulture));
            """;

        Assert.Equal(
            Enumerable.Repeat("FormatException", 12),
            CompileAndRun(source).Split('\n'));
    }

    private void AssertFormatParity(IReadOnlyList<TemporalFormatCase> cases)
    {
        var source = new StringBuilder("using System.Globalization;\n");
        var expected = new List<string>();
        for (var caseIndex = 0; caseIndex < cases.Count; caseIndex++)
        {
            var testCase = cases[caseIndex];
            source.Append("var value").Append(caseIndex).Append(" = ")
                .Append(testCase.Expression).AppendLine(";");
            foreach (var format in testCase.Formats)
            {
                source.Append("Console.WriteLine(value").Append(caseIndex).Append(".ToString(")
                    .Append(ToCSharpStringLiteral(format))
                    .AppendLine(", CultureInfo.InvariantCulture));");
                source.Append("Console.WriteLine($\"{value").Append(caseIndex).Append(':')
                    .Append(format.Replace("\\", "\\\\", StringComparison.Ordinal)
                        .Replace("\"", "\\\"", StringComparison.Ordinal))
                    .AppendLine("}\");");

                var formatted = testCase.Value.ToString(format, CultureInfo.InvariantCulture);
                expected.Add(formatted);
                expected.Add(formatted);
            }
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

    private sealed record TemporalFormatCase(
        string Expression,
        IFormattable Value,
        IReadOnlyList<string> Formats);
}
