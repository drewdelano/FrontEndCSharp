using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrontEndCSharp.Debugger.Integration;

internal sealed class BrowserCdpEvent
{
    public required string Method { get; init; }
    public string? SessionId { get; init; }
    public required JsonElement Parameters { get; init; }
}

internal sealed class BrowserCdpClient : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private readonly CancellationTokenSource cancellation = new();
    private Task? receiveTask;
    private int sequence;

    public Action<BrowserCdpEvent>? EventReceived { get; set; }

    public async Task ConnectAsync(Uri endpoint)
    {
        await socket.ConnectAsync(endpoint, cancellation.Token);
        receiveTask = ReceiveAsync();
    }

    public async Task<JsonElement> SendAsync(
        string method,
        object? parameters = null,
        string? sessionId = null)
    {
        var id = Interlocked.Increment(ref sequence);
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(id, completion))
        {
            throw new InvalidOperationException($"Duplicate CDP request identifier {id}.");
        }
        var body = JsonSerializer.SerializeToUtf8Bytes(new BrowserCdpRequest
        {
            Id = id,
            Method = method,
            Parameters = parameters ?? new { },
            SessionId = sessionId,
        });
        await socket.SendAsync(
            body,
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellation.Token);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var registration = timeout.Token.Register(() => completion.TrySetException(
            new TimeoutException($"{method} exceeded ten seconds.")));
        try
        {
            return await completion.Task;
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        if (socket.State == WebSocketState.Open)
        {
            try
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Complete",
                    CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
        }
        if (receiveTask != null)
        {
            try
            {
                await receiveTask;
            }
            catch (Exception exception) when (exception is OperationCanceledException
                or WebSocketException)
            {
            }
        }
        foreach (var completion in pending.Values)
        {
            completion.TrySetException(
                new InvalidOperationException("The browser CDP connection closed."));
        }
        socket.Dispose();
        cancellation.Dispose();
    }

    private async Task ReceiveAsync()
    {
        var buffer = new byte[64 * 1024];
        while (!cancellation.IsCancellationRequested)
        {
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult receive;
            do
            {
                receive = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellation.Token);
                if (receive.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
                messageStream.Write(buffer, 0, receive.Count);
            }
            while (!receive.EndOfMessage);

            using var document = JsonDocument.Parse(messageStream.ToArray());
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var identifier)
                && pending.TryGetValue(identifier.GetInt32(), out var completion))
            {
                if (root.TryGetProperty("error", out var error))
                {
                    completion.TrySetException(new InvalidOperationException(error.GetRawText()));
                }
                else
                {
                    completion.TrySetResult(root.GetProperty("result").Clone());
                }
                continue;
            }
            if (root.TryGetProperty("method", out var method)
                && root.TryGetProperty("params", out var parameters))
            {
                EventReceived?.Invoke(new BrowserCdpEvent
                {
                    Method = method.GetString() ?? string.Empty,
                    SessionId = root.TryGetProperty("sessionId", out var session)
                        ? session.GetString()
                        : null,
                    Parameters = parameters.Clone(),
                });
            }
        }
    }

    private sealed class BrowserCdpRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("method")]
        public required string Method { get; init; }

        [JsonPropertyName("params")]
        public required object Parameters { get; init; }

        [JsonPropertyName("sessionId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SessionId { get; init; }
    }
}
