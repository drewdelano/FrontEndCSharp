using System.Text.Json;
using System.Text.RegularExpressions;

namespace FrontEndCSharp.Debugger.Integration;

internal static class DebuggerAdapterFailure
{
    public static void Run(string? outputPath)
    {
        var adapterPath = Path.Combine(
            RepositoryLayout.Root,
            "Debugger",
            "Adapter",
            "bin",
            "Debug",
            "net8.0",
            "FrontEndCSharp.DebugAdapter.exe");
        using var adapter = new DapClient(adapterPath);
        try
        {
            var initialized = adapter.Request(
                "initialize",
                new { adapterID = "frontendcsharp-hardening", linesStartAt1 = true });
            RequireSuccess(initialized, expected: true, "Adapter initialization failed.");
            var failure = adapter.Request("launch", new
            {
                program = Path.Combine(
                    RepositoryLayout.Root,
                    "Demo",
                    "FrontEndCSharp.ReactDemo",
                    "DebuggerSteppingFixture.cs"),
                browserPath = @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
                generatedOutputPath = Path.Combine(
                    RepositoryLayout.Root,
                    ".vs",
                    "DebuggerIntegration",
                    "absent-output"),
                debugManifestPath = Path.Combine(
                    RepositoryLayout.Root,
                    ".vs",
                    "DebuggerIntegration",
                    "absent.frontendcsharp.debug.json"),
            });
            RequireSuccess(
                failure,
                expected: false,
                "Missing launch input must produce a real adapter failure.");
            if (!Regex.IsMatch(
                failure.GetRawText(),
                "manifest|exist|find|directory",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException(
                    "The launch failure did not describe the missing input: "
                    + failure.GetRawText());
            }
            WriteResult(outputPath, new
            {
                passed = true,
                processId = adapter.ProcessId,
                failure,
                messages = adapter.Messages,
                stderr = adapter.StandardError,
            });
            var message = failure.TryGetProperty("message", out var failureMessage)
                ? failureMessage.GetString()
                : null;
            Console.WriteLine(JsonSerializer.Serialize(new { passed = true, failure = message }));
        }
        catch (Exception exception)
        {
            WriteResult(outputPath, new
            {
                passed = false,
                error = exception.Message,
                messages = adapter.Messages,
                stderr = adapter.StandardError,
            });
            throw;
        }
    }

    private static void RequireSuccess(
        JsonElement response,
        bool expected,
        string message)
    {
        if (response.GetProperty("success").GetBoolean() != expected)
        {
            throw new InvalidDataException(message + " " + response.GetRawText());
        }
    }

    private static void WriteResult(string? path, object result)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllText(
                Path.GetFullPath(path),
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
