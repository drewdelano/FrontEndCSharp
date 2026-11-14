using System.Diagnostics;

namespace FrontEndCSharp.Compiler.Integration;

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
        Task.WaitAll(outputTask, errorTask);
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
