namespace Compiler.Tests;

public sealed class LazyIntegrationTests : IntegrationTestBase
{
    [Test]
    public void FactoryLazyInitializesOnceAndCachesFailuresAndRecursion()
    {
        var output = CompileAndRun(
            """
            var valueFactoryCalls = 0;
            var value = new Lazy<string>(() =>
            {
                valueFactoryCalls++;
                return "created";
            });

            Console.WriteLine(value.IsValueCreated);
            Console.WriteLine(value.Value);
            Console.WriteLine(value.Value);
            Console.WriteLine(valueFactoryCalls);
            Console.WriteLine(value.IsValueCreated);

            var failureFactoryCalls = 0;
            var failure = new Lazy<int>(() =>
            {
                failureFactoryCalls++;
                throw new InvalidOperationException("factory boom");
            });

            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    _ = failure.Value;
                }
                catch (InvalidOperationException exception)
                {
                    Console.WriteLine(exception.Message);
                }
            }
            Console.WriteLine(failureFactoryCalls);
            Console.WriteLine(failure.IsValueCreated);

            var recursiveFactoryCalls = 0;
            Lazy<int>? recursive = null;
            recursive = new Lazy<int>(() =>
            {
                recursiveFactoryCalls++;
                return recursive.Value;
            });

            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    _ = recursive.Value;
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("recursive");
                }
            }
            Console.WriteLine(recursiveFactoryCalls);
            Console.WriteLine(recursive.IsValueCreated);

            try
            {
                _ = new Lazy<int>(null!);
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine("null factory");
            }
            """);

        Assert.Equal(
            "False\ncreated\ncreated\n1\nTrue\n"
            + "factory boom\nfactory boom\n1\nFalse\n"
            + "recursive\nrecursive\n1\nFalse\nnull factory",
            output);
    }
}
