using System.Diagnostics;

internal sealed class BrowserExecutionResult
{
    public BrowserExecutionResult(string document, string diagnostics)
    {
        Document = document;
        Diagnostics = diagnostics;
    }

    public string Document { get; }
    public string Diagnostics { get; }
}

internal static class ChromiumRunner
{
    public static Task<BrowserExecutionResult> RunAsync(
        string browserPath,
        string workingDirectory,
        string browserProfile,
        string pageUrl,
        CancellationToken cancellationToken)
    {
        return BoundedProcess.RunAsync(
            browserPath,
            [
                "--headless=new",
                "--disable-gpu",
                "--no-default-browser-check",
                "--no-first-run",
                "--dump-dom",
                "--virtual-time-budget=3000",
                $"--user-data-dir={browserProfile}",
                pageUrl,
            ],
            workingDirectory,
            "Chromium",
            cancellationToken);
    }
}

internal static class BoundedProcess
{
    public static async Task<BrowserExecutionResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string operation,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {operation}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }

        var result = new BrowserExecutionResult(
            await standardOutput,
            await standardError);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{operation} failed ({process.ExitCode}).{Environment.NewLine}"
                    + result.Document
                    + Environment.NewLine
                    + result.Diagnostics);
        }

        return result;
    }
}

internal static class HttpClientSmokeAssertions
{
    private static readonly string[] ExpectedDocumentFragments =
    [
        "data-fecs-status=\"ready\"",
        "data-fecs-http-body=\"FrontEndCSharp HTTP response — buffered\"",
        "data-fecs-http-bytes=\"41\"",
        "data-fecs-http-multi=\"first, second\"",
        "data-fecs-http-post=\"POST|request-header|text/plain; charset=utf-8||*/*|browser request — buffered\"",
    ];

    public static void Verify(BrowserExecutionResult result)
    {
        foreach (var expected in ExpectedDocumentFragments)
        {
            if (!result.Document.Contains(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"HttpClient fixture did not report expected DOM content '{expected}'."
                        + $"{Environment.NewLine}DOM:{Environment.NewLine}{result.Document}"
                        + $"{Environment.NewLine}Chromium diagnostics:{Environment.NewLine}{result.Diagnostics}");
            }
        }
    }
}
