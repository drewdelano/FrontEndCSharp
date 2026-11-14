using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;

namespace FrontEndCSharp.Compiler.Integration;

internal sealed class StaticFileServer : IDisposable
{
    private readonly string root;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task serverTask;
    private readonly ConcurrentQueue<string> failedRequests = new();

    public StaticFileServer(string root)
    {
        this.root = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Url = $"http://127.0.0.1:{port}/";
        serverTask = ServeAsync(cancellation.Token);
    }

    public string Url { get; }

    public IReadOnlyCollection<string> FailedRequests => failedRequests.ToArray();

    public void Dispose()
    {
        cancellation.Cancel();
        listener.Stop();
        try
        {
            serverTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        cancellation.Dispose();
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            _ = ServeClientAsync(client, cancellationToken);
        }
    }

    private async Task ServeClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(
            stream,
            Encoding.ASCII,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true))
        {
            var requestLine = await reader.ReadLineAsync(cancellationToken);
            string? header;
            do
            {
                header = await reader.ReadLineAsync(cancellationToken);
            }
            while (!string.IsNullOrEmpty(header));

            var requestTarget = requestLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ElementAtOrDefault(1);
            if (requestTarget == null)
            {
                failedRequests.Enqueue("400 <missing request target>");
                await WriteResponse(stream, 400, "text/plain", Array.Empty<byte>(), cancellationToken);
                return;
            }
            var requestPath = new Uri(new Uri(Url), requestTarget).AbsolutePath;
            var relativePath = requestPath == "/"
                ? "index.html"
                : Uri.UnescapeDataString(requestPath.TrimStart('/'));
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                failedRequests.Enqueue("403 " + requestTarget);
                await WriteResponse(stream, 403, "text/plain", Array.Empty<byte>(), cancellationToken);
                return;
            }
            if (!File.Exists(candidate))
            {
                failedRequests.Enqueue("404 " + requestTarget);
                await WriteResponse(stream, 404, "text/plain", Array.Empty<byte>(), cancellationToken);
                return;
            }
            await WriteResponse(
                stream,
                200,
                GetContentType(candidate),
                await File.ReadAllBytesAsync(candidate, cancellationToken),
                cancellationToken);
        }
    }

    private static async Task WriteResponse(
        NetworkStream stream,
        int statusCode,
        string contentType,
        byte[] body,
        CancellationToken cancellationToken)
    {
        var reason = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            403 => "Forbidden",
            _ => "Not Found",
        };
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} {reason}\r\nContent-Type: {contentType}\r\n"
            + $"Content-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    private static string GetContentType(string path) => Path.GetExtension(path) switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" or ".map" => "application/json; charset=utf-8",
        _ => "application/octet-stream",
    };
}
