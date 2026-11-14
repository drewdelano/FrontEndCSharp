namespace Compiler.Tests;

public sealed class ObjectToStringIntegrationTests : IntegrationTestBase
{
    [Test]
    public void DirectCallsAndInterpolationMatchDesktopDotNet()
    {
        const string source = """
            var plain = new Plain();
            object boxedPlain = plain;
            var inherited = new InheritsOverride();
            object boxedNumber = 42;

            Console.WriteLine(new object().ToString());
            Console.WriteLine(plain.ToString());
            Console.WriteLine(boxedPlain.ToString());
            Console.WriteLine(inherited.ToString());
            Console.WriteLine(boxedNumber.ToString());
            Console.WriteLine($"{plain}");
            Console.WriteLine($"{boxedPlain}");
            Console.WriteLine($"{inherited}");
            Console.WriteLine($"{plain:ignored}");
            Console.WriteLine($"{new CustomFormattable():X}");
            Console.WriteLine($"{new InheritsFormattable():Y}");
            Console.WriteLine($"{new FormattedOverloadWithoutInterface():ignored}");
            Console.WriteLine($"{(object?)null}");

            sealed class Plain { }

            class OverridesToString
            {
                public override string ToString() => "overridden";
            }

            sealed class InheritsOverride : OverridesToString { }

            class CustomFormattable : IFormattable
            {
                public override string ToString() => "plain";

                public string ToString(string? format, IFormatProvider? provider) =>
                    "formatted:" + format;
            }

            sealed class InheritsFormattable : CustomFormattable { }

            sealed class FormattedOverloadWithoutInterface
            {
                public override string ToString() => "plain-overload";

                public string ToString(string? format, IFormatProvider? provider) =>
                    "wrong:" + format;
            }
            """;

        Assert.Equal(
            """
            System.Object
            Plain
            Plain
            overridden
            42
            Plain
            Plain
            overridden
            Plain
            formatted:X
            formatted:Y
            plain-overload

            """.TrimEnd(),
            CompileAndRun(source));
    }
}
