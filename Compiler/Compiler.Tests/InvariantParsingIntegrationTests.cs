namespace Compiler.Tests;

public sealed class InvariantParsingIntegrationTests : IntegrationTestBase
{
    [Test]
    public void NumericParseAndTryParseMatchInvariantContracts()
    {
        const string source = """
            using System.Globalization;

            Console.WriteLine(sbyte.Parse(" -128 ").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(sbyte.TryParse("128", out var sbyteResult)); Console.WriteLine(sbyteResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(byte.Parse(" 255 ").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(byte.TryParse("-1", out var byteResult)); Console.WriteLine(byteResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(short.Parse("-32768").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(short.TryParse("32768", out var shortResult)); Console.WriteLine(shortResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(ushort.Parse("65535").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(ushort.TryParse("-1", out var ushortResult)); Console.WriteLine(ushortResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(int.Parse("-2147483648").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(int.TryParse("2147483648", out var intResult)); Console.WriteLine(intResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(uint.Parse("4294967295").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(uint.TryParse("-1", out var uintResult)); Console.WriteLine(uintResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(long.Parse("-9223372036854775808").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(long.TryParse("9223372036854775808", out var longResult)); Console.WriteLine(longResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(ulong.Parse("18446744073709551615").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(ulong.TryParse("-1", out var ulongResult)); Console.WriteLine(ulongResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(nint.Parse("-2147483648").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(nint.TryParse("2147483648", out var nintResult)); Console.WriteLine(nintResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(nuint.Parse("4294967295").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(nuint.TryParse("4294967296", out var nuintResult)); Console.WriteLine(nuintResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(Int128.Parse("-170141183460469231731687303715884105728").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(Int128.TryParse("170141183460469231731687303715884105728", out var int128Result)); Console.WriteLine(int128Result.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(UInt128.Parse("340282366920938463463374607431768211455").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(UInt128.TryParse("-1", out var uint128Result)); Console.WriteLine(uint128Result.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(Half.Parse("1.5").ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine(Half.TryParse("not-a-number", out var halfResult)); Console.WriteLine(halfResult.ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine(float.Parse("3.4028235E+38").ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine(float.TryParse("not-a-number", out var singleResult)); Console.WriteLine(singleResult.ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine(double.Parse("1.7976931348623157E+308").ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine(double.TryParse("not-a-number", out var doubleResult)); Console.WriteLine(doubleResult.ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine(decimal.Parse("79228162514264337593543950335").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(decimal.TryParse("79228162514264337593543950336", out var decimalResult)); Console.WriteLine(decimalResult.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(double.Parse("1,,2").ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine(double.Parse("1e400").ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine(Half.Parse("65520").ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine(decimal.Parse("1.23456789012345678901234567895").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(decimal.Parse("123-").ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(int.TryParse((string?)null, out intResult)); Console.WriteLine(intResult.ToString(CultureInfo.InvariantCulture));

            try { _ = int.Parse(null!); Console.WriteLine("wrong"); }
            catch (ArgumentNullException) { Console.WriteLine("ArgumentNullException"); }
            try { _ = int.Parse("not-a-number"); Console.WriteLine("wrong"); }
            catch (FormatException) { Console.WriteLine("FormatException"); }
            try { _ = int.Parse("2147483648"); Console.WriteLine("wrong"); }
            catch (OverflowException) { Console.WriteLine("OverflowException"); }
            try { _ = decimal.Parse("79228162514264337593543950336"); Console.WriteLine("wrong"); }
            catch (OverflowException) { Console.WriteLine("OverflowException"); }
            """;

        var expected = new[]
        {
            "-128", "False", "0", "255", "False", "0",
            "-32768", "False", "0", "65535", "False", "0",
            "-2147483648", "False", "0", "4294967295", "False", "0",
            "-9223372036854775808", "False", "0", "18446744073709551615", "False", "0",
            "-2147483648", "False", "0", "4294967295", "False", "0",
            "-170141183460469231731687303715884105728", "False", "0",
            "340282366920938463463374607431768211455", "False", "0",
            "1.5", "False", "0", "3.4028235E+38", "False", "0",
            "1.7976931348623157E+308", "False", "0",
            "79228162514264337593543950335", "False", "0",
            "12", "Infinity", "Infinity", "1.2345678901234567890123456790", "-123", "False", "0",
            "ArgumentNullException", "FormatException", "OverflowException", "OverflowException"
        };
        Assert.Equal(string.Join('\n', expected), CompileAndRun(source));
    }

