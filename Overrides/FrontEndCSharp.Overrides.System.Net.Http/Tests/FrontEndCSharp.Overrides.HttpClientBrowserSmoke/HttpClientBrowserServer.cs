using System.Net;
using System.Net.Sockets;
using System.Text;

internal sealed class HttpClientBrowserServer : IAsyncDisposable
{
    private static readonly byte[] ProbeBody = Encoding.UTF8.GetBytes(
        "FrontEndCSharp HTTP response — buffered");

    private readonly string generatedOutput;
    private readonly LoopbackHttpServer mainServer;

    public HttpClientBrowserServer(string generatedOutput)
    {
        this.generatedOutput = Path.GetFullPath(generatedOutput);
        mainServer = new LoopbackHttpServer(HandleMainRequestAsync);
    }

    public string Origin => mainServer.Origin;

    public async ValueTask DisposeAsync()
    {
        await mainServer.DisposeAsync();
    }

    private async Task<HttpResponseData> HandleMainRequestAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(new Uri(Origin), request.Target);
        switch (requestUri.AbsolutePath)
        {
            case "/http-probe":
                return HttpResponseData.Create(202, ProbeBody)
                    .WithHeader("Content-Type", "text/plain; charset=utf-8")
                    .WithHeader("X-Fecs-Probe", "response-header")
                    .WithHeader("X-Fecs-Multi", "first")
                    .WithHeader("X-Fecs-Multi", "second");

            case "/http-echo":
                return TextResponse(
                    201,
                    string.Join(
                        "|",
                        request.Method,
                        request.GetHeader("X-Fecs-Request"),
                        request.GetHeader("Content-Type"),
                        request.GetHeader("Authorization"),
                        request.GetHeader("Accept"),
                        Encoding.UTF8.GetString(request.Body)));

            default:
                return await ServeGeneratedFileAsync(requestUri.AbsolutePath, cancellationToken);
        }
    }

    private async Task<HttpResponseData> ServeGeneratedFileAsync(
        string requestPath,
        CancellationToken cancellationToken)
    {
        var relativePath = requestPath == "/"
            ? "index.html"
            : Uri.UnescapeDataString(requestPath[1..]);
        var candidate = Path.GetFullPath(
            Path.Combine(
                generatedOutput,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = generatedOutput.EndsWith(Path.DirectorySeparatorChar)
            ? generatedOutput
            : generatedOutput + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(candidate))
        {
            return TextResponse(404, string.Empty);
        }

        var content = await File.ReadAllBytesAsync(candidate, cancellationToken);
        return HttpResponseData.Create(200, content)
            .WithHeader("Content-Type", GetContentType(candidate));
    }

    private static HttpResponseData TextResponse(int statusCode, string content)
    {
        return HttpResponseData.Create(statusCode, Encoding.UTF8.GetBytes(content))
            .WithHeader("Content-Type", "text/plain; charset=utf-8");
    }

    private static string GetContentType(string path) => Path.GetExtension(path) switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" or ".map" => "application/json; charset=utf-8",
        _ => "application/octet-stream",
    };
}

internal sealed class LoopbackHttpServer : IAsyncDisposable
{
    private readonly Func<HttpRequestData, CancellationToken, Task<HttpResponseData>> handler;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task acceptLoop;
    private readonly List<Task> requests = [];
    private readonly object requestsGate = new();

    public LoopbackHttpServer(
        Func<HttpRequestData, CancellationToken, Task<HttpResponseData>> handler)
    {
        this.handler = handler;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        Origin = $"http://127.0.0.1:{endpoint.Port}/";
        acceptLoop = AcceptAsync();
    }

    public string Origin { get; }

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

