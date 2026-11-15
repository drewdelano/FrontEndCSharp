using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project Overrides/FrontEndCSharp.Overrides.System.Net.Http.Json/Tests/FrontEndCSharp.Overrides.HttpJsonBrowserSmoke/FrontEndCSharp.Overrides.HttpJsonBrowserSmoke.csproj -- <chromium-browser-path>");
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
    "Overrides",
    "FrontEndCSharp.Overrides.System.Net.Http.Json",
    "Tests",
    "FrontEndCSharp.Overrides.HttpJsonBrowserFixture");
var fixtureProject = Path.Combine(fixtureRoot, "FrontEndCSharp.Overrides.HttpJsonBrowserFixture.csproj");
var fixtureAssembly = Path.Combine(
    fixtureRoot,
    "bin",
    "Debug",
    "net8.0",
    "HttpJsonBrowserFixture.dll");
var compilerPath = Path.Combine(
    repositoryRoot,
    "Compiler",
    "Core",
    "bin",
    "Debug",
    "net8.0",
    "Compiler.dll");
var generatedOutput = CreateTemporaryDirectory("FrontEndCSharp-HttpJson-Smoke-");
var browserProfile = CreateTemporaryDirectory("FrontEndCSharp-HttpJson-Browser-");

try
{
    await RunAsync(
        "dotnet",
        [compilerPath, "--no-bundle", fixtureProject, generatedOutput, fixtureAssembly],
        repositoryRoot,
        "FrontEndCSharp compilation",
        GetRemainingTime(stopwatch));

    await using var server = new HttpJsonServer(generatedOutput);
    await File.WriteAllTextAsync(
        Path.Combine(generatedOutput, "index.html"),
        "<!doctype html><html><head><meta charset=\"utf-8\"></head>"
            + $"<body data-http-json-endpoint=\"{server.Url}response.json\">"
            + "<script>addEventListener('error',event=>{const exception=event.error?.dotNetException;"
            + "const managedMessage=exception?.v?._message?.v;"
            + "const detail=managedMessage??event.error?.stack??event.message;"
            + "document.body.setAttribute('data-fecs-browser-error',detail);console.error(detail);});</script>"
            + "<script type=\"module\" src=\"/index.js\"></script></body></html>\n",
        Encoding.UTF8);

    var result = await RunChromiumAsync(
        browserPath,
        repositoryRoot,
        browserProfile,
        server.Url,
        server.ReleaseStreamingResponse,
        GetRemainingTime(stopwatch));

    RequireContains(result.StandardOutput, "data-fecs-status=\"ready\"", result.StandardError);
    RequireContains(
        result.StandardOutput,
        "data-fecs-http-json-post-extension=\"Portland\"",
        result.StandardError);
    RequireTargetDuration(stopwatch, "Browser HTTP JSON integration");
    var durationClass = stopwatch.Elapsed >= TimeSpan.FromSeconds(5) ? "slow" : "normal";
    Console.WriteLine(
        $"Browser HTTP JSON integration passed in Chromium in {stopwatch.Elapsed.TotalSeconds:F2} seconds "
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
            "Browser HTTP JSON integration exceeded its absolute 10-second execution limit.");
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
    for (var attempt = 0; attempt < 10 && Directory.Exists(path); attempt++)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            return;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Thread.Sleep(100);
        }
    }
    if (Directory.Exists(path))
    {
        Console.Error.WriteLine($"Warning: could not remove temporary directory '{path}'.");
    }
}

static void RequireContains(string actual, string expected, string diagnostics)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"HTTP JSON fixture did not report expected DOM content '{expected}'."
                + $"{Environment.NewLine}DOM:{Environment.NewLine}{actual}"
                + $"{Environment.NewLine}Chromium diagnostics:{Environment.NewLine}{diagnostics}");
    }
}

