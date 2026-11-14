using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FrontEndCSharp.Debugger.Integration;

internal sealed class DebuggerWebHost : IDisposable
{
    private readonly string root;
    private readonly int importDelayMilliseconds;
    private readonly string bodyMarkup;
    private readonly string afterImportScript;
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource<bool> completed = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task serverTask;

    public DebuggerWebHost(
        string root,
        int importDelayMilliseconds,
        string bodyMarkup,
        string afterImportScript)
    {
        this.root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        this.importDelayMilliseconds = importDelayMilliseconds;
        this.bodyMarkup = bodyMarkup;
        this.afterImportScript = afterImportScript;
        listener.Start();
        Url = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        serverTask = AcceptLoopAsync(lifetime.Token);
    }

    public Uri Url { get; }
    public Task Completed => completed.Task;

    public void Dispose()
    {
        lifetime.Cancel();
        listener.Stop();
        try
        {
            serverTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        lifetime.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = ServeAndDisposeAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ServeAndDisposeAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(
            stream,
            Encoding.ASCII,
            false,
            leaveOpen: true))
        {
            var request = await reader.ReadLineAsync(cancellationToken);
            string? header;
            do
            {
                header = await reader.ReadLineAsync(cancellationToken);
            }
            while (!string.IsNullOrEmpty(header));
            var requestParts = request?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var method = requestParts?.ElementAtOrDefault(0);
            var rawPath = requestParts?.ElementAtOrDefault(1) ?? string.Empty;
            var queryIndex = rawPath.IndexOf('?');
            var path = queryIndex >= 0 ? rawPath[..queryIndex] : rawPath;
            if (method == "POST" && path == "/__completed")
            {
                await RespondAsync(
                    stream,
                    "204 No Content",
                    "text/plain",
                    [],
                    cancellationToken);
                completed.TrySetResult(true);
                return;
            }
            if (path == "/")
            {
                var html = $$"""
                    <!doctype html>{{bodyMarkup}}<script type="module">
                    setTimeout(async () => {
                        try {
                            await import("/js/index.js");
                            {{afterImportScript}}
                        }
                        finally { await fetch("/__completed", { method: "POST" }); }
                    }, {{importDelayMilliseconds}});
                    </script>
                    """;
                await RespondAsync(
                    stream,
                    "200 OK",
                    "text/html; charset=utf-8",
                    Encoding.UTF8.GetBytes(html),
                    cancellationToken);
                return;
            }

            var relative = Uri.UnescapeDataString(path)
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);
            if (relative.StartsWith(
                "js" + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            {
                relative = relative[3..];
            }
            var filePath = Path.GetFullPath(Path.Combine(root, relative));
            var relativeFromRoot = Path.GetRelativePath(root, filePath);
            if (relativeFromRoot == ".."
                || relativeFromRoot.StartsWith(
                    ".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)
                || Path.IsPathRooted(relativeFromRoot)
                || !File.Exists(filePath))
            {
                await RespondAsync(
                    stream,
                    "404 Not Found",
                    "text/plain",
                    [],
                    cancellationToken);
                return;
            }
            var contentType = Path.GetExtension(filePath) == ".js"
                ? "text/javascript; charset=utf-8"
                : "application/octet-stream";
            await RespondAsync(
                stream,
                "200 OK",
                contentType,
                await File.ReadAllBytesAsync(filePath, cancellationToken),
                cancellationToken);
        }
    }

    private static async Task RespondAsync(
        NetworkStream stream,
        string status,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\n"
            + $"Content-Length: {content.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(content, cancellationToken);
    }
}