        Task[] pendingRequests;
        lock (requestsGate)
        {
            pendingRequests = [.. requests];
        }
        try
        {
            await Task.WhenAll(pendingRequests);
        }
        catch (IOException)
        {
            // Chromium can close a canceled Fetch connection before the delayed
            // test response is written.
        }
        catch (OperationCanceledException)
        {
        }
        cancellation.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(cancellation.Token);
            var request = ServeAsync(client, cancellation.Token);
            lock (requestsGate)
            {
                requests.Add(request);
            }
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            try
            {
                var request = await HttpRequestData.ReadAsync(stream, cancellationToken);
                var response = await handler(request, cancellationToken);
                await response.WriteAsync(stream, cancellationToken);
            }
            catch (IOException)
            {
                // A browser-side cancellation owns the connection lifetime.
            }
        }
    }
}

internal sealed class HttpRequestData
{
    private HttpRequestData(
        string method,
        string target,
        Dictionary<string, string> headers,
        byte[] body)
    {
        Method = method;
        Target = target;
        Headers = headers;
        Body = body;
    }

    public string Method { get; }
    public string Target { get; }
    public Dictionary<string, string> Headers { get; }
    public byte[] Body { get; }

    public string GetHeader(string name)
    {
        return Headers.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public static async Task<HttpRequestData> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var headerBytes = new MemoryStream();
        var terminatorState = 0;
        while (terminatorState < 4)
        {
            var buffer = new byte[1];
            var count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0)
            {
                throw new IOException("The HTTP request ended before its headers were complete.");
            }
            headerBytes.WriteByte(buffer[0]);
            if (headerBytes.Length > 64 * 1024)
            {
                throw new InvalidDataException("The HTTP request headers exceeded 64 KiB.");
            }
            terminatorState = NextTerminatorState(terminatorState, buffer[0]);
        }

        var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
        var lines = headerText.Split("\r\n", StringSplitOptions.None);
        var requestParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestParts.Length < 2)
        {
            throw new InvalidDataException("The HTTP request line was malformed.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }
            var name = line[..separator];
            var value = line[(separator + 1)..].Trim();
            headers[name] = headers.TryGetValue(name, out var existing)
                ? existing + ", " + value
                : value;
        }

        var contentLength = headers.TryGetValue("Content-Length", out var lengthText)
            && int.TryParse(lengthText, out var parsedLength)
                ? parsedLength
                : 0;
        var body = new byte[contentLength];
        var offset = 0;
        while (offset < body.Length)
        {
            var count = await stream.ReadAsync(body.AsMemory(offset), cancellationToken);
            if (count == 0)
            {
                throw new IOException("The HTTP request ended before its body was complete.");
            }
            offset += count;
        }

        return new HttpRequestData(requestParts[0], requestParts[1], headers, body);
    }

    private static int NextTerminatorState(int state, byte value)
    {
        if (value == 13)
        {
            return state == 2 ? 3 : 1;
        }
        if (value == 10)
        {
            return state switch
            {
                1 => 2,
                3 => 4,
                _ => 0,
            };
        }
        return 0;
    }
}

internal sealed class HttpResponseData
{
    private readonly List<HttpHeaderData> headers = [];

    private HttpResponseData(int statusCode, byte[] body)
    {
        StatusCode = statusCode;
        Body = body;
    }

    public int StatusCode { get; }
    public byte[] Body { get; }

    public static HttpResponseData Create(int statusCode, byte[] body)
    {
        return new HttpResponseData(statusCode, body);
    }

    public HttpResponseData WithHeader(string name, string value)
    {
        headers.Add(new HttpHeaderData(name, value));
        return this;
    }

    public async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append("HTTP/1.1 ")
            .Append(StatusCode)
            .Append(' ')
            .Append(GetReasonPhrase(StatusCode))
            .Append("\r\n");
        foreach (var header in headers)
        {
            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }
        builder.Append("Content-Length: ").Append(Body.Length).Append("\r\n")
            .Append("Cache-Control: no-store\r\n")
            .Append("Connection: close\r\n\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(builder.ToString()), cancellationToken);
        await stream.WriteAsync(Body, cancellationToken);
    }

    private static string GetReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "OK",
        201 => "Created",
        202 => "Accepted",
        204 => "No Content",
        302 => "Found",
        404 => "Not Found",
        500 => "Internal Server Error",
        _ => "Response",
    };
}

internal sealed class HttpHeaderData
{
    public HttpHeaderData(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public string Value { get; }
}
