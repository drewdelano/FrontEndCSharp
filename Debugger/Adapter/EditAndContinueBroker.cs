using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class EditAndContinueBroker : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private readonly string pipeName;
    private readonly string sessionToken;
    private readonly Func<string, string, Task<RuntimeUpdateAcknowledgement>> applyUpdate;
    private readonly Action<string> diagnostic;
    private readonly CancellationTokenSource lifetime = new();
    private Task? serverTask;

    internal EditAndContinueBroker(
        string pipeName,
        string sessionToken,
        Func<string, string, Task<RuntimeUpdateAcknowledgement>> applyUpdate,
        Action<string> diagnostic)
    {
        this.pipeName = pipeName;
        this.sessionToken = sessionToken;
        this.applyUpdate = applyUpdate;
        this.diagnostic = diagnostic;
    }

    internal void Start() => serverTask = ServeAsync(lifetime.Token);

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken);
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                using var reader = new StreamReader(pipe, utf8, false, 1024, leaveOpen: true);
                await using var writer = new StreamWriter(pipe, utf8, 1024, leaveOpen: true) { AutoFlush = true };
                BrokerResponse response;
                try
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    var request = JsonSerializer.Deserialize<BrokerRequest>(line
                        ?? throw new InvalidDataException("The Edit and Continue request was empty."), JsonOptions)
                        ?? throw new InvalidDataException("The Edit and Continue request was empty.");
                    if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(request.SessionToken),
                        Encoding.UTF8.GetBytes(sessionToken)))
                    {
                        throw new UnauthorizedAccessException("The Edit and Continue session token is invalid.");
                    }
                    var acknowledgement = await applyUpdate(
                        request.UpdateModulePath,
                        request.DebugManifestPath);
                    response = new BrokerResponse
                    {
                        Success = true,
                        Generation = acknowledgement.Generation,
                        Hash = acknowledgement.Hash
                    };
                }
                catch (Exception exception)
                {
                    response = new BrokerResponse { Error = exception.Message };
                    diagnostic($"Edit and Continue rejected: {exception}");
                }
                await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                diagnostic($"Edit and Continue broker failure: {exception}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (serverTask !=  null)
        {
            try
            {
                await serverTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
        lifetime.Dispose();
    }

    private sealed class BrokerRequest
    {
        public string SessionToken { get; set; } = string.Empty;
        public string UpdateModulePath { get; set; } = string.Empty;
        public string DebugManifestPath { get; set; } = string.Empty;
    }

    private sealed class BrokerResponse
    {
        public bool Success { get; set; }
        public int Generation { get; set; }
        public string Hash { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
