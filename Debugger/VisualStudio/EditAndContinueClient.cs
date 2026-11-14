using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace FrontEndCSharp.VisualStudio;

internal sealed class EditAndContinueClient : IDisposable
{
    private readonly Process process;
    private readonly StreamWriter input;
    private readonly StreamReader output;

    private EditAndContinueClient(Process process)
    {
        this.process = process;
        input = process.StandardInput;
        output = process.StandardOutput;
    }

    internal static async Task<EditAndContinueSession> StartAsync(
        string executable,
        string projectPath,
        string sessionDirectory)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = string.Join(" ", new[]
            {
                DebuggerProcessRunner.QuoteArgument(projectPath),
                DebuggerProcessRunner.QuoteArgument(sessionDirectory)
            }),
            WorkingDirectory = Path.GetDirectoryName(projectPath),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Unable to start the Edit and Continue compiler host.");
        var client = new EditAndContinueClient(process);
        var initial = await client.ReadResponseAsync();
        if (!initial.Success)
        {
            var stderr = await process.StandardError.ReadToEndAsync();
            client.Dispose();
            throw new InvalidOperationException(initial.Error + Environment.NewLine + stderr);
        }
        return new EditAndContinueSession(client, initial);
    }

    internal async Task<EditHostResponse> RequestAsync(string command, int generation)
    {
        if (process.HasExited)
        {
            throw new InvalidOperationException(
                $"The Edit and Continue compiler host exited with code {process.ExitCode}.");
        }
        await input.WriteLineAsync(Serialize(new EditHostRequest
        {
            Command = command,
            Generation = generation
        }));
        await input.FlushAsync();
        return await ReadResponseAsync();
    }

    internal static async Task<BrokerResponse> ApplyAsync(
        string pipeName,
        string sessionToken,
        string updateModulePath,
        string debugManifestPath)
    {
        using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await pipe.ConnectAsync(10_000);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var reader = new StreamReader(pipe, utf8, false, 1024, leaveOpen: true);
        using var writer = new StreamWriter(pipe, utf8, 1024, leaveOpen: true)
        {
            AutoFlush = true
        };
        await writer.WriteLineAsync(Serialize(new BrokerRequest
        {
            SessionToken = sessionToken,
            UpdateModulePath = updateModulePath,
            DebugManifestPath = debugManifestPath
        }));
        var line = await reader.ReadLineAsync();
        return Deserialize<BrokerResponse>(line
            ?? throw new EndOfStreamException("The debug adapter closed the Edit and Continue channel."));
    }

    private async Task<EditHostResponse> ReadResponseAsync()
    {
        var line = await output.ReadLineAsync();
        return Deserialize<EditHostResponse>(line
            ?? throw new EndOfStreamException("The Edit and Continue compiler host closed unexpectedly."));
    }

    internal static T Deserialize<T>(string json)
    {
        var serializer = new DataContractJsonSerializer(typeof(T));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return (T)serializer.ReadObject(stream);
    }

    internal static string Serialize<T>(T value)
    {
        var serializer = new DataContractJsonSerializer(typeof(T));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public void Dispose()
    {
        try
        {
            input.Close();
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(2_000);
            }
        }
        catch (InvalidOperationException)
        {
        }
        process.Dispose();
    }
}

internal sealed class EditAndContinueSession
{
    public EditAndContinueSession(EditAndContinueClient client, EditHostResponse initial)
    {
        Client = client;
        Initial = initial;
    }

    public EditAndContinueClient Client { get; }
    public EditHostResponse Initial { get; }
}

[DataContract]
internal sealed class EditHostRequest
{
    [DataMember(Name = "command")]
    public string Command { get; set; } = string.Empty;

    [DataMember(Name = "generation")]
    public int Generation { get; set; }
}

[DataContract]
internal sealed class EditHostResponse
{
    [DataMember(Name = "success")]
    public bool Success { get; set; }

    [DataMember(Name = "generation")]
    public int Generation { get; set; }

    [DataMember(Name = "assemblyPath")]
    public string AssemblyPath { get; set; } = string.Empty;

    [DataMember(Name = "pdbPath")]
    public string PdbPath { get; set; } = string.Empty;

    [DataMember(Name = "metadataDeltaPath")]
    public string MetadataDeltaPath { get; set; } = string.Empty;

    [DataMember(Name = "ilDeltaPath")]
    public string IlDeltaPath { get; set; } = string.Empty;

    [DataMember(Name = "pdbDeltaPath")]
    public string PdbDeltaPath { get; set; } = string.Empty;

    [DataMember(Name = "updatedMethodTokens")]
    public int[] UpdatedMethodTokens { get; set; } = Array.Empty<int>();

    [DataMember(Name = "error")]
    public string Error { get; set; } = string.Empty;
}

[DataContract]
internal sealed class BrokerRequest
{
    [DataMember(Name = "sessionToken")]
    public string SessionToken { get; set; } = string.Empty;

    [DataMember(Name = "updateModulePath")]
    public string UpdateModulePath { get; set; } = string.Empty;

    [DataMember(Name = "debugManifestPath")]
    public string DebugManifestPath { get; set; } = string.Empty;
}

[DataContract]
internal sealed class BrokerResponse
{
    [DataMember(Name = "success")]
    public bool Success { get; set; }

    [DataMember(Name = "generation")]
    public int Generation { get; set; }

    [DataMember(Name = "hash")]
    public string Hash { get; set; } = string.Empty;

    [DataMember(Name = "error")]
    public string Error { get; set; } = string.Empty;
}

[DataContract]
internal sealed class UpdateTranslationResponse
{
    [DataMember(Name = "UpdateModulePath")]
    public string UpdateModulePath { get; set; } = string.Empty;

    [DataMember(Name = "DebugManifestPath")]
    public string DebugManifestPath { get; set; } = string.Empty;

    [DataMember(Name = "Generation")]
    public int Generation { get; set; }

    [DataMember(Name = "Hash")]
    public string Hash { get; set; } = string.Empty;
}
