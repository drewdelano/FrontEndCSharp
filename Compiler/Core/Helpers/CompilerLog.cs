using System;

namespace Compiler.Helpers;

internal enum CompilerLogLevel
{
    Normal,
    Verbose,
}

internal static class CompilerLog
{
    internal static CompilerLogLevel Level { get; set; } = CompilerLogLevel.Normal;

    internal static void WriteLine(CompilerLogLevel level, string message)
    {
        if (level <= Level)
        {
            Console.WriteLine(message);
        }
    }
}