    [Test]
    public void EnumTemporalDurationAndGuidParseAndTryParseMatchInvariantContracts()
    {
        const string source = """
            using System.Globalization;

            Console.WriteLine(Enum.Parse<Access>(" Read, Write ").ToString("D"));
            Console.WriteLine(Enum.Parse<Access>("-1").ToString("D"));
            Console.WriteLine(Enum.TryParse<Access>("Read, Missing", out var enumResult)); Console.WriteLine(enumResult.ToString("D"));
            Console.WriteLine(Enum.TryParse<Access>(null, out enumResult)); Console.WriteLine(enumResult.ToString("D"));
            Console.WriteLine(Enum.TryParse<Access>("32768", out enumResult)); Console.WriteLine(enumResult.ToString("D"));

            Console.WriteLine(DateTime.Parse("02/29/2024 23:07:05").ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine(DateTime.TryParse("not-a-date", out var dateTimeResult)); Console.WriteLine(dateTimeResult.ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine(DateTimeOffset.Parse("02/29/2024 23:07:05 +05:30").ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine(DateTimeOffset.TryParse("not-an-offset", out var dateTimeOffsetResult)); Console.WriteLine(dateTimeOffsetResult.ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine(DateOnly.Parse("02/29/2024").ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine(DateOnly.TryParse("02/30/2024", out var dateOnlyResult)); Console.WriteLine(dateOnlyResult.ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine(TimeOnly.Parse("11:07:05 PM").ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine(TimeOnly.TryParse("25:00", out var timeOnlyResult)); Console.WriteLine(timeOnlyResult.ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine(TimeSpan.Parse("1.02:03:04.5000000").ToString("c", CultureInfo.InvariantCulture));
            Console.WriteLine(TimeSpan.Parse("24:00:00").ToString("c", CultureInfo.InvariantCulture));
            Console.WriteLine(TimeSpan.TryParse("not-a-duration", out var timeSpanResult)); Console.WriteLine(timeSpanResult.ToString("c", CultureInfo.InvariantCulture));

            Console.WriteLine(Guid.Parse(" {00112233-4455-6677-8899-aabbccddeeff} ").ToString("D"));
            Console.WriteLine(Guid.Parse("00112233445566778899aabbccddeeff").ToString("D"));
            Console.WriteLine(Guid.Parse("(00112233-4455-6677-8899-aabbccddeeff)").ToString("D"));
            Console.WriteLine(Guid.Parse("{0x00112233,0x4455,0x6677,{0x88,0x99,0xaa,0xbb,0xcc,0xdd,0xee,0xff}}").ToString("D"));
            Console.WriteLine(Guid.TryParse("not-a-guid", out var guidResult)); Console.WriteLine(guidResult.ToString("D"));
            Console.WriteLine(Guid.TryParse("{(00112233-4455-6677-8899-aabbccddeeff)}", out _));

            try { _ = DateTime.Parse(null!); Console.WriteLine("wrong"); }
            catch (ArgumentNullException) { Console.WriteLine("ArgumentNullException"); }
            try { _ = DateTime.Parse("not-a-date"); Console.WriteLine("wrong"); }
            catch (FormatException) { Console.WriteLine("FormatException"); }
            try { _ = Guid.Parse(null!); Console.WriteLine("wrong"); }
            catch (ArgumentNullException) { Console.WriteLine("ArgumentNullException"); }
            try { _ = Enum.Parse<Access>("Missing"); Console.WriteLine("wrong"); }
            catch (ArgumentException) { Console.WriteLine("ArgumentException"); }
            try { _ = TimeSpan.Parse("10675199.02:48:05.4775808"); Console.WriteLine("wrong"); }
            catch (OverflowException) { Console.WriteLine("OverflowException"); }

            [Flags]
            enum Access : short
            {
                None = 0,
                Read = 1,
                Write = 2
            }
            """;

        const string expected = """
            3
            -1
            False
            0
            False
            0
            False
            0
            2024-02-29T23:07:05.0000000
            False
            0001-01-01T00:00:00.0000000
            2024-02-29T23:07:05.0000000+05:30
            False
            0001-01-01T00:00:00.0000000+00:00
            2024-02-29
            False
            0001-01-01
            23:07:05.0000000
            False
            00:00:00.0000000
            1.02:03:04.5000000
            24.00:00:00
            False
            00:00:00
            00112233-4455-6677-8899-aabbccddeeff
            00112233-4455-6677-8899-aabbccddeeff
            00112233-4455-6677-8899-aabbccddeeff
            00112233-4455-6677-8899-aabbccddeeff
            False
            00000000-0000-0000-0000-000000000000
            False
            ArgumentNullException
            FormatException
            ArgumentNullException
            ArgumentException
            OverflowException
            """;
        Assert.Equal(expected.TrimEnd(), CompileAndRun(source));
    }
}
