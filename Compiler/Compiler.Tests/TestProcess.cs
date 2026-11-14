using System.Diagnostics;
using System.Text;

namespace Compiler.Tests;

internal static class TestProcess
{
    public static (string Output, string Error) Run(
        string executable, string workingDirectory, TimeSpan timeout, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) { startInfo.ArgumentList.Add(argument); }
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {executable}.");
        var command = $"{executable} {string.Join(" ", arguments)}";
        var output = new PipeCapture(process.StandardOutput);
        var error = new PipeCapture(process.StandardError);
        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            if (!process.WaitForExit(5_000))
            {
                throw new TimeoutException($"{command} exceeded {timeout} and did not exit after termination.");
            }
            WaitForCaptures(output, error);
            throw new TimeoutException($"{command} exceeded {timeout}.\n{output.Captured}\n{error.Captured}");
        }
        if (!WaitForCaptures(output, error))
        {
            throw new TimeoutException($"{command} exited but its redirected streams did not close.");
        }
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{command} exited with code {process.ExitCode}.\n{output.Result}\n{error.Result}");
        }
        return (output.Result, error.Result);
    }

    private static bool WaitForCaptures(PipeCapture output, PipeCapture error)
    {
        var clock = Stopwatch.StartNew();
        return output.Wait(5_000) && error.Wait((int)Math.Max(0, 5_000 - clock.ElapsedMilliseconds));
    }

    private sealed class PipeCapture
    {
        private readonly Thread reader;
        private string? result;
        private Exception? failure;

        public PipeCapture(StreamReader stream)
        {
            // Windows Process pipes use synchronous handles. ReadToEndAsync can
            // need pool workers while synchronous tests occupy that pool.
            // Dedicated readers keep pipe draining independent of test scheduling.
            reader = new Thread(() =>
            {
                try { result = stream.ReadToEnd(); }
                catch (Exception exception) { failure = exception; }
            }) { IsBackground = true, Name = "Compiler test process pipe" };
            reader.Start();
        }

        public bool Wait(int milliseconds) => reader.Join(milliseconds);
        public string Captured => reader.Join(0) ? Result : "<stream still open>";
        public string Result => failure == null ? result!
            : throw new InvalidOperationException("Could not capture the child process stream.", failure);
    }
}
