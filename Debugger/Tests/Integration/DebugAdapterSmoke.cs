using System.Text.Json;

namespace FrontEndCSharp.Debugger.Integration;

internal static class DebugAdapterSmoke
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(5);

    public static void Run(string browserPath, IReadOnlyList<string> arguments)
    {
        RunAsync(browserPath, DebugAdapterSmokeOptions.Parse(arguments))
            .GetAwaiter()
            .GetResult();
    }

    private static async Task RunAsync(
        string browserPath,
        DebugAdapterSmokeOptions options)
    {
        var repository = RepositoryLayout.Root;
        var testFecsx = options.Fecsx
            || options.FecsxNamed
            || options.FecsxEvent;
        var fixtureRoot = testFecsx
            ? Path.Combine(repository, "Demo", "FrontEndCSharp.ReactDemo")
            : Path.Combine(
                repository,
                "Compiler",
                "Compiler.Tests",
                "Fixtures",
                "PortablePdbFixture");
        var projectPath = Path.Combine(
            fixtureRoot,
            testFecsx
                ? "FrontEndCSharp.ReactDemo.csproj"
                : "PortablePdbFixture.csproj");
        var sourcePath = testFecsx
            ? Path.Combine(
                fixtureRoot,
                "Components",
                options.FecsxNamed
                    ? "App.fecsx"
                    : options.FecsxEvent
                        ? "FocusCard.fecsx"
                        : "TaskCard.fecsx")
            : Path.Combine(fixtureRoot, "PortableFixture.cs");
        var assemblyName = testFecsx
            ? "FrontEndCSharp.ReactDemo"
            : "PortablePdbFixture";
        var outputRoot = Path.Combine(fixtureRoot, "bin", "Debug", "net8.0");
        var assemblyPath = Path.Combine(outputRoot, assemblyName + ".dll");
        var manifestPath = Path.Combine(
            outputRoot,
            assemblyName + ".frontendcsharp.debug.json");
        var generatedOutput = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-Debug-Smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(generatedOutput);
        OwnedCdpBrowser? externalBrowser = null;
        DebuggerWebHost? webHost = null;
        try
        {
            BuildPrerequisites(repository, projectPath);
            var compilerPath = Path.Combine(
                repository,
                "Compiler",
                "Core",
                "bin",
                "Debug",
                "net8.0",
                "Compiler.exe");
            BoundedProcess.Run(
                compilerPath,
                repository,
                ProcessTimeout,
                "--debugger-rendezvous-timeout-ms",
                "10000",
                projectPath,
                generatedOutput,
                assemblyPath);
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var expectedBuildId = manifest.RootElement.GetProperty("buildId").GetString()
                ?? throw new InvalidDataException("The debug manifest has no build identity.");

            var useExternalBrowser = options.ExternalBrowser || options.CrashBrowser;
            var useWebHost = options.WebHost || testFecsx;
            if ((options.ForeignPause || options.AttachOnly)
                && (!useWebHost || !useExternalBrowser))
            {
                throw new ArgumentException(
                    "--foreign-pause and --attach-only require --web-host and --external-browser.");
            }
            if (useExternalBrowser)
            {
                externalBrowser = await OwnedCdpBrowser.LaunchAsync(browserPath);
            }
            if (useWebHost)
            {
                webHost = new DebuggerWebHost(
                    generatedOutput,
                    options.ForeignPause ? 2_000 : options.AttachOnly ? 0 : 750,
                    testFecsx
                        ? "<div id=\"app\" data-fecs-smoke=\"true\"></div>"
                        : string.Empty,
                    options.FecsxEvent
                        ? """
                            let eventTarget = null;
                            for (let attempt = 0; attempt < 100 && eventTarget === null; attempt++) {
                                eventTarget = document.querySelector(".primary-action");
                                if (eventTarget === null) await new Promise(resolve => setTimeout(resolve, 25));
                            }
                            if (eventTarget === null) throw new Error("The FECSX event target was not rendered.");
                            eventTarget.click();
                            """
                        : string.Empty);
            }

            var launch = new Dictionary<string, object?>
            {
                ["program"] = sourcePath,
                ["generatedOutputPath"] = generatedOutput,
                ["debugManifestPath"] = manifestPath,
            };
            if (externalBrowser == null)
            {
                launch["browserPath"] = browserPath;
                launch["browserHeadless"] = !options.VisibleBrowser;
            }
            else
            {
                launch["browserWebSocketUrl"] = externalBrowser.Endpoint.AbsoluteUri;
            }
            if (webHost != null)
            {
                launch["browserUrl"] = webHost.Url.AbsoluteUri;
            }
            if (options.AttachOnly)
            {
                launch["browserAttachOnly"] = true;
                launch["browserTargetId"] = await PrepareExistingPageTarget(
                    externalBrowser!,
                    webHost!.Url,
                    expectedBuildId);
            }

            var adapterPath = Path.Combine(
                repository,
                "Debugger",
                "Adapter",
                "bin",
                "Debug",
                "net8.0",
                "FrontEndCSharp.DebugAdapter.exe");
            using var adapter = new DapClient(adapterPath);
            try
            {
                var initialize = adapter.RequestSuccess("initialize", new
                {
                    adapterID = "front-end-csharp",
                    clientID = "smoke-test",
                    linesStartAt1 = true,
                    columnsStartAt1 = true,
                    pathFormat = "path",
                    supportsStartDebuggingRequest = true,
                }, RequestTimeout);
                var capabilities = Body(initialize);
                Require(
                    capabilities.GetProperty("supportsExceptionInfoRequest").GetBoolean()
                    && capabilities.GetProperty("supportsEvaluateForHovers").GetBoolean()
                    && capabilities.GetProperty("supportsSetVariable").GetBoolean(),
                    "The adapter did not advertise all managed debugger capabilities: "
                    + initialize.GetRawText());
                adapter.WaitFor(
                    message => IsEvent(message, "initialized"),
                    RequestTimeout,
                    "initialized event");
                adapter.RequestSuccess("launch", launch, RequestTimeout);

                JsonElement breakpointResponse = default;
                if ((!useWebHost || options.AttachOnly) && !options.ForeignPause)
                {
                    breakpointResponse = SetMainBreakpoint(
                        adapter,
                        sourcePath,
                        options,
                        testFecsx);
                }
                var unrelatedBreakpoints = adapter.RequestSuccess("setBreakpoints", new
                {
                    source = new
                    {
                        path = Path.Combine(
                            repository,
                            "Compiler",
                            "Core",
                            "Read",
                            "MethodDecoder.cs"),
                    },
                    breakpoints = new[] { new { line = 1 } },
                    lines = new[] { 1 },
                    sourceModified = false,
                }, RequestTimeout);
                Require(
                    !Body(unrelatedBreakpoints)
                        .GetProperty("breakpoints")[0]
                        .GetProperty("verified")
                        .GetBoolean(),
                    "An unrelated breakpoint was unexpectedly verified: "
                    + unrelatedBreakpoints.GetRawText());
                adapter.RequestSuccess("setExceptionBreakpoints", new
                {
                    filters = options.ForeignPause
                        ? Array.Empty<string>()
                        : new[] { "frontendcsharp-thrown" },
                }, RequestTimeout);
                adapter.RequestSuccess("configurationDone", new { }, RequestTimeout);
                if (useWebHost && !options.ForeignPause && !options.AttachOnly)
                {
                    breakpointResponse = SetMainBreakpoint(
                        adapter,
                        sourcePath,
                        options,
                        testFecsx);
                }

                if (options.ForeignPause)
                {
                    await ProveForeignPauseIsIgnored(
                        adapter,
                        externalBrowser!,
                        webHost!.Url);
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        foreignPauseIgnored = true,
                        foreignPauseRemainedSuspended = true,
                    }, new JsonSerializerOptions { WriteIndented = true }));
                    return;
                }

                var stopped = adapter.WaitFor(
                    message => IsEvent(message, "stopped"),
                    RequestTimeout,
                    "stopped event");
                Require(
                    Body(stopped).GetProperty("reason").GetString() == "breakpoint"
                    && Body(stopped).GetProperty("threadId").GetInt32() == 1,
                    "The adapter reported an unexpected stop: " + stopped.GetRawText());
                if (testFecsx)
                {
                    RunFecsxScenario(
                        adapter,
                        fixtureRoot,
                        sourcePath,
                        options,
                        breakpointResponse,
                        webHost!);
                }
                else if (useWebHost)
                {
                    RunWebHostScenario(adapter, breakpointResponse, webHost!);
                }
                else if (options.CrashBrowser)
                {
                    await externalBrowser!.KillAsync();
                    adapter.WaitFor(
                        message => IsEvent(message, "terminated"),
                        RequestTimeout,
                        "browser crash cleanup");
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        browserCrashCleanup = true,
                    }, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    RunPortableScenario(
                        adapter,
                        sourcePath,
                        breakpointResponse,
                        stopped);
                }
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
            if (externalBrowser != null
                && !options.CrashBrowser
                && externalBrowser.HasExited)
            {
                throw new InvalidDataException(
                    "The adapter terminated a browser it did not own.");
            }
        }
        finally
        {
            webHost?.Dispose();
            if (externalBrowser != null)
            {
                await externalBrowser.DisposeAsync();
            }
            Directory.Delete(generatedOutput, recursive: true);
        }
    }

    private static void RunPortableScenario(
        DapClient adapter,
        string sourcePath,
        JsonElement breakpointResponse,
        JsonElement stopped)
    {
        var nextLine = LineContaining(sourcePath, "return result;");
        var callerLine = LineContaining(sourcePath, "BusyLoop(100_000);");
        var stack = adapter.RequestSuccess(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 },
            RequestTimeout);
        var frames = Body(stack).GetProperty("stackFrames");
        Require(
            frames[0].GetProperty("source").GetProperty("path").GetString() == sourcePath
            && frames[1].GetProperty("name").GetString()
                == "PortablePdbFixture.PortableFixture.Main",
            "The managed stack was not preserved: " + stack.GetRawText());
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
        var localsReference = localsScope.GetProperty("variablesReference").GetInt32();
        var localsResponse = adapter.RequestSuccess(
            "variables",
            new { variablesReference = localsReference },
            RequestTimeout);
        var locals = Body(localsResponse).GetProperty("variables");
        Require(
            HasVariable(locals, "value", "1")
            && locals.EnumerateArray().Any(
                variable => variable.GetProperty("name").GetString() == "result"),
            "Unexpected locals: " + localsResponse.GetRawText());

        var invalidArgument = adapter.RequestFailure("setVariable", new
        {
            variablesReference = localsReference,
            name = "value",
            value = "not-an-int",
        }, RequestTimeout);
        Require(
            ResponseMessage(invalidArgument).Contains(
                "valid System.Int32",
                StringComparison.Ordinal),
            "Invalid argument editing did not report its conversion failure: "
            + invalidArgument.GetRawText());
        var setArgument = adapter.RequestSuccess("setVariable", new
        {
            variablesReference = localsReference,
            name = "value",
            value = "5",
        }, RequestTimeout);
        Require(
            Body(setArgument).GetProperty("value").GetString() == "5"
            && Body(setArgument).GetProperty("type").GetString() == "System.Int32",
            "Managed argument editing returned an unexpected value: "
            + setArgument.GetRawText());
        var evaluation = adapter.RequestSuccess("evaluate", new
        {
            context = "hover",
            expression = "value",
            frameId = frames[0].GetProperty("id").GetInt32(),
        }, RequestTimeout);
        Require(
            Body(evaluation).GetProperty("result").GetString() == "5"
            && Body(evaluation).GetProperty("type").GetString() == "System.Int32",
            "Hover evaluation returned an unexpected value: " + evaluation.GetRawText());

        var nextResponse = adapter.RequestSuccess(
            "next",
            new { threadId = 1, singleThread = false },
            RequestTimeout);
        AssertNoUnexpectedContinued(adapter, "next");
        var stepped = adapter.WaitFor(
            message => IsStoppedEvent(message, "step"),
            RequestTimeout,
            "next");
        AssertStopFollowsResponse("next", nextResponse, stepped);
        var steppedStack = adapter.RequestSuccess(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 },
            RequestTimeout);
        var steppedFrame = Body(steppedStack).GetProperty("stackFrames")[0];
        Require(
            steppedFrame.GetProperty("line").GetInt32() == nextLine,
            "Step did not reach the next source line: " + steppedStack.GetRawText());
        var steppedScopes = adapter.RequestSuccess(
            "scopes",
            new { frameId = steppedFrame.GetProperty("id").GetInt32() },
            RequestTimeout);
        var steppedLocalsReference = Body(steppedScopes).GetProperty("scopes")
            .EnumerateArray()
            .Single(scope => scope.GetProperty("name").GetString() == "Locals")
            .GetProperty("variablesReference")
            .GetInt32();
        var steppedVariables = adapter.RequestSuccess(
            "variables",
            new { variablesReference = steppedLocalsReference },
            RequestTimeout);
        Require(
            HasVariable(Body(steppedVariables).GetProperty("variables"), "result", "6"),
            "Managed argument editing did not affect execution: "
            + steppedVariables.GetRawText());
        var staleEdit = adapter.RequestFailure("setVariable", new
        {
            variablesReference = localsReference,
            name = "value",
            value = "7",
        }, RequestTimeout);
        var staleMessage = ResponseMessage(staleEdit);
        Require(
            staleMessage.Contains("stale", StringComparison.OrdinalIgnoreCase)
            || staleMessage.Contains("no longer", StringComparison.OrdinalIgnoreCase),
            "Stale managed editing did not report a useful error: "
            + staleEdit.GetRawText());
        var setLocal = adapter.RequestSuccess("setVariable", new
        {
            variablesReference = steppedLocalsReference,
            name = "result",
            value = "20",
        }, RequestTimeout);
        Require(
            Body(setLocal).GetProperty("value").GetString() == "20",
            "Managed local editing returned an unexpected value: " + setLocal.GetRawText());

        var stepOutResponse = adapter.RequestSuccess(
            "stepOut",
            new { threadId = 1, singleThread = false },
            RequestTimeout);
        AssertNoUnexpectedContinued(adapter, "stepOut");
        var stepOutStop = adapter.WaitFor(
            message => IsStoppedEvent(message, "step"),
            RequestTimeout,
            "step out");
        AssertStopFollowsResponse("stepOut", stepOutResponse, stepOutStop);
        var steppedOutStack = adapter.RequestSuccess(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 },
            RequestTimeout);
        var stepOutFrame = Body(steppedOutStack).GetProperty("stackFrames")[0];
        Require(
            stepOutFrame.GetProperty("name").GetString()
                == "PortablePdbFixture.PortableFixture.Main"
            && stepOutFrame.GetProperty("line").GetInt32() == callerLine,
            "Step out did not reach the caller: " + steppedOutStack.GetRawText());

        var stepInResponse = adapter.RequestSuccess(
            "stepIn",
            new { threadId = 1, singleThread = false },
            RequestTimeout);
        AssertNoUnexpectedContinued(adapter, "stepIn");
        var stepInStop = adapter.WaitFor(
            message => IsStoppedEvent(message, "step"),
            RequestTimeout,
            "step into call");
        AssertStopFollowsResponse("stepIn", stepInResponse, stepInStop);
        var steppedIntoStack = adapter.RequestSuccess(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 },
            RequestTimeout);
        var stepIntoFrame = Body(steppedIntoStack).GetProperty("stackFrames")[0];
        Require(
            stepIntoFrame.GetProperty("name").GetString()
                == "PortablePdbFixture.PortableFixture.BusyLoop",
            "Step in did not enter the call: " + steppedIntoStack.GetRawText());

        adapter.RequestSuccess(
            "continue",
            new { threadId = 1, singleThread = false },
            RequestTimeout);
        AssertNoUnexpectedContinued(adapter, "continue");
        adapter.RequestSuccess("pause", new { threadId = 1 }, RequestTimeout);
        adapter.WaitFor(
            message => IsStoppedEvent(message, "pause"),
            RequestTimeout,
            "pause");
        var pausedStack = adapter.RequestSuccess(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 },
            RequestTimeout);
        var pauseFrames = Body(pausedStack).GetProperty("stackFrames");
        Require(
            pauseFrames.GetArrayLength() > 0,
            "Pause did not reach a managed boundary: " + pausedStack.GetRawText());

        adapter.RequestSuccess(
            "continue",
            new { threadId = 1, singleThread = false },
            RequestTimeout);
        var thrownStop = adapter.WaitFor(
            message => IsStoppedEvent(message, "exception"),
            RequestTimeout,
            "thrown exception");
        Require(
            Body(thrownStop).GetProperty("description").GetString()!
                .Contains("(thrown)", StringComparison.Ordinal),
            "Expected a thrown managed exception: " + thrownStop.GetRawText());
        var thrownInfo = adapter.RequestSuccess(
            "exceptionInfo",
            new { threadId = 1 },
            RequestTimeout);
        RequireExceptionInfo(thrownInfo, "always");
        var thrownStack = adapter.RequestSuccess(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 },
            RequestTimeout);
        var thrownFrame = Body(thrownStack).GetProperty("stackFrames")[0];
        Require(
            thrownFrame.GetProperty("name").GetString()
                == "PortablePdbFixture.PortableFixture.ThrowManagedException",
            "Thrown stop lost its origin stack: " + thrownStack.GetRawText());

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
            "unhandled exception");
        Require(
            Body(unhandledStop).GetProperty("description").GetString()!
                .Contains("(unhandled)", StringComparison.Ordinal),
            "Expected an unhandled managed exception: " + unhandledStop.GetRawText());
        var unhandledInfo = adapter.RequestSuccess(
            "exceptionInfo",
            new { threadId = 1 },
            RequestTimeout);
        RequireExceptionInfo(unhandledInfo, "unhandled");
        var unhandledStack = adapter.RequestSuccess(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 },
            RequestTimeout);
        var unhandledFrame = Body(unhandledStack).GetProperty("stackFrames")[0];
        Require(
            unhandledFrame.GetProperty("name").GetString()
                == "PortablePdbFixture.PortableFixture.ThrowManagedException",
            "Unhandled stop lost its origin stack: " + unhandledStack.GetRawText());
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
            breakpoint = Body(breakpointResponse).GetProperty("breakpoints")[0],
            stackFrame = frames[0],
            locals,
            stepFrame = steppedFrame,
            stepOutFrame,
            stepIntoCallFrame = stepIntoFrame,
            pauseFrame = pauseFrames[0],
            thrownFrame,
            unhandledFrame,
            stopReason = Body(stopped).GetProperty("reason").GetString(),
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void RunFecsxScenario(
        DapClient adapter,
        string fixtureRoot,
        string sourcePath,
        DebugAdapterSmokeOptions options,
        JsonElement breakpointResponse,
        DebuggerWebHost webHost)
    {
        var stack = adapter.RequestSuccess(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 },
            RequestTimeout);
        var frame = Body(stack).GetProperty("stackFrames")[0];
        var expectedLine = options.FecsxNamed
            ? 39
            : options.FecsxEvent
                ? LineContaining(sourcePath, "<p className=\"focus-copy\">")
                : 5;
        var frameName = frame.GetProperty("name").GetString() ?? string.Empty;
        var correctFrame = options.FecsxNamed
            ? frameName == "FrontEndCSharp.ReactDemo.App+Footer.Render"
            : options.FecsxEvent
                ? frameName.Contains("FrontEndCSharp.ReactDemo.FocusCard", StringComparison.Ordinal)
                    && frameName.Contains("<RenderDefault>", StringComparison.Ordinal)
                : frameName == "FrontEndCSharp.ReactDemo.TaskCard.RenderDefault";
        Require(
            frame.GetProperty("source").GetProperty("path").GetString() == sourcePath
            && frame.GetProperty("line").GetInt32() == expectedLine
            && correctFrame,
            "The FECSX stack frame did not map to authored source: " + stack.GetRawText());

        var stepResponse = adapter.RequestSuccess(
            "stepIn",
            new { threadId = 1, singleThread = false },
            RequestTimeout);
        AssertNoUnexpectedContinued(adapter, "stepIn");
        var stepped = adapter.WaitFor(
            message => IsStoppedEvent(message, "step"),
            RequestTimeout,
            "FECSX step in");
        AssertStopFollowsResponse("stepIn", stepResponse, stepped);
        var steppedStack = adapter.RequestSuccess(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 },
            RequestTimeout);
        var steppedFrame = Body(steppedStack).GetProperty("stackFrames")[0];
        var steppedPath = steppedFrame.GetProperty("source").GetProperty("path").GetString();
        var generatedRoot = Path.GetFullPath(
            Path.Combine(fixtureRoot, "obj", "generated"));
        Require(
            !string.IsNullOrEmpty(steppedPath)
            && !Path.GetFullPath(steppedPath).StartsWith(
                generatedRoot,
                StringComparison.OrdinalIgnoreCase),
            "FECSX step entered generated infrastructure: " + steppedStack.GetRawText());
        adapter.RequestSuccess("setBreakpoints", new
        {
            source = new { name = Path.GetFileName(sourcePath), path = sourcePath },
            breakpoints = Array.Empty<object>(),
            lines = Array.Empty<int>(),
            sourceModified = false,
        }, RequestTimeout);
        adapter.RequestSuccess(
            "setExceptionBreakpoints",
            new { filters = Array.Empty<string>() },
            RequestTimeout);
        adapter.RequestSuccess(
            "continue",
            new { threadId = 1, singleThread = false },
            RequestTimeout);
        WaitForWebHost(webHost, adapter, "FECSX program completion");
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            fecsxBreakpoint = Body(breakpointResponse).GetProperty("breakpoints")[0],
            fecsxStackFrame = frame,
            fecsxStepFrame = steppedFrame,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void RunWebHostScenario(
        DapClient adapter,
        JsonElement breakpointResponse,
        DebuggerWebHost webHost)
    {
        adapter.RequestSuccess(
            "setExceptionBreakpoints",
            new { filters = Array.Empty<string>() },
            RequestTimeout);
        adapter.RequestSuccess(
            "continue",
            new { threadId = 1, singleThread = false },
            RequestTimeout);
        WaitForWebHost(webHost, adapter, "web-host program completion");
        Thread.Sleep(250);
        Require(
            !adapter.Messages.Any(message => IsEvent(message, "terminated")),
            "The web-host debug session terminated when the managed entry point completed.");
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            webHostBreakpoint = Body(breakpointResponse).GetProperty("breakpoints")[0],
            sessionRemainedActive = true,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WaitForWebHost(
        DebuggerWebHost webHost,
        DapClient adapter,
        string phase)
    {
        try
        {
            webHost.Completed.WaitAsync(RequestTimeout).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Timed out during {phase}. queued: "
                + JsonSerializer.Serialize(adapter.Messages)
                + " stderr: " + adapter.StandardError);
        }
    }

    private static JsonElement SetMainBreakpoint(
        DapClient adapter,
        string sourcePath,
        DebugAdapterSmokeOptions options,
        bool testFecsx)
    {
        var line = options.FecsxNamed
            ? 39
            : options.FecsxEvent
                ? LineContaining(sourcePath, "<p className=\"focus-copy\">")
                : testFecsx
                    ? 5
                    : LineContaining(sourcePath, "var result = value + 1;");
        var response = adapter.RequestSuccess("setBreakpoints", new
        {
            source = new { name = Path.GetFileName(sourcePath), path = sourcePath },
            breakpoints = new[] { new { line } },
            lines = new[] { line },
            sourceModified = false,
        }, RequestTimeout);
        var breakpoint = Body(response).GetProperty("breakpoints")[0];
        Require(
            breakpoint.GetProperty("verified").GetBoolean()
            && breakpoint.GetProperty("line").GetInt32() == line,
            "The source breakpoint did not bind: " + response.GetRawText());
        return response;
    }

    private static async Task<string> PrepareExistingPageTarget(
        OwnedCdpBrowser browser,
        Uri pageUrl,
        string buildId)
    {
        var cdp = browser.Client;
        var targets = await cdp.SendAsync("Target.getTargets");
        var page = targets.GetProperty("targetInfos")
            .EnumerateArray()
            .FirstOrDefault(target => target.GetProperty("type").GetString() == "page");
        if (page.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidDataException(
                "The external browser did not expose an initial page target.");
        }
        var targetId = page.GetProperty("targetId").GetString()
            ?? throw new InvalidDataException("The browser page has no target identifier.");
        var attachment = await cdp.SendAsync(
            "Target.attachToTarget",
            new { flatten = true, targetId });
        var sessionId = attachment.GetProperty("sessionId").GetString()
            ?? throw new InvalidDataException("Chromium did not return a session identifier.");
        await cdp.SendAsync("Page.enable", sessionId: sessionId);
        var navigated = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        cdp.EventReceived = message =>
        {
            if (message.Method == "Page.frameNavigated"
                && message.SessionId == sessionId
                && !message.Parameters.GetProperty("frame").TryGetProperty("parentId", out _)
                && message.Parameters.GetProperty("frame").GetProperty("url").GetString()
                    == pageUrl.AbsoluteUri)
            {
                navigated.TrySetResult(true);
            }
        };
        await cdp.SendAsync("Page.navigate", new { url = pageUrl.AbsoluteUri }, sessionId);
        await navigated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cdp.EventReceived = null;
        string? observedBuildId = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline && observedBuildId == null)
        {
            var evaluation = await cdp.SendAsync("Runtime.evaluate", new
            {
                expression = "globalThis.__FrontEndCSharpBuildIdentity?.buildId ?? null",
                returnByValue = true,
            }, sessionId);
            var value = evaluation.GetProperty("result").GetProperty("value");
            observedBuildId = value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
            if (observedBuildId == null)
            {
                await Task.Delay(25);
            }
        }
        Require(
            observedBuildId == buildId,
            $"Existing page published build '{observedBuildId}', expected '{buildId}'.");
        await cdp.SendAsync("Target.detachFromTarget", new { sessionId });
        return targetId;
    }

    private static async Task ProveForeignPauseIsIgnored(
        DapClient adapter,
        OwnedCdpBrowser browser,
        Uri pageUrl)
    {
        adapter.WaitFor(
            message => IsEvent(message, "output")
                && (Body(message).GetProperty("output").GetString()!
                    .Contains("Attached to existing Chromium target", StringComparison.Ordinal)
                    || Body(message).GetProperty("output").GetString()!
                    .Contains("Navigated Chromium", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(10),
            "adapter browser attachment");
        await using var observer = new BrowserCdpClient();
        await observer.ConnectAsync(browser.Endpoint);
        string? targetId = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline && targetId == null)
        {
            var targets = await observer.SendAsync("Target.getTargets");
            targetId = targets.GetProperty("targetInfos")
                .EnumerateArray()
                .Where(target => target.GetProperty("type").GetString() == "page")
                .Where(target => target.GetProperty("url").GetString() == pageUrl.AbsoluteUri)
                .Select(target => target.GetProperty("targetId").GetString())
                .FirstOrDefault();
            if (targetId == null)
            {
                await Task.Delay(25);
            }
        }
        if (targetId == null)
        {
            throw new InvalidDataException(
                $"Unable to find the adapter page target '{pageUrl}'.");
        }
        var attachment = await observer.SendAsync(
            "Target.attachToTarget",
            new { flatten = true, targetId });
        var sessionId = attachment.GetProperty("sessionId").GetString()
            ?? throw new InvalidDataException("The observer received no session identifier.");
        await observer.SendAsync("Runtime.enable", sessionId: sessionId);
        await observer.SendAsync("Debugger.enable", sessionId: sessionId);
        var paused = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        observer.EventReceived = message =>
        {
            if (message.Method == "Debugger.paused" && message.SessionId == sessionId)
            {
                paused.TrySetResult(true);
            }
        };
        await observer.SendAsync("Debugger.pause", sessionId: sessionId);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);
        Require(
            !adapter.Messages.Any(message => IsEvent(message, "stopped")),
            "FrontEndCSharp reported a foreign JavaScript pause as managed.");
        var foreignContinue = adapter.RequestFailure(
            "continue",
            new { threadId = 1, singleThread = false },
            RequestTimeout);
        Require(
            ResponseMessage(foreignContinue).Contains(
                "owned by another debugger",
                StringComparison.Ordinal),
            "FrontEndCSharp did not reject execution control for a foreign pause: "
            + foreignContinue.GetRawText());
        await observer.SendAsync("Debugger.resume", sessionId: sessionId);
        await observer.SendAsync("Target.detachFromTarget", new { sessionId });
    }

    private static void BuildPrerequisites(string repository, string fixtureProject)
    {
        foreach (var project in new[]
        {
            Path.Combine(repository, "Compiler", "Core", "Compiler.csproj"),
            Path.Combine(
                repository,
                "Debugger",
                "Adapter",
                "FrontEndCSharp.DebugAdapter.csproj"),
            fixtureProject,
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

    private static void RequireExceptionInfo(JsonElement response, string breakMode)
    {
        var body = Body(response);
        Require(
            body.GetProperty("exceptionId").GetString()
                == "FrontEndCSharp/System.InvalidOperationException"
            && body.GetProperty("breakMode").GetString() == breakMode
            && body.GetProperty("description").GetString()!
                .Contains("System.InvalidOperationException", StringComparison.Ordinal),
            "Unexpected managed exception details: " + response.GetRawText());
    }

    private static int LineContaining(string sourcePath, string text)
    {
        var lines = File.ReadAllLines(sourcePath);
        var index = Array.FindIndex(
            lines,
            line => line.Contains(text, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidDataException(
                $"Debugger fixture '{sourcePath}' does not contain '{text}'.");
        }
        return index + 1;
    }

    private static void AssertNoUnexpectedContinued(DapClient adapter, string command)
    {
        var continued = adapter.Messages.FirstOrDefault(
            message => IsEvent(message, "continued"));
        if (continued.ValueKind != JsonValueKind.Undefined)
        {
            throw new InvalidDataException(
                $"{command} emitted an unexpected continued event: {continued.GetRawText()}");
        }
    }

    private static void AssertStopFollowsResponse(
        string command,
        JsonElement response,
        JsonElement stopped)
    {
        Require(
            stopped.GetProperty("seq").GetInt32()
                > response.GetProperty("seq").GetInt32(),
            $"{command} reported its stop before its response.");
    }

    private static bool HasVariable(
        JsonElement variables,
        string name,
        string value) =>
        variables.EnumerateArray().Any(variable =>
            variable.GetProperty("name").GetString() == name
            && variable.GetProperty("value").GetString() == value);

    private static string ResponseMessage(JsonElement response) =>
        response.TryGetProperty("message", out var message)
            ? message.GetString() ?? string.Empty
            : string.Empty;

    private static JsonElement Body(JsonElement response) => response.GetProperty("body");

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

    private sealed class DebugAdapterSmokeOptions
    {
        public bool Fecsx { get; private init; }
        public bool FecsxNamed { get; private init; }
        public bool FecsxEvent { get; private init; }
        public bool CrashBrowser { get; private init; }
        public bool ExternalBrowser { get; private init; }
        public bool VisibleBrowser { get; private init; }
        public bool WebHost { get; private init; }
        public bool ForeignPause { get; private init; }
        public bool AttachOnly { get; private init; }

        public static DebugAdapterSmokeOptions Parse(IReadOnlyList<string> arguments)
        {
            var values = arguments.ToHashSet(StringComparer.Ordinal);
            var supported = new HashSet<string>(StringComparer.Ordinal)
            {
                "--fecsx",
                "--fecsx-named",
                "--fecsx-event",
                "--crash-browser",
                "--external-browser",
                "--visible-browser",
                "--web-host",
                "--foreign-pause",
                "--attach-only",
            };
            var unknown = values.FirstOrDefault(value => !supported.Contains(value));
            if (unknown != null)
            {
                throw new ArgumentException($"Unknown debug-adapter smoke option '{unknown}'.");
            }
            return new DebugAdapterSmokeOptions
            {
                Fecsx = values.Contains("--fecsx"),
                FecsxNamed = values.Contains("--fecsx-named"),
                FecsxEvent = values.Contains("--fecsx-event"),
                CrashBrowser = values.Contains("--crash-browser"),
                ExternalBrowser = values.Contains("--external-browser"),
                VisibleBrowser = values.Contains("--visible-browser"),
                WebHost = values.Contains("--web-host"),
                ForeignPause = values.Contains("--foreign-pause"),
                AttachOnly = values.Contains("--attach-only"),
            };
        }
    }
}
