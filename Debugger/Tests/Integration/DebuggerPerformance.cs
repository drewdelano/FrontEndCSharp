using System.Text.Json;

namespace FrontEndCSharp.Debugger.Integration;

internal sealed class BenchmarkMeasurement
{
    public double Elapsed { get; init; }
    public int Checksum { get; init; }
}

internal static class DebuggerPerformance
{
    private const string BenchmarkSource = """
        const managedDebuggerState = { enabled: false, stopIndex: -1 };
        function pauseAtManagedInstruction(opts) {
            if (opts.instructionIndex === managedDebuggerState.stopIndex) debugger;
        }
        globalThis.runNoDebugBenchmark = count => {
            let checksum = 0;
            const started = performance.now();
            for (let index = 0; index < count; index++) {
                const opts = { instructionIndex: index };
                checksum += executeInstruction(opts);
            }
            return { elapsed: performance.now() - started, checksum };
        };
        globalThis.runInactiveHookBenchmark = count => {
            let checksum = 0;
            const started = performance.now();
            for (let index = 0; index < count; index++) {
                const opts = { instructionIndex: index };
                if (managedDebuggerState.enabled) pauseAtManagedInstruction(opts);
                checksum += executeInstruction(opts);
            }
            return { elapsed: performance.now() - started, checksum };
        };
        globalThis.runInlineBreakpointBenchmark = (count, stopIndex) => {
            managedDebuggerState.enabled = true;
            managedDebuggerState.stopIndex = stopIndex;
            let checksum = 0;
            const started = performance.now();
            for (let index = 0; index < count; index++) {
                const opts = { instructionIndex: index };
                if (managedDebuggerState.enabled) pauseAtManagedInstruction(opts);
                checksum += executeInstruction(opts);
            }
            managedDebuggerState.enabled = false;
            return { elapsed: performance.now() - started, checksum };
        };
        globalThis.runConditionalBreakpointBenchmark = count => {
            let checksum = 0;
            const started = performance.now();
            for (let index = 0; index < count; index++) {
                const opts = { instructionIndex: index };
                checksum += executeInstruction(opts); // CDP_CONDITIONAL_BREAKPOINT_SITE
            }
            return { elapsed: performance.now() - started, checksum };
        };
        globalThis.runSteppingBenchmark = count => {
            let checksum = 0;
            const started = performance.now();
            for (let index = 0; index < count; index++) {
                const opts = { instructionIndex: index };
                debugger;
                checksum += executeInstruction(opts);
            }
            return { elapsed: performance.now() - started, checksum };
        };
        function executeInstruction(opts) {
            return opts.instructionIndex & 1;
        }
        //# sourceURL=front-end-csharp-debugger-benchmark.js
        """;

    public static void Run(string browserPath)
    {
        RunAsync(browserPath).GetAwaiter().GetResult();
    }

