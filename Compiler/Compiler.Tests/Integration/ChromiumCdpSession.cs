using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace FrontEndCSharp.Compiler.Integration;

internal sealed class ChromiumCdpSession : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task<string> outputTask;
    private readonly Task<string> errorTask;
    private readonly CdpClient client = new();

    private ChromiumCdpSession(Process process)
    {
        this.process = process;
        outputTask = process.StandardOutput.ReadToEndAsync();
        errorTask = process.StandardError.ReadToEndAsync();
    }

    public IReadOnlyCollection<string> Exceptions => client.Exceptions;

    public static async Task<ChromiumCdpSession> LaunchAsync(
        string browserPath,
        string profilePath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = browserPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (var argument in new[]
        {
            "--headless=new",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-background-timer-throttling",
            "--disable-renderer-backgrounding",
            "--remote-debugging-address=127.0.0.1",
            "--remote-debugging-port=0",
            "--user-data-dir=" + profilePath,
            "about:blank",
        })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start();
        var session = new ChromiumCdpSession(process);
        try
        {
            var endpoint = await session.WaitForEndpoint(profilePath);
            await session.client.ConnectAsync(endpoint);
            await session.client.SendAsync("Page.enable");
            await session.client.SendAsync("Runtime.enable");
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    public Task<JsonElement> SendAsync(string method, object? parameters = null) =>
        client.SendAsync(method, parameters);

    public Task<JsonElement> EvaluateAsync(string expression) =>
        client.EvaluateAsync(expression);

    public async Task WaitForAsync(string expression, string label)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var result = await EvaluateAsync(expression);
            if (result.ValueKind == JsonValueKind.True)
            {
                return;
            }
            await Task.Delay(25);
        }
        var evidence = await EvaluateAsync("globalThis.__fecsMotionEvidence");
        var evidenceText = evidence.ValueKind == JsonValueKind.Undefined
            ? "<undefined>"
            : evidence.GetRawText();
        throw new TimeoutException(
            $"{label} was not observed. Evidence: {evidenceText}\n"
            + "Browser exceptions:\n" + string.Join("\n", Exceptions));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    await client.SendAsync("Browser.close");
                }
                catch (Exception exception) when (exception is WebSocketException
                    or InvalidOperationException
                    or TimeoutException)
                {
                }
            }
            await client.DisposeAsync();
            if (!process.HasExited && !process.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
            Task.WaitAll(outputTask, errorTask);
        }
        finally
        {
            process.Dispose();
        }
    }

    private async Task<Uri> WaitForEndpoint(string profilePath)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var activePortPath = Path.Combine(profilePath, "DevToolsActivePort");
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                Task.WaitAll(outputTask, errorTask);
                throw new InvalidOperationException(
                    "The owned browser exited during startup.\n" + errorTask.Result);
            }
            try
            {
                var lines = await File.ReadAllLinesAsync(activePortPath);
                var response = await client.GetStringAsync(
                    $"http://127.0.0.1:{lines[0]}/json/list");
                using var document = JsonDocument.Parse(response);
                foreach (var target in document.RootElement.EnumerateArray())
                {
                    if (target.GetProperty("type").GetString() == "page")
                    {
                        return new Uri(target.GetProperty("webSocketDebuggerUrl").GetString()!);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException
                or HttpRequestException
                or TaskCanceledException)
            {
            }
            await Task.Delay(50);
        }
        throw new TimeoutException(
            "The owned browser did not expose a page endpoint within fifteen seconds.");
    }
}
