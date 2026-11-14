using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FrontEndCSharp.Debugger.Integration;

internal sealed class DapClient : IDisposable
{
    private readonly Process process;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private readonly List<JsonElement> messages = new();
    private readonly object messagesLock = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task receiveTask;
    private readonly Task<string> errorTask;
    private int sequence;

    public DapClient(string adapterPath)
    {
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = adapterPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => FailPending(
            new InvalidOperationException(
                $"Adapter exited during a request with code {process.ExitCode}."));
        process.Start();
        errorTask = process.StandardError.ReadToEndAsync();
        receiveTask = ReceiveAsync();
    }

    public int ProcessId => process.Id;

    public string StandardError => errorTask.IsCompleted ? errorTask.Result : string.Empty;

    public IReadOnlyList<JsonElement> Messages
    {
        get
        {
            lock (messagesLock)
            {
                return messages.ToArray();
            }
        }
    }

    public JsonElement Request(
        string command,
        object arguments,
        TimeSpan? timeout = null)
    {
        return RequestAsync(
            command,
            arguments,
            timeout ?? TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }

    public JsonElement RequestSuccess(
        string command,
        object arguments,
        TimeSpan? timeout = null)
    {
        var response = Request(command, arguments, timeout);
        if (!response.GetProperty("success").GetBoolean())
        {
            var message = response.TryGetProperty("message", out var messageProperty)
                ? messageProperty.GetString()
                : response.GetRawText();
            throw new InvalidOperationException(
                $"{command} failed: {message}\n{StandardError}");
        }
        return response;
    }

    public JsonElement RequestFailure(
        string command,
        object arguments,
        TimeSpan? timeout = null)
    {
        var response = Request(command, arguments, timeout);
        if (response.GetProperty("success").GetBoolean())
        {
            throw new InvalidOperationException(
                $"{command} unexpectedly succeeded: {response.GetRawText()}");
        }
        return response;
    }

    public JsonElement WaitFor(
        Func<JsonElement, bool> predicate,
        TimeSpan timeout,
        string phase)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            lock (messagesLock)
            {
                var index = messages.FindIndex(message => predicate(message));
                if (index >= 0)
                {
                    var message = messages[index];
                    messages.RemoveAt(index);
                    return message;
                }
            }
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The adapter exited during {phase} with code {process.ExitCode}.\n{StandardError}");
            }
            Thread.Sleep(10);
        }
        throw new TimeoutException(
            $"Timed out during {phase}. queued: {JsonSerializer.Serialize(Messages)} "
            + $"stderr: {StandardError}");
    }

    public void Dispose()
    {
        process.StandardInput.Close();
        if (!process.HasExited && !process.WaitForExit(TimeSpan.FromSeconds(1)))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        cancellation.Cancel();
        try
        {
            receiveTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        Task.WaitAll(errorTask);
        FailPending(new InvalidOperationException("The debug adapter connection closed."));
        process.Dispose();
        cancellation.Dispose();
    }

    private async Task<JsonElement> RequestAsync(
        string command,
        object arguments,
        TimeSpan timeout)
    {
        var requestSequence = Interlocked.Increment(ref sequence);
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(requestSequence, completion))
        {
            throw new InvalidOperationException(
                $"Duplicate DAP request identifier {requestSequence}.");
        }
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            seq = requestSequence,
            type = "request",
            command,
            arguments,
        });
        await process.StandardInput.BaseStream.WriteAsync(
            Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"));
        await process.StandardInput.BaseStream.WriteAsync(body);
        await process.StandardInput.BaseStream.FlushAsync();
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var registration = timeoutSource.Token.Register(() => completion.TrySetException(
            new TimeoutException(
                $"Adapter {command} exceeded {timeout.TotalSeconds:0} seconds.")));
        try
        {
            return await completion.Task;
        }
        finally
        {
            pending.TryRemove(requestSequence, out _);
        }
    }

    private async Task ReceiveAsync()
    {
        var stream = process.StandardOutput.BaseStream;
        while (!cancellation.IsCancellationRequested)
        {
            var headers = await ReadHeaders(stream, cancellation.Token);
            if (headers == null)
            {
                return;
            }
            var contentLength = ParseContentLength(headers);
            var body = new byte[contentLength];
            await stream.ReadExactlyAsync(body, cancellation.Token);
            using var document = JsonDocument.Parse(body);
            var message = document.RootElement.Clone();
            lock (messagesLock)
            {
                messages.Add(message);
            }
            if (message.GetProperty("type").GetString() == "response"
                && message.TryGetProperty("request_seq", out var requestSequence)
                && pending.TryGetValue(requestSequence.GetInt32(), out var completion))
            {
                completion.TrySetResult(message);
            }
        }
    }

    private static async Task<string?> ReadHeaders(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var buffer = new byte[1];
            var count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0)
            {
                return bytes.Count == 0
                    ? null
                    : throw new EndOfStreamException("The adapter ended inside a DAP header.");
            }
            bytes.Add(buffer[0]);
            var length = bytes.Count;
            if (length >= 4
                && bytes[length - 4] == '\r'
                && bytes[length - 3] == '\n'
                && bytes[length - 2] == '\r'
                && bytes[length - 1] == '\n')
            {
                return Encoding.ASCII.GetString(bytes.ToArray());
            }
        }
    }

    private static int ParseContentLength(string headers)
    {
        foreach (var line in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            const string prefix = "Content-Length:";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line[prefix.Length..].Trim(), out var length)
                && length >= 0)
            {
                return length;
            }
        }
        throw new InvalidDataException("The real adapter emitted an invalid DAP header.");
    }

    private void FailPending(Exception exception)
    {
        foreach (var completion in pending.Values)
        {
            completion.TrySetException(exception);
        }
        pending.Clear();
    }
}