    private static async Task RunAsync(string browserPath)
    {
        browserPath = Path.GetFullPath(browserPath);
        if (!File.Exists(browserPath))
        {
            throw new FileNotFoundException("The Chromium browser does not exist.", browserPath);
        }
        await using var browser = await OwnedCdpBrowser.LaunchAsync(browserPath);
        var cdp = browser.Client;
        var version = await cdp.SendAsync("Browser.getVersion");
        var target = await cdp.SendAsync("Target.createTarget", new { url = "about:blank" });
        var attachment = await cdp.SendAsync(
            "Target.attachToTarget",
            new { flatten = true, targetId = target.GetProperty("targetId").GetString() });
        var sessionId = attachment.GetProperty("sessionId").GetString()
            ?? throw new InvalidDataException("Chromium did not return a target session identifier.");
        await cdp.SendAsync("Runtime.enable", sessionId: sessionId);
        await cdp.SendAsync(
            "Runtime.evaluate",
            new { expression = BenchmarkSource },
            sessionId);

        const int baselineInstructionCount = 2_000_000;
        const int breakpointInstructionCount = 5_000;
        await EvaluateBenchmark(cdp, sessionId, "runNoDebugBenchmark(100000)", 100_000);
        await EvaluateBenchmark(cdp, sessionId, "runInactiveHookBenchmark(100000)", 100_000);
        var noDebug = await EvaluateBenchmark(
            cdp,
            sessionId,
            $"runNoDebugBenchmark({baselineInstructionCount})",
            baselineInstructionCount);
        var inactiveHook = await EvaluateBenchmark(
            cdp,
            sessionId,
            $"runInactiveHookBenchmark({baselineInstructionCount})",
            baselineInstructionCount);

        string? benchmarkScript = null;
        cdp.EventReceived = message =>
        {
            if (message.Method == "Debugger.scriptParsed"
                && message.SessionId == sessionId
                && message.Parameters.GetProperty("url").GetString()
                    == "front-end-csharp-debugger-benchmark.js")
            {
                benchmarkScript = message.Parameters.GetProperty("scriptId").GetString();
            }
        };
        await cdp.SendAsync("Debugger.enable", sessionId: sessionId);
        if (benchmarkScript == null)
        {
            throw new InvalidDataException("Chromium did not report the benchmark script.");
        }
        var attachedIdle = await EvaluateBenchmark(
            cdp,
            sessionId,
            $"runInactiveHookBenchmark({baselineInstructionCount})",
            baselineInstructionCount);

        var pauseCount = 0;
        cdp.EventReceived = message =>
        {
            if (message.Method == "Debugger.paused" && message.SessionId == sessionId)
            {
                Interlocked.Increment(ref pauseCount);
                _ = cdp.SendAsync("Debugger.resume", sessionId: sessionId);
            }
        };
        var inlineBreakpoint = await EvaluateBenchmark(
            cdp,
            sessionId,
            $"runInlineBreakpointBenchmark({breakpointInstructionCount}, {breakpointInstructionCount - 1})",
            breakpointInstructionCount);
        var inlinePauseCount = pauseCount;

        var callLine = BenchmarkSource.Split('\n')
            .Select((line, index) => new { line, index })
            .Single(item => item.line.Contains(
                "CDP_CONDITIONAL_BREAKPOINT_SITE",
                StringComparison.Ordinal)).index;
        var breakpoint = await cdp.SendAsync(
            "Debugger.setBreakpoint",
            new
            {
                location = new
                {
                    scriptId = benchmarkScript,
                    lineNumber = callLine,
                    columnNumber = 8,
                },
                condition = $"opts.instructionIndex === {breakpointInstructionCount - 1}",
            },
            sessionId);
        if (!breakpoint.TryGetProperty("actualLocation", out _))
        {
            throw new InvalidDataException(
                "Chromium did not resolve the conditional CDP breakpoint.");
        }
        pauseCount = 0;
        var conditionalBreakpoint = await EvaluateBenchmark(
            cdp,
            sessionId,
            $"runConditionalBreakpointBenchmark({breakpointInstructionCount})",
            breakpointInstructionCount);
        var conditionalPauseCount = pauseCount;
        await cdp.SendAsync(
            "Debugger.removeBreakpoint",
            new { breakpointId = breakpoint.GetProperty("breakpointId").GetString() },
            sessionId);

        pauseCount = 0;
        const int stepCount = 100;
        var stepping = await EvaluateBenchmark(
            cdp,
            sessionId,
            $"runSteppingBenchmark({stepCount})",
            stepCount);
        var steppingPauseCount = pauseCount;
        if (inlinePauseCount != 1
            || conditionalPauseCount != 1
            || steppingPauseCount != stepCount)
        {
            throw new InvalidDataException(
                $"Unexpected pause counts: inline={inlinePauseCount}, "
                + $"conditional={conditionalPauseCount}, stepping={steppingPauseCount}.");
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            browser = version.GetProperty("product").GetString(),
            instructionCounts = new
            {
                baseline = baselineInstructionCount,
                breakpoint = breakpointInstructionCount,
                stepping = stepCount,
            },
            milliseconds = new
            {
                noDebugOutput = noDebug.Elapsed,
                inactiveInlineHook = inactiveHook.Elapsed,
                attachedIdleAdapter = attachedIdle.Elapsed,
                inlineBreakpoint = inlineBreakpoint.Elapsed,
                conditionalCdpBreakpoint = conditionalBreakpoint.Elapsed,
                stepping100Instructions = stepping.Elapsed,
            },
            pauses = new
            {
                inlineBreakpoint = inlinePauseCount,
                conditionalCdpBreakpoint = conditionalPauseCount,
                stepping = steppingPauseCount,
            },
            inactiveHookOverheadPercent = ((inactiveHook.Elapsed / noDebug.Elapsed) - 1) * 100,
            attachedIdleOverheadPercent = ((attachedIdle.Elapsed / inactiveHook.Elapsed) - 1) * 100,
            conditionalToInlineRatio = conditionalBreakpoint.Elapsed / inlineBreakpoint.Elapsed,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<BenchmarkMeasurement> EvaluateBenchmark(
        BrowserCdpClient cdp,
        string sessionId,
        string expression,
        int instructionCount)
    {
        var response = await cdp.SendAsync(
            "Runtime.evaluate",
            new { expression, returnByValue = true },
            sessionId);
        if (response.TryGetProperty("exceptionDetails", out var exception))
        {
            throw new InvalidOperationException(exception.GetRawText());
        }
        var value = response.GetProperty("result").GetProperty("value");
        var measurement = new BenchmarkMeasurement
        {
            Elapsed = value.GetProperty("elapsed").GetDouble(),
            Checksum = value.GetProperty("checksum").GetInt32(),
        };
        var expectedChecksum = instructionCount / 2;
        if (measurement.Checksum != expectedChecksum
            || double.IsNaN(measurement.Elapsed)
            || double.IsInfinity(measurement.Elapsed))
        {
            throw new InvalidDataException(
                $"Benchmark '{expression}' produced checksum {measurement.Checksum} and elapsed "
                + $"{measurement.Elapsed}; expected checksum {expectedChecksum}.");
        }
        return measurement;
    }
}
