namespace FrontEndCSharp.VisualStudio;

internal sealed class DebuggerProcessResult
{
    internal DebuggerProcessResult(int exitCode, string standardOutput, string standardError)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    internal int ExitCode { get; }
    internal string StandardOutput { get; }
    internal string StandardError { get; }
}
