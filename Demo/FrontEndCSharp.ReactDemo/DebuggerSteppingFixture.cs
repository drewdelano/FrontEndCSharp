namespace FrontEndCSharp.ReactDemo;

internal sealed class DebuggerSteppingFixture
{
    private readonly int _offset;

    public DebuggerSteppingFixture(int offset)
    {
        _offset = offset;
    }

    public int Run(int value)
    {
        var incremented = value + _offset;
        var doubled = Double(incremented);
        return doubled + _offset;
    }

    private static int Double(int value)
    {
        var result = value * 2;
        return result;
    }
}
