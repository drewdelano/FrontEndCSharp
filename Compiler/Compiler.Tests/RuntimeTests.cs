using Compiler.Helpers;
using Compiler.Read;
using System.Reflection;

namespace Compiler.Tests;

public class RuntimeTests
{
    private static readonly Compiler.Helpers.RuntimeOverrideCollection RuntimeOverrides =
        Compiler.Helpers.RuntimeOverrideCollection.Load([
            Path.Combine(AppContext.BaseDirectory, "System.Private.CoreLib.fecs.dll")
        ]);

    [Test]
    public void NullableValueGetterUsesRuntimeOverride()
    {
        var getter = typeof(int?).GetProperty(nameof(Nullable<int>.Value))!.GetMethod!;

        Assert.True(RuntimeOverrides.HasRuntimeOverride(getter));
    }

    [Test]
    public void RuntimeOverrideLookupCachesHitsAndMisses()
    {
        var overrides = Compiler.Helpers.RuntimeOverrideCollection.Load([
            Path.Combine(AppContext.BaseDirectory, "System.Private.CoreLib.fecs.dll")
        ]);
        var hit = typeof(int?).GetProperty(nameof(Nullable<int>.Value))!.GetMethod!;
        var miss = typeof(RuntimeTests).GetMethod(nameof(RuntimeOverrideLookupCachesHitsAndMisses))!;

        Assert.True(overrides.HasRuntimeOverride(hit));
        Assert.Equal(1, overrides.CachedMethodLookupCount);
        Assert.True(overrides.HasRuntimeOverride(hit));
        Assert.Equal(1, overrides.CachedMethodLookupCount);
        Assert.NotEmpty(overrides.GetRuntimeOverrideFileName(hit));
        Assert.Equal(1, overrides.CachedMethodLookupCount);

        Assert.False(overrides.HasRuntimeOverride(miss));
        Assert.Equal(2, overrides.CachedMethodLookupCount);
        Assert.False(overrides.HasRuntimeOverride(miss));
        Assert.Equal(2, overrides.CachedMethodLookupCount);

        Assert.False(Compiler.Helpers.RuntimeOverrideCollection.Empty.HasRuntimeOverride(miss));
        Assert.Equal(0, Compiler.Helpers.RuntimeOverrideCollection.Empty.CachedMethodLookupCount);
    }

    [Test]
    public void GenericInterfaceCallFromGenericMethodUsesRuntimeOverride()
    {
        var contains = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(Enumerable.Contains)
                && method.GetParameters().Length == 3);
        var decoder = new MethodDecoder();
        decoder.LoadMethod(contains, validateRuntimeSemantics: false);
        var equalsMethods = decoder.ReferencedMethods.Keys
            .OfType<MethodInfo>()
            .Where(method => method.Name == nameof(IEqualityComparer<object>.Equals))
            .ToArray();

