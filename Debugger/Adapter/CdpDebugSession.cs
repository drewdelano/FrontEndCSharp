using System.Net.WebSockets;
using System.Text.Json;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class CdpDebugSession : IAsyncDisposable
{
    private readonly Uri? browserUrl;
    private readonly bool browserAttachOnly;
    private readonly string? browserTargetId;
    private readonly IReadOnlyList<ManagedBreakpoint> breakpoints;
    private readonly IReadOnlyList<string> exceptionFilters;
    private readonly string expectedBuildId;
    private readonly Action<IReadOnlyList<CdpManagedFrame>> paused;
    private readonly Action completed;
    private readonly Action invalidated;
    private readonly Action<string> failed;
    private readonly Action<string> diagnostic;
    private readonly CdpConnection cdp = new();
    private readonly CdpBrowserHost browserHost;
    private readonly CdpManagedPauseReader managedPauseReader;
    private readonly CdpManagedRuntimeController managedRuntimeController;
    private string? sessionId;
    private int completionSignaled;
    private int pauseBounceRequested;
    private int managedPaused;
    private int runtimeConfigurationAvailable;
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private TaskCompletionSource<bool>? navigationCommitted;
    private Uri? expectedNavigationUrl;

    internal bool HasManagedPause => Volatile.Read(ref managedPaused) == 1;
    internal bool CanPublishRuntimeConfiguration =>
        Volatile.Read(ref runtimeConfigurationAvailable) == 1;

    internal CdpDebugSession(
        string? browserPath,
        Uri? browserEndpoint,
        bool browserHeadless,
        Uri? browserUrl,
        bool browserAttachOnly,
        string? browserTargetId,
        string generatedOutputPath,
        IReadOnlyList<ManagedBreakpoint> breakpoints,
        IReadOnlyList<string> exceptionFilters,
        string expectedBuildId,
        Action<IReadOnlyList<CdpManagedFrame>> paused,
        Action completed,
        Action invalidated,
        Action<string> failed,
        Action<string> diagnostic)
    {
        this.browserUrl = browserUrl;
        this.browserAttachOnly = browserAttachOnly;
        this.browserTargetId = browserTargetId;
        this.breakpoints = breakpoints;
        this.exceptionFilters = exceptionFilters;
        this.expectedBuildId = expectedBuildId;
        this.paused = paused;
        this.completed = completed;
        this.invalidated = invalidated;
        this.failed = failed;
        this.diagnostic = diagnostic;
        browserHost = new CdpBrowserHost(
            new CdpBrowserLaunchOptions
            {
                BrowserPath = browserPath,
                Endpoint = browserEndpoint,
                Headless = browserHeadless,
                ApplicationUrl = browserUrl,
                GeneratedOutputPath = generatedOutputPath
            },
            SignalFailure);
        managedPauseReader = new CdpManagedPauseReader(cdp, expectedBuildId);
        managedRuntimeController = new CdpManagedRuntimeController(
            cdp,
            generatedOutputPath);
    }

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        var browserStart = await browserHost.StartAsync(cancellationToken);
        cdp.EventReceived += OnCdpEvent;
        cdp.Closed += OnConnectionClosed;
        await cdp.ConnectAsync(browserStart.Endpoint, cancellationToken);
        var targets = await cdp.SendAsync("Target.getTargets", cancellationToken: cancellationToken);
        string? targetId = null;
        Uri? targetUrl = null;
        foreach (var target in targets.GetProperty("targetInfos").EnumerateArray())
        {
            var candidateId = target.GetProperty("targetId").GetString();
            if (target.GetProperty("type").GetString() == "page"
                && (browserTargetId == null
                    || string.Equals(candidateId, browserTargetId, StringComparison.Ordinal)))
            {
                targetId = candidateId;
                if (target.TryGetProperty("url", out var candidateUrl)
                    && Uri.TryCreate(candidateUrl.GetString(), UriKind.Absolute, out var parsedUrl))
                {
                    targetUrl = parsedUrl;
                }
                break;
            }
        }
        if (targetId == null)
        {
            if (browserAttachOnly)
            {
                throw new InvalidOperationException(
                    $"The requested Visual Studio browser target '{browserTargetId}' is not available.");
            }
            var target = await cdp.SendAsync(
                "Target.createTarget",
                new { url = "about:blank" },
                cancellationToken: cancellationToken);
            targetId = target.GetProperty("targetId").GetString();
        }
        if (browserAttachOnly && (targetUrl == null || targetUrl != browserUrl))
        {
            throw new InvalidOperationException(
                $"Browser target '{targetId}' is at '{targetUrl}', not the expected application URL '{browserUrl}'.");
        }
        var attached = await cdp.SendAsync("Target.attachToTarget", new
        {
            flatten = true,
            targetId
        }, cancellationToken: cancellationToken);
        sessionId = attached.GetProperty("sessionId").GetString();
        await cdp.SendAsync("Runtime.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        await cdp.SendAsync("Debugger.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        await cdp.SendAsync("Page.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        await cdp.SendAsync("Log.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        await cdp.SendAsync("Network.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        var configuration = JsonSerializer.Serialize(new
        {
            buildId = expectedBuildId,
            breakpoints,
            exceptionFilters
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await cdp.SendAsync("Page.addScriptToEvaluateOnNewDocument", new
        {
            source = $"globalThis.__FrontEndCSharpDebuggerInitialConfiguration = {configuration};"
        }, sessionId, cancellationToken);
        if (browserAttachOnly)
        {
            var attachedConfigurationTask = cdp.SendAsync("Runtime.evaluate", new
            {
                expression = $"(async () => {{ const configuration = {configuration}; const runtime = globalThis.__FrontEndCSharpDebugger; if (runtime) return await runtime.configure(configuration); globalThis.__FrontEndCSharpDebuggerInitialConfiguration = configuration; return {{ buildId: configuration.buildId, pending: true }}; }})()",
                awaitPromise = true,
                returnByValue = true
            }, sessionId, cancellationToken);
            Interlocked.Exchange(ref runtimeConfigurationAvailable, 1);
            var attachedConfiguration = await attachedConfigurationTask;
            if (attachedConfiguration.TryGetProperty("exceptionDetails", out var exception))
            {
                throw new InvalidOperationException(
                    $"The existing browser target rejected the debugger configuration: {exception.GetRawText()}");
            }
            var attachedValue = attachedConfiguration.GetProperty("result").GetProperty("value");
            if (!attachedValue.TryGetProperty("buildId", out var attachedBuildId)
                || attachedBuildId.GetString() != expectedBuildId)
            {
                throw new InvalidOperationException(
                    "The existing browser target did not confirm the selected FrontEndCSharp build identity.");
            }
            diagnostic($"Attached to existing Chromium target {targetId} at {browserUrl}.");
            return;
        }
        var navigationUrl = browserStart.NavigationUrl.AbsoluteUri;
        expectedNavigationUrl = new Uri(navigationUrl);
        navigationCommitted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await cdp.SendAsync("Page.navigate", new
        {
            url = navigationUrl
        }, sessionId, cancellationToken);
        using (var navigationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            navigationTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            await navigationCommitted.Task.WaitAsync(navigationTimeout.Token);
        }
        Interlocked.Exchange(ref runtimeConfigurationAvailable, 1);
        diagnostic($"Navigated Chromium to {navigationUrl}.");
    }

    internal async Task ResumeAsync(CancellationToken cancellationToken)
    {
        await commandGate.WaitAsync(cancellationToken);
        try
        {
            if (sessionId == null)
            {
                return;
            }
            if (Interlocked.Exchange(ref managedPaused, 0) != 1)
            {
                throw new InvalidOperationException(
                    "FrontEndCSharp cannot continue a Chromium pause owned by another debugger.");
            }
            await cdp.SendAsync("Debugger.resume", sessionId: sessionId, cancellationToken: cancellationToken);
        }
        finally
        {
            commandGate.Release();
        }
    }

    internal async Task PauseAsync(CancellationToken cancellationToken)
    {
        await commandGate.WaitAsync(cancellationToken);
        try
        {
            if (sessionId == null || Interlocked.Exchange(ref pauseBounceRequested, 1) != 0)
            {
                return;
            }
            await cdp.SendAsync("Debugger.pause", sessionId: sessionId, cancellationToken: cancellationToken);
        }
        finally
        {
            commandGate.Release();
        }
    }

    internal async Task<ManagedValueSnapshot> SetVariableAsync(
        string callFrameId,
        ManagedAssignmentTarget target,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callFrameId);
        ArgumentNullException.ThrowIfNull(target);
        await commandGate.WaitAsync(cancellationToken);
        try
        {
            if (sessionId == null || Volatile.Read(ref managedPaused) != 1)
            {
                throw new InvalidOperationException("The managed stack frame is no longer available.");
            }
            return await managedRuntimeController.SetVariableAsync(
                sessionId,
                callFrameId,
                target,
                value,
                cancellationToken);
        }
        finally
        {
            commandGate.Release();
        }
    }

    internal async Task StepAsync(
        string kind,
        int baselineDepth,
        IReadOnlyList<ManagedStepLocation> locations,
        CancellationToken cancellationToken)
    {
        await commandGate.WaitAsync(cancellationToken);
        try
        {
            if (sessionId == null)
            {
                return;
            }
            if (Volatile.Read(ref managedPaused) != 1)
            {
                throw new InvalidOperationException(
                    "FrontEndCSharp cannot step a Chromium pause owned by another debugger.");
            }
            var plan = JsonSerializer.Serialize(new { kind, baselineDepth, locations }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await cdp.SendAsync("Runtime.evaluate", new
            {
                expression = $"(() => {{ const plan = {plan}; globalThis.__FrontEndCSharpDebugger.requestStep(plan.kind, plan.baselineDepth, plan.locations); }})()"
            }, sessionId, cancellationToken);
            Interlocked.Exchange(ref managedPaused, 0);
            await cdp.SendAsync("Debugger.resume", sessionId: sessionId, cancellationToken: cancellationToken);
        }
        finally
        {
            commandGate.Release();
        }
    }

    internal async Task<RuntimeUpdateAcknowledgement> ApplyUpdateAsync(
        string relativeModulePath,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        await commandGate.WaitAsync(cancellationToken);
        try
        {
            if (sessionId == null)
            {
                throw new InvalidOperationException("The Chromium debug target is not connected.");
            }
            if (Volatile.Read(ref managedPaused) == 0)
            {
                throw new InvalidOperationException(
                    "Edit and Continue is supported only while stopped at a managed instruction.");
            }
            return await managedRuntimeController.ApplyUpdateAsync(
                sessionId,
                relativeModulePath,
                expectedHash,
                cancellationToken);
        }
        finally
        {
            commandGate.Release();
        }
    }

    internal async Task SetExceptionBreakpointsAsync(
        IReadOnlyList<string> filters,
        CancellationToken cancellationToken)
    {
        await commandGate.WaitAsync(cancellationToken);
        try
        {
            if (sessionId == null)
            {
                return;
            }
            await managedRuntimeController.SetExceptionBreakpointsAsync(
                sessionId,
                filters,
                cancellationToken);
        }
        finally
        {
            commandGate.Release();
        }
    }

    internal async Task SetBreakpointsAsync(
        IReadOnlyList<ManagedBreakpoint> locations,
        CancellationToken cancellationToken)
    {
        await commandGate.WaitAsync(cancellationToken);
        try
        {
            if (sessionId == null)
            {
                throw new InvalidOperationException("The Chromium debug target is not connected.");
            }
            await managedRuntimeController.SetBreakpointsAsync(
                sessionId,
                locations,
                cancellationToken);
        }
        finally
        {
            commandGate.Release();
        }
    }

    private void OnCdpEvent(string method, JsonElement parameters, string? eventSessionId)
    {
        if (eventSessionId != sessionId)
        {
            return;
        }
        if (method == "Page.frameNavigated"
            && !parameters.GetProperty("frame").TryGetProperty("parentId", out _)
            && parameters.GetProperty("frame").TryGetProperty("url", out var frameUrl)
            && Uri.TryCreate(frameUrl.GetString(), UriKind.Absolute, out var committedUrl)
            && expectedNavigationUrl != null
            && committedUrl.Scheme == expectedNavigationUrl.Scheme
            && committedUrl.Port == expectedNavigationUrl.Port
            && string.Equals(committedUrl.Host, expectedNavigationUrl.Host, StringComparison.OrdinalIgnoreCase))
        {
            navigationCommitted?.TrySetResult(true);
            return;
        }
        if (method == "Debugger.paused")
        {
            if (Interlocked.Exchange(ref pauseBounceRequested, 0) == 1)
            {
                _ = BounceToManagedBoundaryAsync();
                return;
            }
            _ = CaptureManagedFramesAsync(parameters);
            return;
        }
        if (method == "Runtime.executionContextsCleared")
        {
            invalidated();
            return;
        }
        if (method == "Runtime.exceptionThrown")
        {
            diagnostic($"Chromium exception: {parameters.GetRawText()}");
            return;
        }
        if (method == "Log.entryAdded" || method == "Network.loadingFailed")
        {
            diagnostic($"Chromium {method}: {parameters.GetRawText()}");
            return;
        }
        if (method != "Runtime.consoleAPICalled"
            || !parameters.TryGetProperty("args", out var arguments)
            || !arguments.EnumerateArray().Any(argument =>
                argument.TryGetProperty("value", out var value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() == "__FECS_DEBUGGEE_COMPLETED__"))
        {
            return;
        }
        if (Interlocked.Exchange(ref completionSignaled, 1) == 0)
        {
            completed();
        }
    }

    private void OnConnectionClosed(Exception? exception)
    {
        SignalFailure(exception == null
            ? "The Chromium debugging connection closed unexpectedly."
            : $"The Chromium debugging connection failed: {exception.Message}");
    }

    private void SignalFailure(string message)
    {
        if (sessionId == null || Interlocked.Exchange(ref completionSignaled, 1) != 0)
        {
            return;
        }
        sessionId = null;
        invalidated();
        failed(message);
    }

    private async Task BounceToManagedBoundaryAsync()
    {
        try
        {
            await commandGate.WaitAsync();
            try
            {
                await cdp.SendAsync("Runtime.evaluate", new
                {
                    expression = "globalThis.__FrontEndCSharpDebugger.requestPause()"
                }, sessionId);
                await cdp.SendAsync("Debugger.resume", sessionId: sessionId);
            }
            finally
            {
                commandGate.Release();
            }
        }
        catch (Exception exception)
        {
            diagnostic($"Unable to pause at a managed boundary: {exception}");
        }
    }

    private async Task CaptureManagedFramesAsync(JsonElement parameters)
    {
        var currentSessionId = sessionId;
        if (currentSessionId == null)
        {
            return;
        }
        var capture = await managedPauseReader.CaptureAsync(parameters, currentSessionId);
        if (capture.Error != null)
        {
            diagnostic($"Unable to capture managed frames: {capture.Error}");
            if (!capture.OwnershipConfirmed)
            {
                return;
            }
            Interlocked.Exchange(ref managedPaused, 0);
            try
            {
                await cdp.SendAsync("Debugger.resume", sessionId: sessionId);
            }
            catch (Exception resumeException)
            {
                diagnostic($"Unable to resume after rejecting an invalid managed stop: {resumeException}");
            }
            return;
        }
        if (!capture.OwnershipConfirmed)
        {
            diagnostic("Ignored a Chromium pause that is not owned by the FrontEndCSharp runtime.");
            return;
        }
        if (capture.Frames.Count > 0)
        {
            Interlocked.Exchange(ref managedPaused, 1);
            paused(capture.Frames);
            return;
        }
        if (capture.FramesMissing)
        {
            diagnostic("The FrontEndCSharp runtime reported a managed stop without an inspectable managed frame; resuming the target.");
            await cdp.SendAsync("Debugger.resume", sessionId: currentSessionId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DetachAsync();
        cdp.EventReceived -= OnCdpEvent;
        cdp.Closed -= OnConnectionClosed;
        await cdp.DisposeAsync();
        await browserHost.DisposeAsync();
        commandGate.Dispose();
    }

    private async Task DetachAsync()
    {
        var detachedSessionId = sessionId;
        if (detachedSessionId == null)
        {
            return;
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await commandGate.WaitAsync(timeout.Token);
        try
        {
            sessionId = null;
            try
            {
                await cdp.SendAsync("Runtime.evaluate", new
                {
                    expression = "globalThis.__FrontEndCSharpDebugger?.disable()"
                }, detachedSessionId, timeout.Token);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or OperationCanceledException
                or WebSocketException)
            {
                diagnostic($"Unable to clear runtime debugger state during detach: {exception.Message}");
            }
            if (!browserAttachOnly)
            {
                try
                {
                    await cdp.SendAsync("Debugger.resume", sessionId: detachedSessionId, cancellationToken: timeout.Token);
                }
                catch (Exception exception) when (exception is InvalidOperationException
                    or OperationCanceledException
                    or WebSocketException)
                {
                    if (!exception.Message.Contains(
                        "Can only perform operation while paused",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostic($"Unable to resume the Chromium target during detach: {exception.Message}");
                    }
                }
            }
            try
            {
                await cdp.SendAsync("Target.detachFromTarget", new
                {
                    sessionId = detachedSessionId
                }, cancellationToken: timeout.Token);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or OperationCanceledException
                or WebSocketException)
            {
                diagnostic($"Unable to detach from the Chromium target cleanly: {exception.Message}");
            }
        }
        finally
        {
            commandGate.Release();
        }
    }
}
