using System.Globalization;
using System.Text;

namespace Compiler.Tests;

public sealed class EnumFormattingIntegrationTests : IntegrationTestBase
{
    [Test]
    public void StandardFormatsAndInterpolationMatchDesktopDotNet()
    {
        var cases = new EnumFormatCase[]
        {
            new("(SByteValue)(-1)", (SByteValue)(-1), "G"),
            new("SByteValue.Negative", SByteValue.Negative, "X"),
            new("ByteValue.Maximum", ByteValue.Maximum, "D"),
            new("Int16Value.Minimum", Int16Value.Minimum, "X"),
            new("UInt16Value.Maximum", UInt16Value.Maximum, "X"),
            new("Int32Value.Alias", Int32Value.Alias, "G"),
            new("(Int32Value)42", (Int32Value)42, "G"),
            new("UInt32Value.Maximum", UInt32Value.Maximum, "D"),
            new("Int64Value.Minimum", Int64Value.Minimum, "X"),
            new("UInt64Value.Maximum", UInt64Value.Maximum, "G"),
            new("UInt64Value.Maximum", UInt64Value.Maximum, "X"),
            new("ByteFlags.ReadWrite", ByteFlags.ReadWrite, "G"),
            new("ByteFlags.Read | ByteFlags.Execute", ByteFlags.Read | ByteFlags.Execute, "G"),
            new("ByteFlags.Write | ByteFlags.Execute", ByteFlags.Write | ByteFlags.Execute, "F"),
            new("(ByteFlags)8", (ByteFlags)8, "F"),
            new("PlainBits.One | PlainBits.Four", PlainBits.One | PlainBits.Four, "F"),
            new("(PlainBits)0", (PlainBits)0, "F"),
            new("SignedFlags.All", SignedFlags.All, "G"),
            new("SignedFlags.Low | SignedFlags.High", SignedFlags.Low | SignedFlags.High, "F"),
            new("ByteFlags.None", ByteFlags.None, "F")
        };

        var source = new StringBuilder("using System.Globalization;\n");
        var expected = new List<string>(cases.Length * 2);
        for (var index = 0; index < cases.Length; index++)
        {
            var testCase = cases[index];
            source.Append("var value").Append(index).Append(" = ")
                .Append(testCase.Expression).AppendLine(";");
            source.Append("Console.WriteLine(value").Append(index).Append(".ToString(\"")
                .Append(testCase.Format).AppendLine("\"));");
            source.Append("Console.WriteLine($\"{value").Append(index).Append(':')
                .Append(testCase.Format).AppendLine("}\");");

            var formatted = testCase.Value.ToString(testCase.Format);
            expected.Add(formatted);
            expected.Add(formatted);
        }

        source.AppendLine(EnumDeclarations);

        Assert.Equal(string.Join('\n', expected), CompileAndRun(source.ToString()));
    }

    [Test]
    public void PublicOverloadsNullFormatsAndInvalidFormatsMatchDesktopDotNet()
    {
        const string source = """
            using System.Globalization;

            var value = ByteFlags.Read | ByteFlags.Execute;
            Console.WriteLine(value.ToString());
            Console.WriteLine(value.ToString((string?)null));
            Console.WriteLine(value.ToString(string.Empty));
            Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(value.ToString("f", CultureInfo.InvariantCulture));
            Console.WriteLine(value.ToString("d"));
            Console.WriteLine(value.ToString("x"));
            try { Console.WriteLine(value.ToString("Q")); }
            catch (FormatException) { Console.WriteLine("FormatException"); }
            try { Console.WriteLine(value.ToString("GG", CultureInfo.InvariantCulture)); }
            catch (FormatException) { Console.WriteLine("FormatException"); }

            [Flags]
            enum ByteFlags : byte
            {
                None = 0,
                Read = 1,
                AliasRead = Read,
                Write = 2,
                ReadWrite = Read | Write,
                Execute = 4
            }
            """;

        var value = ByteFlags.Read | ByteFlags.Execute;
#pragma warning disable CS0618
        var expected = string.Join('\n',
            value.ToString(),
            value.ToString((string?)null),
            value.ToString(string.Empty),
            value.ToString(CultureInfo.InvariantCulture),
            value.ToString("f", CultureInfo.InvariantCulture),
            value.ToString("d"),
            value.ToString("x"),
            "FormatException",
            "FormatException");
        Assert.Throws<FormatException>(() => value.ToString("Q"));
        Assert.Throws<FormatException>(() => value.ToString("GG", CultureInfo.InvariantCulture));
#pragma warning restore CS0618
        Assert.Equal(expected, CompileAndRun(source));
    }

    private const string EnumDeclarations = """
        enum SByteValue : sbyte { Negative = -1, One = 1 }
        enum ByteValue : byte { Maximum = byte.MaxValue }
        enum Int16Value : short { Minimum = short.MinValue }
        enum UInt16Value : ushort { Maximum = ushort.MaxValue }
        enum Int32Value : int { First = 1, Alias = First }
        enum UInt32Value : uint { Maximum = uint.MaxValue }
        enum Int64Value : long { Minimum = long.MinValue }
        enum UInt64Value : ulong { Maximum = ulong.MaxValue }
        [Flags]
        enum ByteFlags : byte
        {
            None = 0,
            Read = 1,
            AliasRead = Read,
            Write = 2,
            ReadWrite = Read | Write,
            Execute = 4
        }
        enum PlainBits : byte { One = 1, Two = 2, Four = 4 }
        [Flags]
        enum SignedFlags : long { None = 0, Low = 1, High = long.MinValue, All = -1 }
        """;

    private sealed record EnumFormatCase(string Expression, Enum Value, string Format);

    private enum SByteValue : sbyte { Negative = -1, One = 1 }
    private enum ByteValue : byte { Maximum = byte.MaxValue }
    private enum Int16Value : short { Minimum = short.MinValue }
    private enum UInt16Value : ushort { Maximum = ushort.MaxValue }
    private enum Int32Value : int { First = 1, Alias = First }
    private enum UInt32Value : uint { Maximum = uint.MaxValue }
    private enum Int64Value : long { Minimum = long.MinValue }
    private enum UInt64Value : ulong { Maximum = ulong.MaxValue }
    [Flags]
    private enum ByteFlags : byte
    {
        None = 0,
        Read = 1,
        AliasRead = Read,
        Write = 2,
        ReadWrite = Read | Write,
        Execute = 4
    }
    private enum PlainBits : byte { One = 1, Two = 2, Four = 4 }
    [Flags]
    private enum SignedFlags : long { None = 0, Low = 1, High = long.MinValue, All = -1 }
}
