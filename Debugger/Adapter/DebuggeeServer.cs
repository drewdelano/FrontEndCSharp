using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class DebuggeeServer : IAsyncDisposable
{
    private readonly string generatedOutputPath;
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource lifetime = new();

    internal DebuggeeServer(string generatedOutputPath)
    {
        this.generatedOutputPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(generatedOutputPath));
    }

    internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    internal void Start()
    {
        listener.Start();
        _ = AcceptLoopAsync(lifetime.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                await ServeAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.ASCII, false, leaveOpen: true))
        {
            var request = await reader.ReadLineAsync(cancellationToken);
            string? header;
            do
            {
                header = await reader.ReadLineAsync(cancellationToken);
            }
            while (!string.IsNullOrEmpty(header));

            var path = request?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
            var queryIndex = path?.IndexOf('?') ?? -1;
            if (queryIndex >= 0)
            {
                path = path![..queryIndex];
            }
            try
            {
                if (path is "/" or "/index.html")
                {
                    await RespondAsync(stream, "200 OK", "text/html; charset=utf-8", DebuggeeHtml(), cancellationToken);
                    return;
                }
                if (path == "/favicon.ico")
                {
                    await RespondAsync(stream, "204 No Content", "image/x-icon", string.Empty, cancellationToken);
                    return;
                }

                var relativePath = Uri.UnescapeDataString(path ?? string.Empty).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var filePath = Path.GetFullPath(Path.Combine(generatedOutputPath, relativePath));
                var pathFromRoot = Path.GetRelativePath(generatedOutputPath, filePath);
                if (pathFromRoot == ".."
                    || pathFromRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || Path.IsPathRooted(pathFromRoot)
                    || !File.Exists(filePath))
                {
                    await RespondAsync(stream, "404 Not Found", "text/plain", "Not found", cancellationToken);
                    return;
                }

                if (string.Equals(Path.GetExtension(filePath), ".bin", StringComparison.OrdinalIgnoreCase))
                {
                    await RespondAsync(
                        stream,
                        "200 OK",
                        "application/octet-stream",
                        await File.ReadAllBytesAsync(filePath, cancellationToken),
                        cancellationToken);
                    return;
                }
                await RespondAsync(stream, "200 OK", "text/javascript; charset=utf-8", await File.ReadAllTextAsync(filePath, cancellationToken), cancellationToken);
            }
            catch
            {
                await RespondAsync(stream, "500 Internal Server Error", "text/plain", "Server error", cancellationToken);
            }
        }
    }

    private string DebuggeeHtml()
    {
        return """
            <!doctype html>
            <script type="module">
            try {
                await import("/index.js");
            } catch (error) {
                console.error("Managed program terminated with an exception", error);
            }
            console.log("__FECS_DEBUGGEE_COMPLETED__");
            </script>
            """;
    }

    private static async Task RespondAsync(NetworkStream stream, string status, string contentType, string body, CancellationToken cancellationToken)
    {
        await RespondAsync(stream, status, contentType, Encoding.UTF8.GetBytes(body), cancellationToken);
    }

    private static async Task RespondAsync(NetworkStream stream, string status, string contentType, byte[] content, CancellationToken cancellationToken)
    {
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(content, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        listener.Stop();
        lifetime.Dispose();
        return ValueTask.CompletedTask;
    }
}
