namespace AsyncPdbFixture;

public static class AsyncFixture
{
    public static async Task Main()
    {
        Console.WriteLine(await Caller(2));
        throw new InvalidOperationException("async debugger fixture");
    }

    private static async Task<int> Caller(int value)
    {
        return await AddOneAsync(value);
    }

    private static async Task<int> AddOneAsync(int value)
    {
        var beforeAwait = value + 10;
        await Task.Yield();
        return beforeAwait + 1;
    }
}
