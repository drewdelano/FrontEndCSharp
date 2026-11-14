using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrontEndCSharp.Compiler.Integration;

internal sealed class CdpClient : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentQueue<string> exceptions = new();
    private Task? receiveTask;
    private int sequence;

    public IReadOnlyCollection<string> Exceptions => exceptions.ToArray();

    public async Task ConnectAsync(Uri endpoint)
    {
        await socket.ConnectAsync(endpoint, cancellation.Token);
        receiveTask = ReceiveAsync();
    }

    public async Task<JsonElement> SendAsync(string method, object? parameters = null)
    {
        var id = Interlocked.Increment(ref sequence);
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(id, completion))
        {
            throw new InvalidOperationException($"Duplicate CDP request identifier {id}.");
        }

        var request = JsonSerializer.SerializeToUtf8Bytes(new CdpRequest
        {
            Id = id,
            Method = method,
            Parameters = parameters ?? new { },
        });
        await socket.SendAsync(
            request,
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

    public async Task<JsonElement> EvaluateAsync(string expression)
    {
        var response = await SendAsync(
            "Runtime.evaluate",
            new { expression, returnByValue = true });
        if (response.TryGetProperty("exceptionDetails", out var exceptionDetails))
        {
            throw new InvalidOperationException(
                "Browser evaluation failed: " + exceptionDetails.GetRawText());
        }
        var result = response.GetProperty("result");
        if (!result.TryGetProperty("value", out var value))
        {
            return default;
        }
        return value.Clone();
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
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
        }
        foreach (var completion in pending.Values)
        {
            completion.TrySetException(
                new InvalidOperationException("The owned browser connection closed."));
        }
        socket.Dispose();
        cancellation.Dispose();
    }

    private async Task ReceiveAsync()
    {
        var buffer = new byte[64 * 1024];
        while (!cancellation.IsCancellationRequested)
        {
            using var message = new MemoryStream();
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
                message.Write(buffer, 0, receive.Count);
            }
            while (!receive.EndOfMessage);

            using var document = JsonDocument.Parse(message.ToArray());
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
            if (root.TryGetProperty("method", out var eventMethod)
                && eventMethod.GetString() == "Runtime.exceptionThrown")
            {
                exceptions.Enqueue(root.GetRawText());
            }
        }
    }

    private sealed class CdpRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("method")]
        public required string Method { get; init; }

        [JsonPropertyName("params")]
        public required object Parameters { get; init; }
    }
}
