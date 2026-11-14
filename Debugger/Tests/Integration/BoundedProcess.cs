using System.Diagnostics;

namespace FrontEndCSharp.Debugger.Integration;

internal sealed class ProcessOutput
{
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
}

internal static class BoundedProcess
{
    public static ProcessOutput Run(
        string fileName,
        string workingDirectory,
        TimeSpan timeout,
        params string[] arguments)
    {
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(
                $"{fileName} exceeded its {timeout.TotalSeconds:0}-second limit.");
        }
        var streamTimeout = timeout - stopwatch.Elapsed;
        if (streamTimeout <= TimeSpan.Zero
            || !Task.WaitAll([outputTask, errorTask], streamTimeout))
        {
            throw new TimeoutException(
                $"{fileName} exited but its redirected streams did not close within "
                + $"the {timeout.TotalSeconds:0}-second process limit.");
        }
        var result = new ProcessOutput
        {
            StandardOutput = outputTask.Result,
            StandardError = errorTask.Result,
        };
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{fileName} {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.\n"
                + result.StandardOutput + result.StandardError);
        }
        return result;
    }
}
