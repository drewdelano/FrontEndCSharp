using System.Diagnostics;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class CdpBrowserHost : IAsyncDisposable
{
    private readonly CdpBrowserLaunchOptions options;
    private readonly Action<string> failed;
    private DebuggeeServer? server;
    private Process? browser;
    private string? profileDirectory;

    internal CdpBrowserHost(
        CdpBrowserLaunchOptions options,
        Action<string> failed)
    {
        this.options = options;
        this.failed = failed;
    }

    internal async Task<CdpBrowserStartResult> StartAsync(CancellationToken cancellationToken)
    {
        if (options.Endpoint == null && !File.Exists(options.BrowserPath))
        {
            throw new FileNotFoundException("Chromium browser was not found.", options.BrowserPath);
        }
        if (!Directory.Exists(options.GeneratedOutputPath))
        {
            throw new DirectoryNotFoundException(options.GeneratedOutputPath);
        }

        if (options.ApplicationUrl == null)
        {
            server = new DebuggeeServer(options.GeneratedOutputPath);
            server.Start();
        }
        var endpoint = options.Endpoint ?? await StartBrowserAsync(cancellationToken);
        var navigationUrl = options.ApplicationUrl
            ?? new Uri($"http://127.0.0.1:{server!.Port}/index.html");
        return new CdpBrowserStartResult(endpoint, navigationUrl);
    }

    public async ValueTask DisposeAsync()
    {
        if (browser != null && !browser.HasExited)
        {
            try
            {
                browser.Kill(entireProcessTree: true);
                await browser.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
            }
        }
        if (browser != null)
        {
            browser.Exited -= OnBrowserExited;
            browser.Dispose();
        }
        if (server != null)
        {
            await server.DisposeAsync();
        }
        if (profileDirectory != null
            && Path.GetFileName(profileDirectory).StartsWith(
                "FrontEndCSharp-VS-",
                StringComparison.Ordinal))
        {
            try
            {
                Directory.Delete(profileDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private async Task<Uri> StartBrowserAsync(CancellationToken cancellationToken)
    {
        profileDirectory = Path.Combine(
            Path.GetTempPath(),
            $"FrontEndCSharp-VS-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profileDirectory);
        var browserArguments = new List<string>();
        if (options.Headless)
        {
            browserArguments.Add("--headless=new");
        }
        browserArguments.AddRange([
            "--no-default-browser-check",
            "--no-first-run",
            "--remote-debugging-address=127.0.0.1",
            "--remote-debugging-port=0",
            $"--user-data-dir=\"{profileDirectory}\"",
            "about:blank"
        ]);
        browser = Process.Start(new ProcessStartInfo
        {
            FileName = options.BrowserPath!,
            Arguments = string.Join(' ', browserArguments),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        }) ?? throw new InvalidOperationException("Unable to start Chromium.");
        browser.EnableRaisingEvents = true;
        browser.Exited += OnBrowserExited;
        return await ReadEndpointAsync(profileDirectory, browser, cancellationToken);
    }

    private void OnBrowserExited(object? sender, EventArgs arguments)
    {
        failed($"Chromium exited unexpectedly with code {browser?.ExitCode}.");
    }

    private static async Task<Uri> ReadEndpointAsync(
        string profile,
        Process process,
        CancellationToken cancellationToken)
    {
        var portFile = Path.Combine(profile, "DevToolsActivePort");
        for (var attempt = 0; attempt < 200; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Chromium exited with code {process.ExitCode} before opening its debug port.");
            }
            try
            {
                if (File.Exists(portFile))
                {
                    var lines = await File.ReadAllLinesAsync(portFile, cancellationToken);
                    if (lines.Length >= 2)
                    {
                        return new Uri($"ws://127.0.0.1:{lines[0]}{lines[1]}");
                    }
                }
            }
            catch (IOException)
            {
            }
            await Task.Delay(50, cancellationToken);
        }
        throw new TimeoutException("Chromium did not publish a DevTools endpoint.");
    }
}

internal sealed class CdpBrowserLaunchOptions
{
    internal string? BrowserPath { get; init; }
    internal Uri? Endpoint { get; init; }
    internal bool Headless { get; init; }
    internal Uri? ApplicationUrl { get; init; }
    internal string GeneratedOutputPath { get; init; } = string.Empty;
}

internal sealed class CdpBrowserStartResult
{
    internal CdpBrowserStartResult(Uri endpoint, Uri navigationUrl)
    {
        Endpoint = endpoint;
        NavigationUrl = navigationUrl;
    }

    internal Uri Endpoint { get; }
    internal Uri NavigationUrl { get; }
}