static async Task<ProcessResult> RunChromiumAsync(
    string browserPath,
    string workingDirectory,
    string browserProfile,
    string pageUrl,
    Action streamFirstObserved,
    TimeSpan timeout)
{
    var debuggingPort = GetAvailablePort();
    var startInfo = new ProcessStartInfo(browserPath)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (var argument in new[]
    {
        "--headless=new",
        "--disable-gpu",
        "--enable-logging=stderr",
        "--no-default-browser-check",
        "--no-first-run",
        $"--remote-debugging-port={debuggingPort}",
        "--remote-allow-origins=*",
        $"--user-data-dir={browserProfile}",
        pageUrl,
    })
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start Chromium.");
    var standardOutput = process.StandardOutput.ReadToEndAsync();
    var standardError = process.StandardError.ReadToEndAsync();
    using var cancellation = new CancellationTokenSource(timeout);
    var dom = string.Empty;
    Exception? failure = null;
    try
    {
        var debuggerUrl = await FindPageDebuggerUrlAsync(
            debuggingPort,
            pageUrl,
            process,
            cancellation.Token);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(debuggerUrl, cancellation.Token);
        var commandId = 0;
        string? status = null;
        var streamReleased = false;
        while (!cancellation.IsCancellationRequested
            && status != "ready"
            && status != "failed")
        {
            status = await EvaluateStringAsync(
                socket,
                ++commandId,
                "document.body.getAttribute('data-fecs-status')",
                cancellation.Token);
            if (!streamReleased)
            {
                var firstChunk = await EvaluateStringAsync(
                    socket,
                    ++commandId,
                    "document.body.getAttribute('data-fecs-http-stream-first')",
                    cancellation.Token);
                if (firstChunk == "first")
                {
                    streamFirstObserved();
                    streamReleased = true;
                }
                else if (firstChunk == "failed")
                {
                    throw new InvalidOperationException(
                        "The managed streaming fixture rejected the first response chunk.");
                }
            }
            var browserError = await EvaluateStringAsync(
                socket,
                ++commandId,
                "document.body.getAttribute('data-fecs-browser-error')",
                cancellation.Token);
            if (!string.IsNullOrEmpty(browserError))
            {
                throw new InvalidOperationException(
                    $"The managed browser fixture failed: {browserError}");
            }
            if (status != "ready" && status != "failed")
            {
                await Task.Delay(25, cancellation.Token);
            }
        }
        dom = await EvaluateStringAsync(
            socket,
            ++commandId,
            "document.documentElement.outerHTML",
            cancellation.Token) ?? string.Empty;
    }
    catch (OperationCanceledException)
    {
        failure = new TimeoutException($"Chromium did not complete within {timeout}.");
    }
    catch (Exception exception)
    {
        failure = exception;
    }
    finally
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
        await process.WaitForExitAsync();
    }

    var diagnostics = (await standardError) + Environment.NewLine + await standardOutput;
    if (failure !=  null)
    {
        throw new InvalidOperationException(
            failure.Message
                + Environment.NewLine
                + "Chromium diagnostics:"
                + Environment.NewLine
                + diagnostics,
            failure);
    }

    return new ProcessResult(
        0,
        dom,
        diagnostics);
}

static int GetAvailablePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static async Task<Uri> FindPageDebuggerUrlAsync(
    int debuggingPort,
    string pageUrl,
    Process process,
    CancellationToken cancellationToken)
{
    using var client = new HttpClient();
    var endpoint = $"http://127.0.0.1:{debuggingPort}/json/list";
    while (true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (process.HasExited)
        {
            throw new InvalidOperationException(
                $"Chromium exited before exposing its debugging target ({process.ExitCode}).");
        }
        try
        {
            var json = await client.GetStringAsync(endpoint, cancellationToken);
            using var document = JsonDocument.Parse(json);
            foreach (var target in document.RootElement.EnumerateArray())
            {
                if (target.GetProperty("type").GetString() == "page"
                    && target.GetProperty("url").GetString() == pageUrl)
                {
                    return new Uri(target.GetProperty("webSocketDebuggerUrl").GetString()!);
                }
            }
        }
        catch (HttpRequestException)
        {
        }
        await Task.Delay(25, cancellationToken);
    }
}

