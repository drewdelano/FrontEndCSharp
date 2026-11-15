using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace FrontEndCSharp.Debugger.Integration;

internal static class DebugAdapterEditAndContinueSmoke
{
    private static readonly TimeSpan DapTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static void Run(string browserPath)
    {
        var repository = RepositoryLayout.Root;
        var temporary = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-DAP-EnC-" + Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(temporary, "project");
        var sessionRoot = Path.Combine(temporary, "session");
        var generatedOutput = Path.Combine(temporary, "debug-js");
        var fixture = Path.Combine(
            repository,
            "Debugger",
            "Tests",
            "Fixtures",
            "EditAndContinueFixture");
        CopyFixture(fixture, projectRoot);
        try
        {
            var projectPath = Path.Combine(projectRoot, "EditAndContinueFixture.csproj");
            var sourcePath = Path.Combine(projectRoot, "Program.cs");
            File.WriteAllText(
                projectPath,
                File.ReadAllText(projectPath).Replace(
                    @"..\..\..\..\Overrides",
                    Path.Combine(repository, "Overrides"),
                    StringComparison.Ordinal));
            BuildPrerequisites(repository);
            var compilerPath = Path.Combine(
                repository,
                "Compiler",
                "Core",
                "bin",
                "Debug",
                "net8.0",
                "Compiler.exe");
            var hostPath = Path.Combine(
                repository,
                "Debugger",
                "EditAndContinue",
                "bin",
                "Debug",
                "net8.0",
                "FrontEndCSharp.EditAndContinue.exe");
            var adapterPath = Path.Combine(
                repository,
                "Debugger",
                "Adapter",
                "bin",
                "Debug",
                "net8.0",
                "FrontEndCSharp.DebugAdapter.exe");
            var pipeName = "FrontEndCSharp-EnC-Smoke-" + Guid.NewGuid().ToString("N");
            var sessionToken = Guid.NewGuid().ToString("N");

            using var host = new JsonLineProcess(hostPath, projectPath, sessionRoot);
            var initial = host.NextResponse();
            RequireSuccess(initial, expected: true);
            var initialAssembly = GetString(initial, "assemblyPath");
            RunCompiler(
                compilerPath,
                repository,
                "--debugger-rendezvous-timeout-ms",
                "10000",
                projectPath,
                generatedOutput,
                initialAssembly);
            var currentManifest = Path.Combine(
                Path.GetDirectoryName(initialAssembly)!,
                "EditAndContinueFixture.frontendcsharp.debug.json");

            using var adapter = new DapClient(adapterPath);
            try
            {
                adapter.RequestSuccess("initialize", new
                {
                    adapterID = "front-end-csharp",
                    clientID = "edit-and-continue-smoke",
                    linesStartAt1 = true,
                    columnsStartAt1 = true,
                    pathFormat = "path",
                }, DapTimeout);
                adapter.WaitFor(
                    message => IsEvent(message, "initialized"),
                    DapTimeout,
                    "Edit-and-Continue initialization");
                adapter.RequestSuccess("launch", new
                {
                    program = sourcePath,
                    browserPath,
                    browserHeadless = true,
                    generatedOutputPath = generatedOutput,
                    debugManifestPath = currentManifest,
                    editAndContinuePipe = pipeName,
                    editAndContinueToken = sessionToken,
                }, DapTimeout);
                var initialBreakpoints = adapter.RequestSuccess("setBreakpoints", new
                {
                    source = new { name = "Program.cs", path = sourcePath },
                    breakpoints = new[] { new { line = 8 } },
                    lines = new[] { 8 },
                }, DapTimeout);
                var initialBreakpoint = Body(initialBreakpoints)
                    .GetProperty("breakpoints")[0];
                Require(
                    initialBreakpoint.GetProperty("verified").GetBoolean(),
                    "Initial Edit-and-Continue breakpoint did not bind: "
                    + initialBreakpoints.GetRawText());
                adapter.RequestSuccess("configurationDone", new { }, DapTimeout);
                var initialStop = adapter.WaitFor(
                    message => IsStoppedEvent(message, "breakpoint"),
                    DapTimeout,
                    "initial Edit-and-Continue breakpoint");

                var originalSource = File.ReadAllText(sourcePath);
                File.WriteAllText(
                    sourcePath,
                    originalSource.Replace(
                        "Console.WriteLine(getter());",
                        "Console.WriteLine(getter() + 0);",
                        StringComparison.Ordinal));
                var activePrepared = host.Request(new { command = "prepare", generation = 0 });
                RequireSuccess(activePrepared, expected: true);
                var activeUpdate = Translate(
                    compilerPath,
                    repository,
                    projectPath,
                    generatedOutput,
                    activePrepared,
                    currentManifest,
                    "active-candidate-js");
                var activeUpdatePath = Path.Combine(
                    generatedOutput,
                    activeUpdate.UpdateModulePath);
                var untamperedUpdate = File.ReadAllText(activeUpdatePath);
                File.WriteAllText(activeUpdatePath, untamperedUpdate + "\n// torn update simulation");
                var tornRejected = ApplyBroker(
                    pipeName,
                    sessionToken,
                    activeUpdate.UpdateModulePath,
                    activeUpdate.DebugManifestPath);
                RequireFailure(tornRejected, "does not match manifest hash");
                File.WriteAllText(activeUpdatePath, untamperedUpdate);
                var activeRejected = ApplyBroker(
                    pipeName,
                    sessionToken,
                    activeUpdate.UpdateModulePath,
                    activeUpdate.DebugManifestPath);
                RequireFailure(activeRejected, "Cannot replace active method");
                var discarded = host.Request(new
                {
                    command = "discard",
                    generation = GetInt32(activePrepared, "generation"),
                });
                RequireSuccess(discarded, expected: true);

                File.WriteAllText(
                    sourcePath,
                    originalSource.Replace(
                        "var result = 1;",
                        "\n        var result = 2;",
                        StringComparison.Ordinal));
                var inactivePrepared = host.Request(new { command = "prepare", generation = 0 });
                RequireSuccess(inactivePrepared, expected: true);
                var inactiveUpdate = Translate(
                    compilerPath,
                    repository,
                    projectPath,
                    generatedOutput,
                    inactivePrepared,
                    currentManifest,
                    "inactive-candidate-js");

                var temporarilyUnverified = adapter.RequestSuccess("setBreakpoints", new
                {
                    source = new { name = "Program.cs", path = sourcePath },
                    breakpoints = new[] { new { line = 16 } },
                    lines = new[] { 16 },
                    sourceModified = true,
                }, DapTimeout);
                var pendingBreakpoint = Body(temporarilyUnverified)
                    .GetProperty("breakpoints")[0];
                Require(
                    !pendingBreakpoint.GetProperty("verified").GetBoolean(),
                    "The pre-commit breakpoint unexpectedly bound to stale symbols.");
                var breakpointId = pendingBreakpoint.GetProperty("id").GetInt32();

                var applied = ApplyBroker(
                    pipeName,
                    sessionToken,
                    inactiveUpdate.UpdateModulePath,
                    inactiveUpdate.DebugManifestPath);
                RequireSuccess(applied, expected: true);
                Require(applied.Generation == 1, "The runtime committed the wrong generation.");
                var rebound = adapter.WaitFor(
                    message => IsBreakpointEvent(message, breakpointId),
                    TimeSpan.FromSeconds(10),
                    "Edit-and-Continue breakpoint rebound");
                var reboundBreakpoint = Body(rebound).GetProperty("breakpoint");
                Require(
                    reboundBreakpoint.GetProperty("verified").GetBoolean()
                    && reboundBreakpoint.GetProperty("line").GetInt32() == 16,
                    "The updated breakpoint did not rebind: " + rebound.GetRawText());
                var committed = host.Request(new
                {
                    command = "commit",
                    generation = GetInt32(inactivePrepared, "generation"),
                });
                RequireSuccess(committed, expected: true);
                currentManifest = inactiveUpdate.DebugManifestPath;
                var staleRejected = ApplyBroker(
                    pipeName,
                    sessionToken,
                    inactiveUpdate.UpdateModulePath,
                    currentManifest);
                Require(
                    !staleRejected.Success
                    && staleRejected.Error.Contains("generation", StringComparison.OrdinalIgnoreCase),
                    "The runtime did not reject a stale update: " + staleRejected.Error);

                adapter.RequestSuccess(
                    "continue",
                    new { threadId = 1, singleThread = false },
                    DapTimeout);
                adapter.WaitFor(
                    message => IsStoppedEvent(message, "breakpoint"),
                    DapTimeout,
                    "updated Edit-and-Continue breakpoint");
                var stack = adapter.RequestSuccess(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 10 },
                    DapTimeout);
                var updatedFrame = Body(stack).GetProperty("stackFrames")[0];
                Require(
                    updatedFrame.GetProperty("name").GetString()
                        == "EditAndContinueFixture.Program.GetValue"
                    && updatedFrame.GetProperty("line").GetInt32() == 16,
                    "The updated stop did not use committed symbols: " + stack.GetRawText());
                var scopes = adapter.RequestSuccess(
                    "scopes",
                    new { frameId = updatedFrame.GetProperty("id").GetInt32() },
                    DapTimeout);
                var locals = Body(scopes).GetProperty("scopes")
                    .EnumerateArray()
                    .Single(scope => scope.GetProperty("name").GetString() == "Locals");
                var variablesResponse = adapter.RequestSuccess(
                    "variables",
                    new
                    {
                        variablesReference = locals.GetProperty("variablesReference").GetInt32(),
                    },
                    DapTimeout);
                var updatedLocal = Body(variablesResponse).GetProperty("variables")[0];
                Require(
                    updatedLocal.GetProperty("name").GetString() == "result"
                    && updatedLocal.GetProperty("value").GetString() == "2",
                    "The updated local value was not visible: "
                    + variablesResponse.GetRawText());

                adapter.RequestSuccess(
                    "next",
                    new { threadId = 1, singleThread = false },
                    DapTimeout);
                adapter.WaitFor(
                    message => IsStoppedEvent(message, "step"),
                    DapTimeout,
                    "step after Edit-and-Continue");

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    activeMethodRejected = true,
                    tornUpdateRejected = true,
                    staleUpdateRejected = true,
                    generation = applied.Generation,
                    reboundLine = reboundBreakpoint.GetProperty("line").GetInt32(),
                    updatedLocal,
                    steppedAfterUpdate = true,
                    initialStopReason = Body(initialStop).GetProperty("reason").GetString(),
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            finally
            {
                try
                {
                    adapter.RequestSuccess(
                        "disconnect",
                        new { restart = false, terminateDebuggee = true },
                        TimeSpan.FromSeconds(2));
                }
                catch (Exception exception) when (exception is InvalidOperationException
                    or TimeoutException)
                {
                }
            }
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static void BuildPrerequisites(string repository)
    {
        foreach (var project in new[]
        {
            Path.Combine(repository, "Compiler", "Core", "Compiler.csproj"),
            Path.Combine(
                repository,
                "Debugger",
                "EditAndContinue",
                "FrontEndCSharp.EditAndContinue.csproj"),
            Path.Combine(
                repository,
                "Debugger",
                "Adapter",
                "FrontEndCSharp.DebugAdapter.csproj"),
        })
        {
            BoundedProcess.Run(
                "dotnet",
                repository,
                ProcessTimeout,
                "build",
                project,
                "--configuration",
                "Debug");
        }
    }

    private static EditAndContinueTranslation Translate(
        string compilerPath,
        string repository,
        string projectPath,
        string generatedOutput,
        JsonElement prepared,
        string baselineManifest,
        string directoryName)
    {
        var assemblyPath = GetString(prepared, "assemblyPath");
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath)!;
        var candidateOutput = Path.Combine(assemblyDirectory, directoryName);
        RunCompiler(compilerPath, repository, projectPath, candidateOutput, assemblyPath);
        var candidateManifest = Path.Combine(
            assemblyDirectory,
            "EditAndContinueFixture.frontendcsharp.debug.json");
        var updateManifest = Path.Combine(
            assemblyDirectory,
            directoryName + ".frontendcsharp.debug.json");
        var updatedMethodTokens = GetProperty(prepared, "updatedMethodTokens")
            .EnumerateArray()
            .Select(token => token.GetInt32());
        var output = RunCompiler(
            compilerPath,
            repository,
            "--create-debug-update",
            baselineManifest,
            candidateManifest,
            generatedOutput,
            candidateOutput,
            GetInt32(prepared, "generation").ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            string.Join(',', updatedMethodTokens),
            updateManifest);
        var json = output.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries)[^1];
        return JsonSerializer.Deserialize<EditAndContinueTranslation>(json, JsonOptions)
            ?? throw new InvalidDataException("Compiler returned an empty update translation.");
    }

