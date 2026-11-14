using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using ProtocolBreakpoint = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Breakpoint;
using ProtocolThread = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class FrontEndDebugAdapter : DebugAdapterBase
{
    private readonly object gate = new();
    private readonly SemaphoreSlim configurationPublishGate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly ManagedPauseRequestCoordinator pauseRequests;
    private CdpDebugSession? session;
    private EditAndContinueBroker? editAndContinueBroker;
    private FrontEndDebugConfiguration? configuration;
    private int nextBreakpointId;
    private DebugManifestIndex? manifest;
    private List<ActiveBreakpoint> activeBreakpoints = [];
    private readonly List<RequestedBreakpoint> requestedBreakpoints = [];
    private List<string> exceptionFilters = [];
    private readonly ManagedPauseState pauseState = new();

    internal FrontEndDebugAdapter(Stream input, Stream output)
    {
        pauseRequests = new ManagedPauseRequestCoordinator(
            pauseState,
            gate,
            () => session,
            lifetime);
        InitializeProtocolClient(input, output);
    }

    protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
    {
        Protocol.SendEvent(new InitializedEvent());
        return new InitializeResponse
        {
            SupportsConfigurationDoneRequest = true,
            SupportsExceptionInfoRequest = true,
            SupportsEvaluateForHovers = true,
            SupportsSetVariable = true,
            ExceptionBreakpointFilters = [
                new ExceptionBreakpointsFilter("frontendcsharp-thrown", "Thrown managed exceptions") { Default = false },
                new ExceptionBreakpointsFilter("frontendcsharp-unhandled", "Unhandled managed exceptions") { Default = true }
            ]
        };
    }

    protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
    {
        ConfigureSessionFromProtocol(arguments.ConfigurationProperties);
        return new LaunchResponse();
    }

    protected override AttachResponse HandleAttachRequest(AttachArguments arguments)
    {
        ConfigureSessionFromProtocol(arguments.ConfigurationProperties);
        return new AttachResponse();
    }

    private void ConfigureSessionFromProtocol(
        IReadOnlyDictionary<string, Newtonsoft.Json.Linq.JToken> configuration)
    {
        try
        {
            ConfigureSession(configuration);
        }
        catch (ProtocolException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException
            or FormatException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException
            or System.Text.Json.JsonException)
        {
            throw new ProtocolException($"Invalid debug configuration: {ex.Message}");
        }
    }

    private void ConfigureSession(
        IReadOnlyDictionary<string, Newtonsoft.Json.Linq.JToken> properties)
    {
        configuration = FrontEndDebugConfiguration.Parse(properties);
        manifest = configuration.Manifest;
    }

    protected override ConfigurationDoneResponse HandleConfigurationDoneRequest(
        ConfigurationDoneArguments arguments)
    {
        _ = StartDebuggeeAsync();
        return new ConfigurationDoneResponse();
    }

    protected override SetBreakpointsResponse HandleSetBreakpointsRequest(
        SetBreakpointsArguments arguments)
    {
        var requestedSource = Path.GetFullPath(arguments.Source.Path ?? string.Empty);
        var resolved = new List<ActiveBreakpoint>();
        var breakpoints = new List<ProtocolBreakpoint>();
        foreach (var breakpoint in arguments.Breakpoints ?? [])
        {
            var id = Interlocked.Increment(ref nextBreakpointId);
            if (manifest!.TryResolve(requestedSource, breakpoint.Line, out var location, out var error))
            {
                resolved.Add(new ActiveBreakpoint(id, location!));
                breakpoints.Add(new ProtocolBreakpoint
                {
                    Verified = true,
                    Id = id,
                    Line = location!.Line,
                    Column = location.Column,
                    Source = arguments.Source
                });
            }
            else
            {
                breakpoints.Add(new ProtocolBreakpoint
                {
                    Verified = false,
                    Id = id,
                    Line = breakpoint.Line,
                    Source = arguments.Source,
                    Message = error
                });
            }
        }
        lock (gate)
        {
            requestedBreakpoints.RemoveAll(item => string.Equals(
                item.SourcePath, requestedSource, StringComparison.OrdinalIgnoreCase));
            requestedBreakpoints.AddRange((arguments.Breakpoints ?? []).Select((breakpoint, index) =>
                new RequestedBreakpoint(
                    breakpoints[index].Id!.Value,
                    requestedSource,
                    breakpoint.Line)));
            activeBreakpoints.RemoveAll(item => string.Equals(
                Path.GetFullPath(item.Location.SourcePath), requestedSource, StringComparison.OrdinalIgnoreCase));
            activeBreakpoints.AddRange(resolved);
        }
        PublishBreakpointsToRuntime();
        return new SetBreakpointsResponse(breakpoints);
    }

    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(
        SetExceptionBreakpointsArguments arguments)
    {
        var requested = arguments.Filters
            .Select(filter => filter switch
            {
                "frontendcsharp-thrown" => "thrown",
                "frontendcsharp-unhandled" => "unhandled",
                _ => null
            })
            .Where(stage => stage != null)
            .Select(stage => stage!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        lock (gate)
        {
            exceptionFilters = requested;
        }
        PublishExceptionBreakpointsToRuntime();
        return new SetExceptionBreakpointsResponse();
    }

    protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments arguments)
    {
        return new ThreadsResponse([new ProtocolThread(id: 1, name: "Managed Thread")]);
    }

    protected override StackTraceResponse HandleStackTraceRequest(
        StackTraceArguments arguments) => pauseRequests.ReadStackTrace();

    protected override ScopesResponse HandleScopesRequest(
        ScopesArguments arguments) => pauseRequests.ReadScopes(arguments);

    protected override VariablesResponse HandleVariablesRequest(
        VariablesArguments arguments) => pauseRequests.ReadVariables(arguments);

    protected override SetVariableResponse HandleSetVariableRequest(
        SetVariableArguments arguments) => pauseRequests.SetVariable(arguments);

    protected override EvaluateResponse HandleEvaluateRequest(
        EvaluateArguments arguments) => pauseRequests.Evaluate(arguments);

    protected override ExceptionInfoResponse HandleExceptionInfoRequest(
        ExceptionInfoArguments arguments) => pauseRequests.ReadExceptionInfo(arguments);

    protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
    {
        if (session == null || !session.HasManagedPause)
        {
            throw new ProtocolException(
                "FrontEndCSharp cannot continue a Chromium pause owned by another debugger.");
        }
        lock (gate)
        {
            pauseState.Reset();
        }
        session?.ResumeAsync(lifetime.Token).GetAwaiter().GetResult();
        return new ContinueResponse { AllThreadsContinued = true };
    }

    protected override PauseResponse HandlePauseRequest(PauseArguments arguments)
    {
        session?.PauseAsync(lifetime.Token).GetAwaiter().GetResult();
        return new PauseResponse();
    }

    protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
    {
        ExecuteStep("in");
        return new StepInResponse();
    }

    protected override NextResponse HandleNextRequest(NextArguments arguments)
    {
        ExecuteStep("over");
        return new NextResponse();
    }

    protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
    {
        ExecuteStep("out");
        return new StepOutResponse();
    }

    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
    {
        lifetime.Cancel();
        editAndContinueBroker?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        session?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return new DisconnectResponse();
    }

    private async Task<RuntimeUpdateAcknowledgement> ApplyUpdateAsync(
        string updateModulePath,
        string nextManifestPath)
    {
        var currentManifest = manifest
            ?? throw new ProtocolException("The debug manifest has not been loaded.");
        var nextManifest = DebugManifestIndex.Load(Path.GetFullPath(nextManifestPath));
        if (!string.Equals(nextManifest.BuildId, currentManifest.BuildId, StringComparison.Ordinal)
            || nextManifest.Generation != currentManifest.Generation + 1)
        {
            throw new ProtocolException(
                $"The update manifest is stale or belongs to another build. Expected baseline '{currentManifest.BuildId}' generation {currentManifest.Generation + 1}.");
        }

        List<StagedBreakpoint> staged = [];
        lock (gate)
        {
            foreach (var breakpoint in requestedBreakpoints)
            {
                if (nextManifest.TryResolve(
                    breakpoint.SourcePath,
                    breakpoint.Line,
                    out var location,
                    out var error))
                    staged.Add(new StagedBreakpoint(breakpoint, location, error));
            }
        }
        var rebound = staged.Where(item => item.Location != null)
            .Select(item => new ActiveBreakpoint(item.Request.Id, item.Location!))
            .ToList();

        var acknowledgement = await (session
            ?? throw new ProtocolException("The debug session is not running."))
            .ApplyUpdateAsync(updateModulePath, nextManifest.ContentHash, lifetime.Token);
        if (!string.Equals(acknowledgement.BaselineBuildId, currentManifest.BuildId, StringComparison.Ordinal)
            || acknowledgement.Generation != nextManifest.Generation
            || !string.Equals(acknowledgement.Hash, nextManifest.ContentHash, StringComparison.Ordinal))
        {
            OnFailed(
                "The runtime committed an update whose acknowledgement does not match the staged debug manifest. The session was terminated to avoid mixed code and symbols.");
            throw new ProtocolException(
                "The runtime update acknowledgement does not match the staged debug manifest. The session cannot safely continue.");
        }

        try
        {
            await session.SetBreakpointsAsync(rebound.Select(item => new ManagedBreakpoint(
                item.Id,
                item.Location.ModuleId,
                item.Location.MethodToken,
                item.Location.BlockIndex,
                item.Location.InstructionIndex)).ToList(), lifetime.Token);
        }
        catch (Exception exception)
        {
            OnFailed(
                $"The runtime committed generation {acknowledgement.Generation}, but breakpoint publication failed: {exception.Message}. The session was terminated to avoid mixed debugger state.");
            throw;
        }
        lock (gate)
        {
            manifest = nextManifest;
            activeBreakpoints = rebound;
        }
        foreach (var item in staged)
        {
            Protocol.SendEvent(new BreakpointEvent(
                BreakpointEvent.ReasonValue.Changed,
                new ProtocolBreakpoint
                {
                    Id = item.Request.Id,
                    Verified = item.Location != null,
                    Line = item.Location?.Line ?? item.Request.Line,
                    Column = item.Location?.Column,
                    Source = new Source
                    {
                        Name = Path.GetFileName(item.Request.SourcePath),
                        Path = item.Request.SourcePath
                    },
                    Message = item.Error
                }));
        }
        WriteDiagnostic(
            $"Applied FrontEndCSharp update generation {acknowledgement.Generation} ({acknowledgement.Hash}).");
        return acknowledgement;
    }


    private async Task StartDebuggeeAsync()
    {
        CdpDebugSession? newSession = null;
        var sessionStarted = false;
        try
        {
            var currentConfiguration = configuration
                ?? throw new ProtocolException("The debug session has not been configured.");
            List<ActiveBreakpoint> breakpointSnapshot;
            List<string> exceptionFilterSnapshot;
            lock (gate)
            {
                breakpointSnapshot = activeBreakpoints.ToList();
                exceptionFilterSnapshot = exceptionFilters.ToList();
            }
            newSession = new CdpDebugSession(
                currentConfiguration.BrowserPath,
                currentConfiguration.BrowserEndpoint,
                currentConfiguration.BrowserHeadless,
                currentConfiguration.BrowserUrl,
                currentConfiguration.BrowserAttachOnly,
                currentConfiguration.BrowserTargetId,
                currentConfiguration.GeneratedOutputPath,
                breakpointSnapshot.Select(item => new ManagedBreakpoint(
                    item.Id,
                    item.Location.ModuleId,
                    item.Location.MethodToken,
                    item.Location.BlockIndex,
                    item.Location.InstructionIndex)).ToList(),
                exceptionFilterSnapshot,
                manifest!.BuildId,
                OnPaused,
                OnCompleted,
                OnInvalidated,
                OnFailed,
                WriteDiagnostic);
            lock (gate)
            {
                session = newSession;
            }
            await newSession.StartAsync(lifetime.Token);
            await configurationPublishGate.WaitAsync(lifetime.Token);
            try
            {
                List<ManagedBreakpoint> currentBreakpoints;
                List<string> currentExceptionFilters;
                lock (gate)
                {
                    currentBreakpoints = SnapshotManagedBreakpointsLocked();
                    currentExceptionFilters = exceptionFilters.ToList();
                }
                await newSession.SetBreakpointsAsync(currentBreakpoints, lifetime.Token);
                await newSession.SetExceptionBreakpointsAsync(currentExceptionFilters, lifetime.Token);
            }
            finally
            {
                configurationPublishGate.Release();
            }
            if (currentConfiguration.EditAndContinuePipe != null)
            {
                editAndContinueBroker = new EditAndContinueBroker(
                    currentConfiguration.EditAndContinuePipe,
                    currentConfiguration.EditAndContinueToken!,
                    (modulePath, manifestPath) => ApplyUpdateAsync(modulePath, manifestPath),
                    WriteDiagnostic);
                editAndContinueBroker.Start();
                WriteDiagnostic("Edit and Continue command channel is ready.");
            }
            sessionStarted = true;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Protocol.SendEvent(new OutputEvent($"FrontEndCSharp debug adapter failed: {exception}\n")
            {
                Category = OutputEvent.CategoryValue.Stderr
            });
            Protocol.SendEvent(new TerminatedEvent());
        }
        finally
        {
            if (!sessionStarted && newSession != null)
            {
                lock (gate)
                {
                    if (ReferenceEquals(session, newSession))
                    {
                        session = null;
                    }
                }
                await newSession.DisposeAsync();
            }
        }
    }

    private void PublishBreakpointsToRuntime()
    {
        configurationPublishGate.Wait(lifetime.Token);
        try
        {
            CdpDebugSession? currentSession;
            List<ManagedBreakpoint> currentBreakpoints;
            lock (gate)
            {
                currentSession = session?.CanPublishRuntimeConfiguration == true ? session : null;
                currentBreakpoints = SnapshotManagedBreakpointsLocked();
            }
            currentSession?.SetBreakpointsAsync(currentBreakpoints, lifetime.Token).GetAwaiter().GetResult();
        }
        finally
        {
            configurationPublishGate.Release();
        }
    }

    private void PublishExceptionBreakpointsToRuntime()
    {
        configurationPublishGate.Wait(lifetime.Token);
        try
        {
            CdpDebugSession? currentSession;
            List<string> currentExceptionFilters;
            lock (gate)
            {
                currentSession = session?.CanPublishRuntimeConfiguration == true ? session : null;
                currentExceptionFilters = exceptionFilters.ToList();
            }
            currentSession?.SetExceptionBreakpointsAsync(currentExceptionFilters, lifetime.Token)
                .GetAwaiter().GetResult();
        }
        finally
        {
            configurationPublishGate.Release();
        }
    }

    private List<ManagedBreakpoint> SnapshotManagedBreakpointsLocked() => activeBreakpoints
        .Select(item => new ManagedBreakpoint(
            item.Id,
            item.Location.ModuleId,
            item.Location.MethodToken,
            item.Location.BlockIndex,
            item.Location.InstructionIndex))
        .ToList();

    private void OnPaused(IReadOnlyList<CdpManagedFrame> cdpFrames)
    {
        ManagedPauseSnapshot snapshot;
        lock (gate)
        {
            snapshot = pauseState.Project(
                cdpFrames,
                manifest ?? throw new ProtocolException("The debug manifest has not been loaded."));
        }
        var stopReason = snapshot.StopReason switch
        {
            "pause" => StoppedEvent.ReasonValue.Pause,
            "step" => StoppedEvent.ReasonValue.Step,
            "exception" => StoppedEvent.ReasonValue.Exception,
            _ => StoppedEvent.ReasonValue.Breakpoint
        };
        Protocol.SendEvent(new StoppedEvent(stopReason)
        {
            Description = stopReason == StoppedEvent.ReasonValue.Exception
                ? $"Managed exception ({snapshot.ExceptionStage}): {snapshot.ExceptionType}"
                : "Managed stop",
            ThreadId = 1,
            AllThreadsStopped = true,
            HitBreakpointIds = snapshot.HitBreakpointIds
        });
    }
    private void ExecuteStep(string kind)
    {
        int baselineDepth;
        lock (gate)
        {
            baselineDepth = pauseState.ManagedDepth;
            if (baselineDepth == 0)
            {
                throw new ProtocolException("Stepping requires a managed suspension.");
            }
            pauseState.Reset();
        }
        session!.StepAsync(kind, baselineDepth, manifest!.GetStepLocations(), lifetime.Token)
            .GetAwaiter().GetResult();
    }

    private sealed record RequestedBreakpoint(int Id, string SourcePath, int Line);

    private sealed record class ActiveBreakpoint(int Id, ResolvedBreakpoint Location);

    private sealed record class StagedBreakpoint(
        RequestedBreakpoint Request,
        ResolvedBreakpoint? Location,
        string? Error);

    private void OnCompleted()
    {
        Protocol.SendEvent(new ExitedEvent(exitCode: 0));
        Protocol.SendEvent(new TerminatedEvent());
    }

    private void OnInvalidated()
    {
        lock (gate)
        {
            pauseState.Reset();
        }
    }

    private void OnFailed(string message)
    {
        Protocol.SendEvent(new OutputEvent($"{message}\n")
        {
            Category = OutputEvent.CategoryValue.Stderr
        });
        Protocol.SendEvent(new TerminatedEvent());
    }

    private void WriteDiagnostic(string message)
    {
        Protocol.SendEvent(new OutputEvent($"{message}\n")
        {
            Category = OutputEvent.CategoryValue.Console
        });
    }

}
