using System.Diagnostics;
using System.Net.WebSockets;

namespace FrontEndCSharp.Debugger.Integration;

internal sealed class OwnedCdpBrowser : IAsyncDisposable
{
    private readonly Process process;
    private readonly string profilePath;
    private readonly Task<string> outputTask;
    private readonly Task<string> errorTask;

    private OwnedCdpBrowser(Process process, string profilePath)
    {
        this.process = process;
        this.profilePath = profilePath;
        outputTask = process.StandardOutput.ReadToEndAsync();
        errorTask = process.StandardError.ReadToEndAsync();
        Client = new BrowserCdpClient();
    }

    public BrowserCdpClient Client { get; }
    public Uri Endpoint { get; private set; } = null!;
    public bool HasExited => process.HasExited;

    public static async Task<OwnedCdpBrowser> LaunchAsync(string browserPath)
    {
        var profilePath = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-Owned-Browser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profilePath);
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
            "--no-default-browser-check",
            "--no-first-run",
            "--remote-debugging-address=127.0.0.1",
            "--remote-debugging-port=0",
            "--user-data-dir=" + profilePath,
            "about:blank",
        })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start();
        var browser = new OwnedCdpBrowser(process, profilePath);
        try
        {
            browser.Endpoint = await browser.ReadEndpoint();
            await browser.Client.ConnectAsync(browser.Endpoint);
            return browser;
        }
        catch
        {
            await browser.DisposeAsync();
            throw;
        }
    }

    public async Task KillAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            try
            {
                await Client.SendAsync("Browser.close");
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or TimeoutException
                or WebSocketException)
            {
            }
        }
        await Client.DisposeAsync();
        if (!process.HasExited && !process.WaitForExit(TimeSpan.FromSeconds(5)))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        Task.WaitAll(outputTask, errorTask);
        process.Dispose();
        DeleteProfile();
    }

    private async Task<Uri> ReadEndpoint()
    {
        var endpointPath = Path.Combine(profilePath, "DevToolsActivePort");
        for (var attempt = 0; attempt < 400; attempt++)
        {
            if (process.HasExited)
            {
                Task.WaitAll(outputTask, errorTask);
                throw new InvalidOperationException(
                    "Chromium exited during startup.\n" + errorTask.Result);
            }
            try
            {
                var lines = await File.ReadAllLinesAsync(endpointPath);
                if (lines.Length >= 2
                    && int.TryParse(lines[0], out var port)
                    && !string.IsNullOrWhiteSpace(lines[1]))
                {
                    return new Uri($"ws://127.0.0.1:{port}{lines[1]}");
                }
            }
            catch (IOException)
            {
            }
            await Task.Delay(25);
        }
        throw new TimeoutException("Chromium did not publish its CDP endpoint.");
    }

    private void DeleteProfile()
    {
        for (var attempt = 0; attempt < 10 && Directory.Exists(profilePath); attempt++)
        {
            try
            {
                Directory.Delete(profilePath, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
        if (Directory.Exists(profilePath))
        {
            throw new IOException($"Could not remove owned browser profile '{profilePath}'.");
        }
    }
}