    private static BrokerResponse ApplyBroker(
        string pipeName,
        string sessionToken,
        string updateModulePath,
        string debugManifestPath)
    {
        using var timeout = new CancellationTokenSource(DapTimeout);
        using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        try
        {
            pipe.ConnectAsync(timeout.Token).GetAwaiter().GetResult();
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using var reader = new StreamReader(pipe, utf8, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(pipe, utf8, 1024, leaveOpen: true)
            {
                AutoFlush = true,
            };
            writer.WriteLine(JsonSerializer.Serialize(new
            {
                sessionToken,
                updateModulePath,
                debugManifestPath,
            }));
            var response = reader.ReadLineAsync(timeout.Token).GetAwaiter().GetResult()
                ?? throw new InvalidDataException("The Edit-and-Continue broker returned no response.");
            return JsonSerializer.Deserialize<BrokerResponse>(response, JsonOptions)
                ?? throw new InvalidDataException("The Edit-and-Continue broker returned empty JSON.");
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("The Edit-and-Continue broker exceeded 20 seconds.");
        }
    }

    private static string RunCompiler(
        string compilerPath,
        string repository,
        params string[] arguments) =>
        BoundedProcess.Run(compilerPath, repository, ProcessTimeout, arguments).StandardOutput;

    private static void CopyFixture(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var segments = relative.Split(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (segments.Any(segment => segment == "bin" || segment == "obj"))
            {
                continue;
            }
            var output = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.Copy(file, output);
        }
    }

    private static JsonElement Body(JsonElement response) => response.GetProperty("body");

    private static bool IsEvent(JsonElement message, string eventName) =>
        message.GetProperty("type").GetString() == "event"
        && message.GetProperty("event").GetString() == eventName;

    private static bool IsStoppedEvent(JsonElement message, string reason) =>
        IsEvent(message, "stopped")
        && Body(message).GetProperty("reason").GetString() == reason;

    private static bool IsBreakpointEvent(JsonElement message, int breakpointId) =>
        IsEvent(message, "breakpoint")
        && Body(message).GetProperty("breakpoint").GetProperty("id").GetInt32()
            == breakpointId;

    private static void RequireFailure(BrokerResponse response, string expectedMessage)
    {
        Require(
            !response.Success
            && response.Error.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase),
            $"Expected broker failure containing '{expectedMessage}', received '{response.Error}'.");
    }