        Assert.All(equalsMethods, method => Assert.True(
            RuntimeOverrides.HasRuntimeOverride(
                GenericsHelper.GetPossiblyGenericReferenceFromMethod(method)),
            $"Missing override for {NameNormalizers.GetFullyQualifiedMethodName(method)}"));
    }

    [Test]
    public void CompileSimpleArithmetic()
    {
        var csharp = "int x = 5 + 3; Console.WriteLine(x);";
        var output = CompileAndRun(csharp);
        Assert.Equal("8", output);
    }

    [Test]
    public void ExplicitInterfaceGetterDoesNotCollideWithGenericGetter()
    {
        var csharp = """
            IValue value = new Value<string>("typed");
            Console.WriteLine(value.Current);
            Console.WriteLine(((Value<string>)value).Current);

            interface IValue
            {
                object Current { get; }
            }

            sealed class Value<T>(T current) : IValue
            {
                public T Current { get; } = current;
                object IValue.Current => Current!;
            }
            """;

        var output = CompileAndRun(csharp);

        Assert.Equal("typed\ntyped", output);
    }

    [Test]
    public void CompileBitwiseOperations()
    {
        var csharp = "int x = 5 & 3; Console.WriteLine(x);";
        var output = CompileAndRun(csharp);
        Assert.Equal("1", output);
    }

    [Test]
    public void CompileNotOperation()
    {
        var csharp = "int x = ~5; Console.WriteLine(x);";
        var output = CompileAndRun(csharp);
        Assert.Equal("-6", output);
    }

    [Test]
    public void CompileUnsignedComparison()
    {
        var csharp = @"
uint a = 4294967295; // max uint
uint b = 1;
bool result = a > b;
Console.WriteLine(result);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("True", output);
    }

    [Test]
    public void CompileStringLength()
    {
        var csharp = @"
string s = ""Hello"";
int len = s.Length;
Console.WriteLine(len);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("5", output);
    }

    [Test]
    public void StringEmptyUsesTheClrEmptyStringValue()
    {
        var output = CompileAndRun("Console.WriteLine(string.Empty.Length);");

        Assert.Equal("0", output);
    }

    [Test]
    public void BoxedStructEnumeratorDispatchesInterfaceMethods()
    {
        var csharp = """
            IEnumerable<int> values = new List<int> { 7 };
            using var enumerator = values.GetEnumerator();
            Console.WriteLine(enumerator.MoveNext());
            Console.WriteLine(enumerator.Current);
            """;

        var output = CompileAndRun(csharp);

        Assert.Equal("True\n7", output);
    }

    [Test]
    public void ExactInterfaceRuntimeOverrideHandlesInternalImplementation()
    {
        var csharp = """
            IEqualityComparer<string> comparer = StringComparer.OrdinalIgnoreCase;
            Console.WriteLine(comparer.Equals("VALUE", "value"));
            """;

        var output = CompileAndRun(csharp);

        Assert.Equal("True", output);
    }

    [Test]
    public void GenericUnboxAnyPreservesReferenceTypeValues()
    {
        var csharp = """
            object boxed = Box("value");
            Console.WriteLine(Unbox<string>(boxed));

            static object Box<T>(T value) => value!;
            static T Unbox<T>(object value) => (T)value;
            """;

        var output = CompileAndRun(csharp);

        Assert.Equal("value", output);
    }

    [Test]
    public void CompileObjectConstructor()
    {
        var csharp = @"
object obj = new object();
Console.WriteLine(obj != null);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("True", output);
    }

    [Test]
    public void CompileCharIsWhiteSpaceFromRuntimeOverrideAssembly()
    {
        var csharp = "Console.WriteLine(char.IsWhiteSpace(' '));";
        var output = CompileAndRun(csharp);
        Assert.Equal("True", output);
    }

    [Test]
    public void Int32AndDateTimeToStringUseManagedInvariantFormatting()
    {
        var csharp = """
            var date = new DateTime(
                2024, 2, 29, 13, 5, 7, 0,
                DateTimeKind.Unspecified);

            Console.WriteLine(int.MinValue.ToString());
            Console.WriteLine(0.ToString());
            Console.WriteLine(int.MaxValue.ToString());
            Console.WriteLine((-42).ToString("D5"));
            Console.WriteLine(255.ToString("x4"));
            Console.WriteLine((-1).ToString("X"));
            Console.WriteLine(5.ToString("B8"));
            Console.WriteLine(date.ToString());
            Console.WriteLine(date.ToString("yyyy-MM-dd"));
            var guid = Guid.NewGuid();
            Console.WriteLine(guid.ToString("D"));
            Console.WriteLine(guid.ToString("N"));
            Console.WriteLine(guid.ToString("B"));
            Console.WriteLine(guid.ToString("P"));
            Console.WriteLine(guid.ToString("X"));
            try
            {
                Console.WriteLine(1.ToString("Q"));
            }
            catch (FormatException)
            {
                Console.WriteLine("invalid-int-format");
            }
            """;

        var output = CompileAndRun(csharp);

        var lines = output.Split('\n');
        Assert.Equal(
            new[]
            {
                "-2147483648", "0", "2147483647", "-00042", "00ff", "FFFFFFFF",
                "00000101", "02/29/2024 13:05:07", "2024-02-29"
            },
            lines[..9]);
        var guid = AssertVersion4Guid(lines[9], "D");
        Assert.Equal(guid.ToString("N"), lines[10]);
        Assert.Equal(guid.ToString("B"), lines[11]);
        Assert.Equal(guid.ToString("P"), lines[12]);
        Assert.Equal(guid.ToString("X"), lines[13]);
        Assert.Equal("invalid-int-format", lines[14]);
    }

    [Test]
    public void InterpolatedStringsUseManagedInt32AndDateTimeFormatting()
    {
        var csharp = """
            var count = -42;
            var date = new DateTime(
                2024, 2, 29, 13, 5, 7, 0,
                DateTimeKind.Unspecified);
            var guid = Guid.NewGuid();

            Console.WriteLine($"count={count}; when={date}; again={count}");
            Console.WriteLine($"decimal={count:D5}; hex={255:X4}; binary={5:B8}");
            Console.WriteLine($"date={date:yyyy-MM-dd}; guid={guid:N}");
            try
            {
                Console.WriteLine($"{date:Q}");
            }
            catch (FormatException)
            {
                Console.WriteLine("invalid-date-format");
            }
            """;

        var output = CompileAndRun(csharp);

        var lines = output.Split('\n');
        Assert.Equal("count=-42; when=02/29/2024 13:05:07; again=-42", lines[0]);
        Assert.Equal("decimal=-00042; hex=00FF; binary=00000101", lines[1]);
        const string guidPrefix = "date=2024-02-29; guid=";
        Assert.StartsWith(guidPrefix, lines[2]);
        AssertVersion4Guid(lines[2][guidPrefix.Length..], "N");
        Assert.Equal("invalid-date-format", lines[3]);
    }

    private static Guid AssertVersion4Guid(string value, string format)
    {
        var guid = Guid.ParseExact(value, format);
        Assert.NotEqual(Guid.Empty, guid);
        var canonical = guid.ToString("D");
        Assert.Equal('4', canonical[14]);
        Assert.True("89ab".Contains(canonical[19]));
        return guid;
    }

    private string CompileAndRun(string csharpCode)
    {
        return IntegrationTestBase.CompileAndRunStatic(csharpCode);
    }

}
