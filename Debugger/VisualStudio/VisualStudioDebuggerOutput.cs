using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace FrontEndCSharp.VisualStudio;

internal sealed class VisualStudioDebuggerOutput
{
    private readonly IVsOutputWindowPane pane;

    internal VisualStudioDebuggerOutput(IVsOutputWindowPane pane)
    {
        this.pane = pane;
    }

    internal void Activate()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        pane.Activate();
    }

    internal void WriteLine(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        pane.OutputStringThreadSafe(message + Environment.NewLine);
    }

    internal void Write(DebuggerProcessResult result)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            WriteLine(result.StandardOutput.TrimEnd());
        }
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            WriteLine(result.StandardError.TrimEnd());
        }
    }
}
