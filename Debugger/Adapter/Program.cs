namespace FrontEndCSharp.DebugAdapter;

internal static class Program
{
    private static void Main()
    {
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        var adapter = new FrontEndDebugAdapter(input, output);
        adapter.Protocol.Run();
        adapter.Protocol.WaitForReader();
    }
}
