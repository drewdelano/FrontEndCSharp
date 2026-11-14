namespace PortablePdbFixture;

public static class PortableFixture
{
    public static void Main()
    {
        Console.WriteLine(AddOne(1));
        BusyLoop(100_000);
        CatchManagedException();
        ThrowManagedException();
        _ = NegativeZeroSingle();
        _ = NegativeZeroDouble();
        _ = Utf16BoundaryString();
        _ = FrontCodedFirst();
        _ = FrontCodedSecond();
        _ = DuplicateStringFirst();
        _ = DuplicateStringSecond();
        _ = LongStringBoundary();
    }

    public static int AddOne(int value)
    {
        var result = value + 1;
        return result;
    }

    public static int ReadCapturedLocal(int value)
    {
        var captured = value + 1;
        Func<int> read = () => captured;
        return read();
    }

    public static float NegativeZeroSingle() => -0.0f;

    public static double NegativeZeroDouble() => -0.0d;

    public static string Utf16BoundaryString() =>
        "\0\u007f\u0080\u07ff\u0800\ufffd\ud800x\udc00😀";

    public static string FrontCodedFirst() => "\u0800-front-code";

    public static string FrontCodedSecond() => "\u0801-front-code";

    public static string DuplicateStringFirst() => "\ud800-duplicate";

    public static string DuplicateStringSecond() => "\ud800-duplicate";

    public static string LongStringBoundary() =>
        "\uffffzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz"
        + "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz"
        + "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz"
        + "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz";

    private static int BusyLoop(int count)
    {
        var total = 0;
        for (var index = 0; index < count; index++)
        {
            total += index;
        }
        return total;
    }

    public static async Task<int> AddOneAsync(int value)
    {
        var beforeAwait = value + 10;
        await Task.Yield();
        return beforeAwait + 1;
    }

    private static void CatchManagedException()
    {
        try
        {
            ThrowManagedException();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void ThrowManagedException() =>
        throw new InvalidOperationException("debugger fixture");
}
