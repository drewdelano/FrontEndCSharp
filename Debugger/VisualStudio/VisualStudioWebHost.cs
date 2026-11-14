using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEndCSharp.VisualStudio;

internal sealed class VisualStudioWebHost
{
    private readonly FrontEndCSharpPackage package;
    private readonly VisualStudioDebuggerOutput output;
    private Process? process;

    internal VisualStudioWebHost(
        FrontEndCSharpPackage package,
        VisualStudioDebuggerOutput output)
    {
        this.package = package;
        this.output = output;
    }

    internal async Task<Uri> StartAsync(
        string webProjectPath,
        string configuration,
        string workingDirectory)
    {
        var listening = new TaskCompletionSource<Uri>();
        var capturedOutput = new StringBuilder();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = string.Join(" ", new[]
            {
                "run",
                "--project",
                webProjectPath,
                "--configuration",
                configuration,
                "--no-build",
                "--no-launch-profile",
                "--",
                "--urls",
                "http://127.0.0.1:0"
            }.Select(DebuggerProcessRunner.QuoteArgument)),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
        var candidate = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        DataReceivedEventHandler capture = (_, eventArgs) =>
        {
            if (string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                return;
            }
            lock (capturedOutput)
            {
                capturedOutput.AppendLine(eventArgs.Data);
            }
            const string marker = "Now listening on: ";
            var markerIndex = eventArgs.Data.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0
                && Uri.TryCreate(
                    eventArgs.Data.Substring(markerIndex + marker.Length).Trim(),
                    UriKind.Absolute,
                    out var uri))
            {
                BrowserCompatibilityTrace.Write("aspnet-host-listening", uri.ToString());
                listening.TrySetResult(uri);
            }
        };
        candidate.OutputDataReceived += capture;
        candidate.ErrorDataReceived += capture;
        candidate.Exited += (_, _) =>
        {
            string detail;
            lock (capturedOutput)
            {
                detail = capturedOutput.ToString().Trim();
            }
            listening.TrySetException(new InvalidOperationException(
                $"The web host exited before it started listening (exit code {candidate.ExitCode}). {detail}"));
        };

        try
        {
            if (!candidate.Start())
            {
                throw new InvalidOperationException("Unable to start the web project.");
            }
            process = candidate;
            candidate.BeginOutputReadLine();
            candidate.BeginErrorReadLine();
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            output.WriteLine(
                $"Starting {Path.GetFileNameWithoutExtension(webProjectPath)} on an isolated loopback port...");
            var timeout = Task.Delay(TimeSpan.FromSeconds(30));
            if (await Task.WhenAny(listening.Task, timeout) != listening.Task)
            {
                throw new TimeoutException(
                    "The web project did not report a listening URL within 30 seconds.");
            }
            var url = await listening.Task;
            output.WriteLine($"Web host is ready at {url}.");
            return url;
        }
        catch
        {
            Stop();
            throw;
        }
    }

    internal void Stop()
    {
        var runningProcess = process;
        process = null;
        if (runningProcess == null)
        {
            return;
        }
        try
        {
            if (!runningProcess.HasExited)
            {
                runningProcess.Kill();
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            runningProcess.Dispose();
        }
    }

    internal void InvalidateNormalGenerationStamp(string webProjectDirectory)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var intermediateDirectory = Path.Combine(webProjectDirectory, "obj");
        if (!Directory.Exists(intermediateDirectory))
        {
            return;
        }
        try
        {
            foreach (var stampPath in Directory.EnumerateFiles(
                intermediateDirectory,
                "browser-output.stamp",
                SearchOption.AllDirectories))
            {
                File.Delete(stampPath);
            }
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is DirectoryNotFoundException)
        {
            output.WriteLine(
                "Could not invalidate the ordinary FrontEndCSharp web-build output: "
                + exception.Message);
        }
    }
}
