namespace FrontEndCSharp.Compiler.Integration;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 2 && args[0] == "javascript-interop-browser-smoke")
            {
                JavaScriptInteropBrowserSmoke.Run(
                    args[1],
                    args.Skip(2).Contains("--no-build", StringComparer.Ordinal));
                return 0;
            }
            if (args.Length >= 2 && args[0] == "fecsx-react-demo-browser-smoke")
            {
                FecsxReactDemoBrowserSmoke.Run(
                    args[1],
                    args.Skip(2).Contains("--no-build", StringComparer.Ordinal));
                return 0;
            }
            if (args.Length >= 2 && args[0] == "fecscn-browser-smoke")
            {
                FecscnBrowserSmoke.Run(
                    args[1],
                    args.Skip(2).Contains("--no-build", StringComparer.Ordinal));
                return 0;
            }
            if (args.Length == 1 && args[0] == "interpreter-performance")
            {
                InterpreterPerformance.Run();
                return 0;
            }
            if (args.Length >= 1 && args[0] == "compiler-performance")
            {
                CompilerPerformance.Run(args.Skip(1).ToArray());
                return 0;
            }

            Console.Error.WriteLine(
                "Usage: FrontEndCSharp.Compiler.Integration javascript-interop-browser-smoke <chromium-browser-path> [--no-build]\n"
                + "   or: FrontEndCSharp.Compiler.Integration fecsx-react-demo-browser-smoke <chromium-browser-path> [--no-build]\n"
                + "   or: FrontEndCSharp.Compiler.Integration fecscn-browser-smoke <chromium-browser-path> [--no-build]\n"
                + "   or: FrontEndCSharp.Compiler.Integration interpreter-performance\n"
                + "   or: FrontEndCSharp.Compiler.Integration compiler-performance [--label <name>] [--fixture <generated|react-demo>] [--pages <count>] [--samples <count>] [--bundle <true|false>] [--workspace <path>] [--compiler <path>]");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Error: " + exception.Message);
            return 1;
        }
    }
}
