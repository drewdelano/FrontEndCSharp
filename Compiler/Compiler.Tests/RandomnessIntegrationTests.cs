namespace Compiler.Tests;

public sealed class RandomnessIntegrationTests : IntegrationTestBase
{
    [Test]
    public void GuidAndCryptographicRandomApisUseSecureNonRepeatingValues()
    {
        var output = CompileAndRun(
            """
            using System.Security.Cryptography;

            var firstGuid = Guid.NewGuid();
            var secondGuid = Guid.NewGuid();
            Console.WriteLine(firstGuid.ToString("D"));
            Console.WriteLine(secondGuid.ToString("D"));

            var firstBytes = RandomNumberGenerator.GetBytes(257);
            var secondBytes = RandomNumberGenerator.GetBytes(257);
            var anyNonZero = false;
            var arraysDiffer = false;
            for (var index = 0; index < firstBytes.Length; index++)
            {
                anyNonZero |= firstBytes[index] != 0;
                arraysDiffer |= firstBytes[index] != secondBytes[index];
            }
            Console.WriteLine(firstBytes.Length);
            Console.WriteLine(anyNonZero);
            Console.WriteLine(arraysDiffer);

            var singleBounded = true;
            var doubleBounded = true;
            for (var index = 0; index < 512; index++)
            {
                var single = RandomNumberGenerator.GetInt32(7);
                var range = RandomNumberGenerator.GetInt32(-17, 23);
                singleBounded &= single >= 0 && single < 7;
                doubleBounded &= range >= -17 && range < 23;
            }
            Console.WriteLine(singleBounded);
            Console.WriteLine(doubleBounded);
            Console.WriteLine(RandomNumberGenerator.GetInt32(1));
            Console.WriteLine(RandomNumberGenerator.GetInt32(-1, 0));
            Console.WriteLine(RandomNumberGenerator.GetBytes(0).Length);

            try { _ = RandomNumberGenerator.GetBytes(-1); Console.WriteLine("wrong"); }
            catch (ArgumentOutOfRangeException) { Console.WriteLine("ArgumentOutOfRangeException"); }
            try { _ = RandomNumberGenerator.GetInt32(0); Console.WriteLine("wrong"); }
            catch (ArgumentOutOfRangeException) { Console.WriteLine("ArgumentOutOfRangeException"); }
            try { _ = RandomNumberGenerator.GetInt32(5, 5); Console.WriteLine("wrong"); }
            catch (ArgumentException) { Console.WriteLine("ArgumentException"); }
            """);

        var lines = output.Split(
            '\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var firstGuid = AssertVersion4Guid(lines[0]);
        var secondGuid = AssertVersion4Guid(lines[1]);
        Assert.NotEqual(firstGuid, secondGuid);
        Assert.Equal("257", lines[2]);
        Assert.Equal("True", lines[3]);
        Assert.Equal("True", lines[4]);
        Assert.Equal("True", lines[5]);
        Assert.Equal("True", lines[6]);
        Assert.Equal("0", lines[7]);
        Assert.Equal("-1", lines[8]);
        Assert.Equal("0", lines[9]);
        Assert.Equal("ArgumentOutOfRangeException", lines[10]);
        Assert.Equal("ArgumentOutOfRangeException", lines[11]);
        Assert.Equal("ArgumentException", lines[12]);
    }

    private static Guid AssertVersion4Guid(string value)
    {
        var guid = Guid.ParseExact(value, "D");
        Assert.NotEqual(Guid.Empty, guid);
        Assert.Equal('4', value[14]);
        Assert.True("89ab".Contains(value[19]));
        return guid;
    }
}
