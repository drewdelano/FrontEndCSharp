namespace Compiler.Tests;

public sealed class RandomIntegrationTests : IntegrationTestBase
{
    [Test]
    public void RandomSupportsDeterministicSeedsBoundsBytesAndSharedInstance()
    {
        var output = CompileAndRun(
            """
            var first = new Random(123456789);
            var second = new Random(123456789);
            Console.WriteLine(first.Next() == second.Next());
            Console.WriteLine(first.Next(1000) == second.Next(1000));
            Console.WriteLine(first.Next(-100, 100) == second.Next(-100, 100));
            Console.WriteLine(first.NextDouble() == second.NextDouble());

            var firstBytes = new byte[17];
            var secondBytes = new byte[17];
            first.NextBytes(firstBytes);
            second.NextBytes(secondBytes);
            var equalBytes = true;
            var anyNonzeroByte = false;
            for (var index = 0; index < firstBytes.Length; index++)
            {
                equalBytes &= firstBytes[index] == secondBytes[index];
                anyNonzeroByte |= firstBytes[index] != 0;
            }
            Console.WriteLine(equalBytes);
            Console.WriteLine(anyNonzeroByte);

            var bounds = new Random(42);
            var inRange = true;
            for (var index = 0; index < 100; index++)
            {
                var value = bounds.Next(-3, 4);
                inRange &= value >= -3 && value < 4;
            }
            Console.WriteLine(inRange);
            Console.WriteLine(bounds.Next(0));
            Console.WriteLine(bounds.Next(1));
            Console.WriteLine(bounds.Next(5, 5));
            var fraction = bounds.NextDouble();
            Console.WriteLine(fraction >= 0 && fraction < 1);

            try
            {
                _ = bounds.Next(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("negative max");
            }

            try
            {
                _ = bounds.Next(2, 1);
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("reversed bounds");
            }

            try
            {
                bounds.NextBytes(null!);
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine("null bytes");
            }

            var unseeded = new Random();
            Console.WriteLine(unseeded.Next() >= 0);
            Console.WriteLine(Random.Shared.Next(10) is >= 0 and < 10);
            """);

        Assert.Equal(
            "True\nTrue\nTrue\nTrue\nTrue\nTrue\nTrue\n0\n0\n5\nTrue\n"
            + "negative max\nreversed bounds\nnull bytes\nTrue\nTrue",
            output);
    }
}
