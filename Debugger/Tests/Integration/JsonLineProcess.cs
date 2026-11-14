using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace FrontEndCSharp.Debugger.Integration;

internal sealed class JsonLineProcess : IDisposable
{
    private readonly Process process;
    private readonly Channel<JsonElement> responses = Channel.CreateUnbounded<JsonElement>();
    private readonly Task outputTask;
    private readonly Task<string> errorTask;

    public JsonLineProcess(string fileName, params string[] arguments)
    {
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start();
        errorTask = process.StandardError.ReadToEndAsync();
        outputTask = ReadOutput();
    }

    public JsonElement NextResponse()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            return responses.Reader.ReadAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                "Timed out waiting for the JSON-line host. stderr:\n" + CurrentError());
        }
    }

    public JsonElement Request(object request)
    {
        process.StandardInput.WriteLine(JsonSerializer.Serialize(request));
        process.StandardInput.Flush();
        return NextResponse();
    }

    public void Dispose()
    {
        process.StandardInput.Close();
        if (!process.HasExited && !process.WaitForExit(TimeSpan.FromSeconds(2)))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        Task.WaitAll(outputTask, errorTask);
        process.Dispose();
    }

    private async Task ReadOutput()
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (line == null)
            {
                responses.Writer.TryComplete();
                return;
            }
            using var document = JsonDocument.Parse(line);
            await responses.Writer.WriteAsync(document.RootElement.Clone());
        }
    }

    private string CurrentError() => errorTask.IsCompleted
        ? errorTask.Result
        : "<process is still running>";
}
