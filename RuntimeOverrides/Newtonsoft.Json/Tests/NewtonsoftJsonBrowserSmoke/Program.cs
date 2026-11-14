using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project RuntimeOverrides/Newtonsoft.Json/Tests/NewtonsoftJsonBrowserSmoke/NewtonsoftJsonBrowserSmoke.csproj -- <chromium-browser-path>");
    return 2;
}

var browserPath = Path.GetFullPath(args[0]);
if (!File.Exists(browserPath))
{
    Console.Error.WriteLine($"Chromium executable not found: {browserPath}");
    return 2;
}

var stopwatch = Stopwatch.StartNew();
var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
var fixtureRoot = Path.Combine(
    repositoryRoot,
    "RuntimeOverrides",
    "Newtonsoft.Json",
    "Tests",
    "NewtonsoftJsonBrowserSmokeFixture");
var fixtureProject = Path.Combine(fixtureRoot, "NewtonsoftJsonBrowserSmokeFixture.csproj");
var fixtureAssembly = Path.Combine(
    fixtureRoot,
    "bin",
    "Debug",
    "net8.0",
    "NewtonsoftJsonBrowserSmokeFixture.dll");
var compilerPath = Path.Combine(
    repositoryRoot,
    "Compiler",
    "Core",
    "bin",
    "Debug",
    "net8.0",
    "Compiler.dll");
var generatedOutput = CreateTemporaryDirectory("FrontEndCSharp-NewtonsoftJson-Smoke-");
var browserProfile = CreateTemporaryDirectory("FrontEndCSharp-NewtonsoftJson-Browser-");

try
{
    await RunAsync(
        "dotnet",
        [compilerPath, "--no-bundle", fixtureProject, generatedOutput, fixtureAssembly],
        repositoryRoot,
        "FrontEndCSharp compilation",
        GetRemainingTime(stopwatch));

    await File.WriteAllTextAsync(
        Path.Combine(generatedOutput, "index.html"),
        "<!doctype html><html><head><meta charset=\"utf-8\"></head>"
            + "<body><script>addEventListener('error',event=>{const detail=event.error?.stack??event.message;"
            + "document.body.setAttribute('data-fecs-browser-error',detail);console.error(detail);});</script>"
            + "<script type=\"module\" src=\"/index.js\"></script></body></html>\n",
        Encoding.UTF8);

    await using var server = new StaticFileServer(generatedOutput);
    var result = await RunAsync(
        browserPath,
        [
            "--headless=new",
            "--disable-gpu",
            "--enable-logging=stderr",
            "--no-default-browser-check",
            "--no-first-run",
            "--dump-dom",
            "--virtual-time-budget=2000",
            $"--user-data-dir={browserProfile}",
            server.Url,
        ],
        repositoryRoot,
        "Chromium",
        GetRemainingTime(stopwatch));

    var dom = result.StandardOutput;
    RequireContains(dom, "data-fecs-status=\"ready\"", result.StandardError);

    RequireTargetDuration(stopwatch, "Newtonsoft.Json browser integration");
    var durationClass = stopwatch.Elapsed >= TimeSpan.FromSeconds(5) ? "slow" : "normal";
    Console.WriteLine(
        $"Newtonsoft.Json browser integration passed in Chromium in {stopwatch.Elapsed.TotalSeconds:F2} seconds "
            + $"[{durationClass}; 5-second slow bar; 10-second maximum].");
    return 0;
}
finally
{
    DeleteDirectory(generatedOutput);
    DeleteDirectory(browserProfile);
}

static string FindRepositoryRoot(string start)
{
    for (var directory = new DirectoryInfo(Path.GetFullPath(start));
         directory !=  null;
         directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln"))
            && File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            return directory.FullName;
        }
    }

    throw new InvalidOperationException(
        $"Could not locate the FrontEndCSharp repository above '{start}'.");
}

static TimeSpan GetRemainingTime(Stopwatch stopwatch)
{
    var remaining = TimeSpan.FromSeconds(10) - stopwatch.Elapsed;
    return remaining > TimeSpan.Zero
        ? remaining
        : throw new TimeoutException(
            "Newtonsoft.Json browser integration exceeded its absolute 10-second execution limit.");
}

