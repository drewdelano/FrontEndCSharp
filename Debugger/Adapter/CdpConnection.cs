using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class CdpConnection : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private readonly CancellationTokenSource lifetime = new();
    private int nextId;

    internal event Action<string, JsonElement, string?>? EventReceived;
    internal event Action<Exception?>? Closed;

    internal async Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        await socket.ConnectAsync(endpoint, cancellationToken);
        _ = ReceiveLoopAsync(lifetime.Token);
    }

    internal async Task<JsonElement> SendAsync(
        string method,
        object? parameters = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = completion;

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            id,
            method,
            @params = parameters ?? new { },
            sessionId
        }, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
        var bytes = Encoding.UTF8.GetBytes(document.RootElement.GetRawText());
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);

        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return await completion.Task;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        Exception? failure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                using var document = JsonDocument.Parse(message.ToArray());
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var idProperty))
                {
                    var id = idProperty.GetInt32();
                    if (!pending.TryRemove(id, out var completion))
                    {
                        continue;
                    }
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

                if (root.TryGetProperty("method", out var method))
                {
                    var parameters = root.TryGetProperty("params", out var value)
                        ? value.Clone()
                        : default;
                    var sessionId = root.TryGetProperty("sessionId", out var session)
                        ? session.GetString()
                        : null;
                    EventReceived?.Invoke(method.GetString()!, parameters, sessionId);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failure = exception;
            foreach (var completion in pending.Values)
            {
                completion.TrySetException(exception);
            }
            pending.Clear();
        }
        finally
        {
            Closed?.Invoke(failure);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (socket.State == WebSocketState.Open)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
        }
        socket.Dispose();
        lifetime.Dispose();
    }
}