    private static void RequireSuccess(BrokerResponse response, bool expected)
    {
        if (response.Success != expected)
        {
            throw new InvalidDataException(response.Error);
        }
    }

    private static void RequireSuccess(JsonElement response, bool expected)
    {
        var success = GetProperty(response, "success").GetBoolean();
        if (success != expected)
        {
            var error = TryGetProperty(response, "error", out var errorProperty)
                ? errorProperty.GetString()
                : response.GetRawText();
            throw new InvalidDataException(error);
        }
    }

    private static int GetInt32(JsonElement element, string name) =>
        GetProperty(element, name).GetInt32();

    private static string GetString(JsonElement element, string name) =>
        GetProperty(element, name).GetString()
        ?? throw new InvalidDataException($"JSON property '{name}' was null.");

    private static JsonElement GetProperty(JsonElement element, string name)
    {
        if (TryGetProperty(element, name, out var property))
        {
            return property;
        }
        throw new InvalidDataException(
            $"JSON did not contain '{name}' or its Pascal-case equivalent: {element.GetRawText()}");
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement property) =>
        element.TryGetProperty(name, out property)
        || element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out property);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private sealed class EditAndContinueTranslation
    {
        public string UpdateModulePath { get; init; } = string.Empty;
        public string DebugManifestPath { get; init; } = string.Empty;
    }

    private sealed class BrokerResponse
    {
        public bool Success { get; init; }
        public int Generation { get; init; }
        public string Error { get; init; } = string.Empty;
    }
}