static void RequireTargetDuration(Stopwatch stopwatch, string operation)
{
    stopwatch.Stop();
        if (stopwatch.Elapsed >= TimeSpan.FromSeconds(10))
    {
        throw new TimeoutException(
            $"{operation} completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds; "
                + "tests must complete in under 10 seconds.");
    }
}

static string CreateTemporaryDirectory(string prefix)
{
    var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void DeleteDirectory(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, recursive: true);
    }
}

static void RequireContains(string actual, string expected, string diagnostics = "")
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Newtonsoft.Json fixture did not report expected DOM content '{expected}'."
                + $"{Environment.NewLine}DOM:{Environment.NewLine}{actual}"
                + $"{Environment.NewLine}Chromium diagnostics:{Environment.NewLine}{diagnostics}");
    }
}

static async Task<ProcessResult> RunAsync(
    string executable,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    string operation,
    TimeSpan? timeout = null)
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
    var standardOutput = process.StandardOutput.ReadToEndAsync();
    var standardError = process.StandardError.ReadToEndAsync();
    using var cancellation = timeout.HasValue
        ? new CancellationTokenSource(timeout.Value)
        : new CancellationTokenSource();
    try
    {
        await process.WaitForExitAsync(cancellation.Token);
    }
    catch (OperationCanceledException) when (timeout.HasValue)
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException($"{operation} did not complete within {timeout.Value}.");
    }

    var result = new ProcessResult(
        process.ExitCode,
        await standardOutput,
        await standardError);
    if (result.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"{operation} failed ({result.ExitCode}).{Environment.NewLine}"
                + result.StandardOutput
                + Environment.NewLine
                + result.StandardError);
    }

    return result;
}

internal sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal sealed class StaticFileServer : IAsyncDisposable
{
    private readonly string root;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task acceptLoop;
    private readonly List<Task> requests = [];
    private readonly object requestsLock = new();

    public StaticFileServer(string root)
    {
        this.root = Path.GetFullPath(root);
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        Url = $"http://127.0.0.1:{endpoint.Port}/";
        acceptLoop = AcceptAsync();
    }

    public string Url { get; }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        listener.Stop();
        try
        {
            await acceptLoop;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException) when (cancellation.IsCancellationRequested)
        {
        }

        Task[] pending;
        lock (requestsLock)
        {
            pending = [.. requests];
        }
        try
        {
            await Task.WhenAll(pending);
        }
        catch (Exception exception) when (
            exception is IOException or OperationCanceledException)
        {
            // Headless Chromium can abort pending response writes as it exits
            // after the top-level module completes.
        }
        cancellation.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(cancellation.Token);
            var request = ServeAsync(client, cancellation.Token);
            lock (requestsLock)
            {
                requests.Add(request);
            }
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        await using (var stream = client.GetStream())
        using (var reader = new StreamReader(
            stream,
            Encoding.ASCII,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true))
        {
            var requestLine = await reader.ReadLineAsync(token);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(token)))
            {
            }

            var requestTarget = requestLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ElementAtOrDefault(1);
            if (requestTarget == null)
            {
                await WriteResponseAsync(stream, 400, "text/plain", [], token);
                return;
            }

            var requestPath = new Uri(new Uri(Url), requestTarget).AbsolutePath;
            var relativePath = requestPath == "/"
                ? "index.html"
                : Uri.UnescapeDataString(requestPath[1..]);
            var candidate = Path.GetFullPath(
                Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(candidate))
            {
                await WriteResponseAsync(stream, 404, "text/plain", [], token);
                return;
            }

            var content = await File.ReadAllBytesAsync(candidate, token);
            await WriteResponseAsync(stream, 200, GetContentType(candidate), content, token);
        }
    }

    private static async Task WriteResponseAsync(
        Stream stream,
        int status,
        string contentType,
        byte[] content,
        CancellationToken token)
    {
        var reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            _ => "Not Found",
        };
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\n"
                + $"Content-Type: {contentType}\r\n"
                + $"Content-Length: {content.Length}\r\n"
                + "Cache-Control: no-store\r\n"
                + "Connection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(content, token);
    }

    private static string GetContentType(string path) => Path.GetExtension(path) switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" or ".map" => "application/json; charset=utf-8",
        _ => "application/octet-stream",
    };
}