static async Task<string?> EvaluateStringAsync(
    ClientWebSocket socket,
    int commandId,
    string expression,
    CancellationToken cancellationToken)
{
    using var response = await SendDevToolsCommandAsync(
        socket,
        commandId,
        "Runtime.evaluate",
        new { expression, returnByValue = true },
        cancellationToken);
    var result = response.RootElement
        .GetProperty("result")
        .GetProperty("result");
    return result.TryGetProperty("value", out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

static async Task<JsonDocument> SendDevToolsCommandAsync(
    ClientWebSocket socket,
    int commandId,
    string method,
    object parameters,
    CancellationToken cancellationToken)
{
    var command = JsonSerializer.SerializeToUtf8Bytes(new
    {
        id = commandId,
        method,
        @params = parameters,
    });
    await socket.SendAsync(
        command,
        WebSocketMessageType.Text,
        endOfMessage: true,
        cancellationToken);

    var buffer = new byte[16384];
    while (true)
    {
        using var message = new MemoryStream();
        WebSocketReceiveResult received;
        do
        {
            received = await socket.ReceiveAsync(buffer, cancellationToken);
            message.Write(buffer, 0, received.Count);
        }
        while (!received.EndOfMessage);

        var document = JsonDocument.Parse(message.ToArray());
        if (document.RootElement.TryGetProperty("id", out var id)
            && id.GetInt32() == commandId)
        {
            return document;
        }
        document.Dispose();
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

internal sealed class HttpJsonServer : IAsyncDisposable
{
    private static readonly byte[] JsonResponse = Encoding.UTF8.GetBytes(
        "{\"location\":\"Seattle\",\"forecasts\":[{\"day\":\"Monday\",\"temperatureC\":18},"
            + "{\"day\":\"Tuesday\",\"temperatureC\":21}],\"measurements\":{\"Humidity\":72}}");

    private readonly string root;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task acceptLoop;
    private readonly List<Task> requests = [];
    private readonly object requestsLock = new();
    private readonly TaskCompletionSource streamContinuation = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public HttpJsonServer(string root)
    {
        this.root = Path.GetFullPath(root);
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        Url = $"http://127.0.0.1:{endpoint.Port}/";
        acceptLoop = AcceptAsync();
    }

    public string Url { get; }

    public void ReleaseStreamingResponse() => streamContinuation.TrySetResult();

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
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? headerLine;
            while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync(token)))
            {
                var separator = headerLine.IndexOf(':');
                if (separator > 0)
                {
                    headers[headerLine[..separator]] = headerLine[(separator + 1)..].Trim();
                }
            }

            var requestParts = requestLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var requestMethod = requestParts?.ElementAtOrDefault(0);
            var requestTarget = requestParts?.ElementAtOrDefault(1);
            if (requestMethod == null || requestTarget == null)
            {
                await WriteResponseAsync(stream, 400, "text/plain", [], token);
                return;
            }

            var contentLength = headers.TryGetValue("Content-Length", out var lengthText)
                && int.TryParse(lengthText, out var parsedLength)
                    ? parsedLength
                    : 0;
            var requestBody = new char[contentLength];
            var charsRead = 0;
            while (charsRead < requestBody.Length)
            {
                var count = await reader.ReadAsync(
                    requestBody.AsMemory(charsRead, requestBody.Length - charsRead),
                    token);
                if (count == 0)
                {
                    break;
                }
                charsRead += count;
            }

            var requestPath = new Uri(new Uri(Url), requestTarget).AbsolutePath;
            if (requestPath == "/response.json")
            {
                await WriteResponseAsync(
                    stream,
                    200,
                    "application/json; charset=utf-8",
                    JsonResponse,
                    token);
                return;
            }

            if (requestPath == "/echo.json")
            {
                var contentTypeIsJson = headers.TryGetValue("Content-Type", out var contentType)
                    && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
                if ((requestMethod != "POST" && requestMethod != "PUT")
                    || !contentTypeIsJson
                    || charsRead != requestBody.Length)
                {
                    await WriteResponseAsync(stream, 400, "text/plain", [], token);
                    return;
                }

                await WriteResponseAsync(
                    stream,
                    200,
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(requestBody),
                    token);
                return;
            }

            if (requestPath == "/slow")
            {
                try
                {
                    await WriteDelayedResponseAsync(
                        stream,
                        "text/plain; charset=utf-8",
                        "slow-response"u8.ToArray(),
                        TimeSpan.FromMilliseconds(500),
                        token);
                }
                catch (IOException)
                {
                    // Cancellation and timeout abort the browser connection before
                    // the deliberately delayed response is written.
                }
                return;
            }

            if (requestPath == "/stream")
            {
                await WriteChunkedHeadersAsync(stream, token);
                await WriteChunkAsync(stream, "first"u8.ToArray(), token);
                await streamContinuation.Task.WaitAsync(token);
                await WriteChunkAsync(stream, "second"u8.ToArray(), token);
                await stream.WriteAsync("0\r\n\r\n"u8.ToArray(), token);
                return;
            }

            if (requestPath == "/stream-cancel" || requestPath == "/stream-dispose")
            {
                try
                {
                    await WriteChunkedHeadersAsync(stream, token);
                    await Task.Delay(500, token);
                    await WriteChunkAsync(stream, "late"u8.ToArray(), token);
                    await stream.WriteAsync("0\r\n\r\n"u8.ToArray(), token);
                }
                catch (IOException)
                {
                }
                return;
            }

            if (requestPath == "/stream-error")
            {
                await WriteChunkedHeadersAsync(stream, token);
                await WriteChunkAsync(stream, "first"u8.ToArray(), token);
                return;
            }

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

    private static async Task WriteDelayedResponseAsync(
        Stream stream,
        string contentType,
        byte[] content,
        TimeSpan delay,
        CancellationToken token)
    {
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n"
                + $"Content-Type: {contentType}\r\n"
                + $"Content-Length: {content.Length}\r\n"
                + "Cache-Control: no-store\r\n"
                + "Connection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.FlushAsync(token);
        await Task.Delay(delay, token);
        await stream.WriteAsync(content, token);
    }

    private static async Task WriteChunkedHeadersAsync(
        Stream stream,
        CancellationToken token)
    {
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n"
                + "Content-Type: application/octet-stream\r\n"
                + "Transfer-Encoding: chunked\r\n"
                + "Cache-Control: no-store\r\n"
                + "Connection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.FlushAsync(token);
    }

    private static async Task WriteChunkAsync(
        Stream stream,
        byte[] content,
        CancellationToken token)
    {
        var length = Encoding.ASCII.GetBytes(content.Length.ToString("X") + "\r\n");
        await stream.WriteAsync(length, token);
        await stream.WriteAsync(content, token);
        await stream.WriteAsync("\r\n"u8.ToArray(), token);
        await stream.FlushAsync(token);
    }

    private static string GetContentType(string path) => Path.GetExtension(path) switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" or ".map" => "application/json; charset=utf-8",
        _ => "application/octet-stream",
    };
}
