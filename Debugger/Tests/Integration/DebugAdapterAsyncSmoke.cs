using System.Text.Json;

namespace FrontEndCSharp.Debugger.Integration;

internal static class DebugAdapterAsyncSmoke
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public static void Run(string browserPath)
    {
        var repository = RepositoryLayout.Root;
        var fixtureRoot = Path.Combine(
            repository,
            "Debugger",
            "Tests",
            "Fixtures",
            "AsyncPdbFixture");
        var sourcePath = Path.Combine(fixtureRoot, "AsyncFixture.cs");
        var outputRoot = Path.Combine(fixtureRoot, "bin", "Debug", "net8.0");
        var assemblyPath = Path.Combine(outputRoot, "AsyncPdbFixture.dll");
        var manifestPath = Path.Combine(
            outputRoot,
            "AsyncPdbFixture.frontendcsharp.debug.json");
        var generatedOutput = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-Async-Debug-Smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(generatedOutput);
        try
        {
            var sourceLines = File.ReadAllLines(sourcePath);
            var asyncBreakpointLine = LineContaining(sourceLines, "return beforeAwait + 1;");
            var callerCompletionLine = LineContaining(
                sourceLines,
                "return await AddOneAsync(value);") + 1;
            var unhandledThrowLine = LineContaining(
                sourceLines,
                "throw new InvalidOperationException");
            BoundedProcess.Run(
                "dotnet",
                repository,
                TimeSpan.FromMinutes(5),
                "build",
                Path.Combine(repository, "Compiler", "Core", "Compiler.csproj"),
                "--configuration",
                "Debug");
            BoundedProcess.Run(
                "dotnet",
                repository,
                TimeSpan.FromMinutes(5),
                "build",
                Path.Combine(
                    repository,
                    "Debugger",
                    "Adapter",
                    "FrontEndCSharp.DebugAdapter.csproj"),
                "--configuration",
                "Debug");
            BoundedProcess.Run(
                "dotnet",
                repository,
                TimeSpan.FromMinutes(5),
                "build",
                Path.Combine(fixtureRoot, "AsyncPdbFixture.csproj"),
                "--configuration",
                "Debug");
            BoundedProcess.Run(
                Path.Combine(
                    repository,
                    "Compiler",
                    "Core",
                    "bin",
                    "Debug",
                    "net8.0",
                    "Compiler.exe"),
                repository,
                TimeSpan.FromMinutes(5),
                "--debugger-rendezvous-timeout-ms",
                "15000",
                Path.Combine(fixtureRoot, "AsyncPdbFixture.csproj"),
                generatedOutput,
                assemblyPath);

            using var adapter = new DapClient(Path.Combine(
                repository,
                "Debugger",
                "Adapter",
                "bin",
                "Debug",
                "net8.0",
                "FrontEndCSharp.DebugAdapter.exe"));
            try
            {
                adapter.RequestSuccess("initialize", new
                {
                    adapterID = "front-end-csharp",
                    clientID = "async-smoke-test",
                    linesStartAt1 = true,
                    columnsStartAt1 = true,
                    pathFormat = "path",
                }, RequestTimeout);
                adapter.WaitFor(
                    message => IsEvent(message, "initialized"),
                    TimeSpan.FromSeconds(10),
                    "initialization");
                adapter.RequestSuccess("launch", new
                {
                    program = sourcePath,
                    browserPath,
                    browserHeadless = true,
                    generatedOutputPath = generatedOutput,
                    debugManifestPath = manifestPath,
                }, RequestTimeout);
                var breakpoints = adapter.RequestSuccess("setBreakpoints", new
                {
                    source = new { name = Path.GetFileName(sourcePath), path = sourcePath },
                    breakpoints = new[] { new { line = asyncBreakpointLine } },
                    lines = new[] { asyncBreakpointLine },
                    sourceModified = false,
                }, RequestTimeout);
                var breakpoint = Body(breakpoints).GetProperty("breakpoints")[0];
                Require(
                    breakpoint.GetProperty("verified").GetBoolean()
                    && breakpoint.GetProperty("line").GetInt32() == asyncBreakpointLine,
                    "Async breakpoint did not bind: " + breakpoints.GetRawText());
                adapter.RequestSuccess("configurationDone", new { }, RequestTimeout);
                var stopped = adapter.WaitFor(
                    message => IsStoppedEvent(message, "breakpoint"),
                    RequestTimeout,
                    "async breakpoint");
                var stack = adapter.RequestSuccess(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 },
                    RequestTimeout);
                var frames = Body(stack).GetProperty("stackFrames");
                Require(
                    frames.GetArrayLength() >= 3
                    && FrameName(frames[0]) == "AsyncPdbFixture.AsyncFixture.AddOneAsync"
                    && FrameName(frames[1]) == "AsyncPdbFixture.AsyncFixture.Caller"
                    && FrameName(frames[2]) == "AsyncPdbFixture.AsyncFixture.Main"
                    && frames[0].GetProperty("line").GetInt32() == asyncBreakpointLine,
                    "Async source and logical caller mapping failed: " + stack.GetRawText());
                var scopes = adapter.RequestSuccess(
                    "scopes",
                    new { frameId = frames[0].GetProperty("id").GetInt32() },
                    RequestTimeout);
                var scopeItems = Body(scopes).GetProperty("scopes");
                var localsScope = scopeItems.EnumerateArray().SingleOrDefault(
                    scope => scope.GetProperty("name").GetString() == "Locals");
                Require(
                    scopeItems.GetArrayLength() == 1
                    && localsScope.ValueKind != JsonValueKind.Undefined,
                    "Expected one merged managed Locals scope: " + scopes.GetRawText());
                var localsResponse = adapter.RequestSuccess(
                    "variables",
                    new
                    {
                        variablesReference = localsScope.GetProperty("variablesReference").GetInt32(),
                    },
                    RequestTimeout);
                var variables = Body(localsResponse).GetProperty("variables");
                Require(
                    HasVariable(variables, "value", "2")
                    && HasVariable(variables, "beforeAwait", "12"),
                    "Async arguments or hoisted locals were not projected: "
                    + localsResponse.GetRawText());

                adapter.RequestSuccess(
                    "stepOut",
                    new { threadId = 1, singleThread = false },
                    RequestTimeout);
                var stepOutStop = adapter.WaitFor(
                    message => IsStoppedEvent(message, "step"),
                    RequestTimeout,
                    "async step out");
                var stepOutStack = adapter.RequestSuccess(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 },
                    RequestTimeout);
                var stepOutFrame = Body(stepOutStack).GetProperty("stackFrames")[0];
                Require(
                    FrameName(stepOutFrame) == "AsyncPdbFixture.AsyncFixture.Caller"
                    && stepOutFrame.GetProperty("line").GetInt32() == callerCompletionLine,
                    "Async Step Out did not reach the logical caller: "
                    + stepOutStack.GetRawText());
                adapter.RequestSuccess(
                    "setExceptionBreakpoints",
                    new { filters = new[] { "frontendcsharp-unhandled" } },
                    RequestTimeout);
                adapter.RequestSuccess(
                    "continue",
                    new { threadId = 1, singleThread = false },
                    RequestTimeout);
                var unhandledStop = adapter.WaitFor(
                    message => IsStoppedEvent(message, "exception"),
                    RequestTimeout,
                    "async unhandled exception");
                var unhandledInfo = adapter.RequestSuccess(
                    "exceptionInfo",
                    new { threadId = 1 },
                    RequestTimeout);
                var exceptionBody = Body(unhandledInfo);
                Require(
                    exceptionBody.GetProperty("exceptionId").GetString()
                        == "FrontEndCSharp/System.InvalidOperationException"
                    && exceptionBody.GetProperty("breakMode").GetString() == "unhandled",
                    "Unexpected async unhandled exception details: "
                    + unhandledInfo.GetRawText());
                var unhandledStack = adapter.RequestSuccess(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 },
                    RequestTimeout);
                var unhandledFrame = Body(unhandledStack).GetProperty("stackFrames")[0];
                Require(
                    FrameName(unhandledFrame) == "AsyncPdbFixture.AsyncFixture.Main"
                    && unhandledFrame.GetProperty("line").GetInt32() == unhandledThrowLine,
                    "Async unhandled stop lost its throw origin: "
                    + unhandledStack.GetRawText());
                adapter.RequestSuccess(
                    "continue",
                    new { threadId = 1, singleThread = false },
                    RequestTimeout);
                adapter.WaitFor(
                    message => IsEvent(message, "terminated"),
                    RequestTimeout,
                    "termination");
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    breakpoint,
                    frames,
                    locals = variables,
                    stepOutFrame,
                    stepOutReason = Body(stepOutStop).GetProperty("reason").GetString(),
                    unhandledFrame,
                    unhandledReason = Body(unhandledStop).GetProperty("reason").GetString(),
                    stopReason = Body(stopped).GetProperty("reason").GetString(),
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
            Directory.Delete(generatedOutput, recursive: true);
        }
    }

    private static int LineContaining(string[] lines, string text)
    {
        var index = Array.FindIndex(lines, line => line.Contains(text, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidDataException($"Async fixture is missing '{text}'.");
        }
        return index + 1;
    }

    private static JsonElement Body(JsonElement response) => response.GetProperty("body");

    private static string? FrameName(JsonElement frame) =>
        frame.GetProperty("name").GetString();

    private static bool HasVariable(JsonElement variables, string name, string value) =>
        variables.EnumerateArray().Any(variable =>
            variable.GetProperty("name").GetString() == name
            && variable.GetProperty("value").GetString() == value);

    private static bool IsEvent(JsonElement message, string eventName) =>
        message.GetProperty("type").GetString() == "event"
        && message.GetProperty("event").GetString() == eventName;

    private static bool IsStoppedEvent(JsonElement message, string reason) =>
        IsEvent(message, "stopped")
        && Body(message).GetProperty("reason").GetString() == reason;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
