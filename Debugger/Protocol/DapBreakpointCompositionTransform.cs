using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

/// <summary>
/// Projects manifest-owned C# breakpoints onto conditional JavaScript
/// breakpoints at the interpreter's executeInstruction boundary. Requests for
/// every other source and every other DAP command are returned verbatim.
/// </summary>
public sealed class DapBreakpointCompositionTransform
{
    private readonly Action<string>? diagnosticSink;
    private readonly DapDiagnosticTracer diagnosticTracer;
    private readonly ManagedRuntimeCoordinator runtimeCoordinator;
    private readonly ManagedBreakpointCoordinator breakpointCoordinator;
    private readonly ManagedExceptionCoordinator exceptionCoordinator;
    private readonly ManagedVariableState variableState;
    private readonly ManagedVariableRequestCoordinator variableRequestCoordinator;
    private readonly ManagedVariableResponseCoordinator variableResponseCoordinator;
    private readonly ManagedStepCoordinator stepCoordinator;
    private readonly ManagedStackState stackState;
    private readonly ManagedStackRequestCoordinator stackRequestCoordinator;
    private readonly ManagedStackExpansionCoordinator stackExpansionCoordinator;
    private readonly Dictionary<int, PendingManagedStepStop> pendingStepStopProbes =
        new Dictionary<int, PendingManagedStepStop>();
    private readonly object gate = new object();
    private int nextProxyRequestSequence = 1500000000;

    public DapBreakpointCompositionTransform(
        IManagedBreakpointResolver resolver,
        string runtimeSourcePath,
        int runtimeLine,
        Action<string>? diagnosticSink = null,
        string? expectedBuildId = null)
        : this(
            new[]
            {
                new DapBreakpointCompositionCandidate(
                    resolver,
                    runtimeSourcePath,
                    runtimeLine,
                    expectedBuildId ?? "unidentified-build")
            },
            diagnosticSink,
            expectedBuildId)
    {
    }

    public DapBreakpointCompositionTransform(
        IReadOnlyList<DapBreakpointCompositionCandidate> candidates,
        Action<string>? diagnosticSink = null)
        : this(candidates, diagnosticSink, candidates?.FirstOrDefault()?.BuildId)
    {
    }

    private DapBreakpointCompositionTransform(
        IReadOnlyList<DapBreakpointCompositionCandidate> candidates,
        Action<string>? diagnosticSink,
        string? initialBuildId)
    {
        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }
        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one debugger composition candidate is required.", nameof(candidates));
        }
        this.diagnosticSink = diagnosticSink;
        diagnosticTracer = new DapDiagnosticTracer(diagnosticSink);
        runtimeCoordinator = new ManagedRuntimeCoordinator(
            candidates.ToArray(),
            initialBuildId,
            diagnosticSink);
        breakpointCoordinator = new ManagedBreakpointCoordinator(diagnosticSink);
        exceptionCoordinator = new ManagedExceptionCoordinator(diagnosticSink);
        variableState = new ManagedVariableState();
        variableRequestCoordinator = new ManagedVariableRequestCoordinator(
            variableState,
            diagnosticSink);
        variableResponseCoordinator = new ManagedVariableResponseCoordinator(
            variableState,
            diagnosticSink);
        stepCoordinator = new ManagedStepCoordinator(diagnosticSink);
        stackState = new ManagedStackState();
        stackRequestCoordinator = new ManagedStackRequestCoordinator(
            stackState,
            diagnosticSink);
        stackExpansionCoordinator = new ManagedStackExpansionCoordinator(
            stackState,
            runtimeCoordinator,
            diagnosticSink);
    }

    public string TransformClientToAdapter(string payload)
    {
        lock (gate)
        {
            return TransformClientToAdapterCore(payload);
        }
    }

    private string TransformClientToAdapterCore(string payload)
    {
        diagnosticTracer.TraceClientMilestone(payload);
        if (exceptionCoordinator.TryTransformClientRequest(
            payload,
            out var exceptionPayload))
        {
            return exceptionPayload;
        }
        if (stepCoordinator.TryTransformClientRequest(
            payload,
            stackState.ActiveBinding,
            stackState.ActiveFrameId,
            runtimeCoordinator.Resolver,
            out var stepPayload))
        {
            return stepPayload;
        }
        if (variableRequestCoordinator.TryTransformClientRequest(
            payload,
            runtimeCoordinator.Resolver,
            stackState.LogicalFrames,
            stackState.ProjectedFrames,
            out var variablePayload))
        {
            return variablePayload;
        }
        if (stackRequestCoordinator.TryTrackClientRequest(payload))
        {
            return payload;
        }
        if (!ManagedBreakpointCoordinator.TryParseSetBreakpointsRequest(
            payload,
            out var message,
            out var arguments))
        {
            return payload;
        }

        var source = arguments["source"] as JObject;
        var sourcePath = source?.Value<string>("path");
        var requestSequence = message.Value<int?>("seq");
        if (sourcePath == null
            || string.IsNullOrWhiteSpace(sourcePath)
            || !requestSequence.HasValue)
        {
            diagnosticSink?.Invoke("request-invalid");
            return payload;
        }
        runtimeCoordinator.SelectForSource(sourcePath);
        if (!runtimeCoordinator.Resolver.OwnsSource(sourcePath))
        {
            diagnosticSink?.Invoke("request-unowned");
            return payload;
        }

        return breakpointCoordinator.TransformClientRequest(
            message,
            arguments,
            sourcePath,
            runtimeCoordinator.Resolver,
            runtimeCoordinator.RuntimeSourcePath,
            runtimeCoordinator.RuntimeLine);
    }

    public string TransformAdapterToClient(string payload)
    {
        lock (gate)
        {
            return TransformAdapterToClientCore(payload);
        }
    }

    public DapTransformDispatch TransformAdapterToClientDuplex(string payload)
    {
        lock (gate)
        {
            return TransformAdapterToClientDuplexCore(payload);
        }
    }

    private DapTransformDispatch TransformAdapterToClientDuplexCore(string payload)
    {
        JObject message;
        try
        {
            message = JObject.Parse(payload);
        }
        catch (JsonException)
        {
            return ClientOnly(payload);
        }

        diagnosticTracer.TraceAdapterMilestone(message);

        if (variableResponseCoordinator.TryCompleteManagedScalarMaterialization(
            message,
            out var scalarCompletion))
        {
            return scalarCompletion;
        }

        if (breakpointCoordinator.TryTransformBreakpointEvents(
            message,
            out var breakpointEvents))
        {
            return new DapTransformDispatch(
                breakpointEvents,
                Array.Empty<string>());
        }

        if (runtimeCoordinator.TryHandleAdapterMessage(
            message,
            payload,
            exceptionCoordinator.Filters,
            () => nextProxyRequestSequence++,
            TransformAdapterToClientCore,
            out var runtimeDispatch))
        {
            return runtimeDispatch;
        }
        if (stepCoordinator.TryHandleAdapterResponse(
            message,
            runtimeCoordinator,
            frameId => stackState.ActiveFrameId = frameId,
            () => nextProxyRequestSequence++,
            out var stepDispatch))
        {
            return stepDispatch;
        }
        if (string.Equals(message.Value<string>("type"), "event", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("event"), "stopped", StringComparison.Ordinal)
            && (stepCoordinator.AwaitingStop != null || exceptionCoordinator.HasFilters))
        {
            var probeSequence = nextProxyRequestSequence++;
            pendingStepStopProbes[probeSequence] = new PendingManagedStepStop(
                payload,
                message,
                stepCoordinator.AwaitingStop);
            diagnosticSink?.Invoke("stop-managed-probe-started");
            return new DapTransformDispatch(
                Array.Empty<string>(),
                new[]
                {
                    new JObject
                    {
                        ["seq"] = probeSequence,
                        ["type"] = "request",
                        ["command"] = "evaluate",
                        ["arguments"] = new JObject
                        {
                            ["expression"] = "JSON.stringify((stop=>stop?[stop.reason,stop.exceptionStage??null,stop.exceptionType??null,stop.frames?.[0]?.moduleId??stop.moduleId??null,stop.frames?.[0]?.methodToken??stop.methodToken??0,stop.frames?.[0]?.blockIndex??stop.blockIndex??0,stop.frames?.[0]?.instructionIndex??stop.instructionIndex??0]:null)(globalThis.__FrontEndCSharpDebugger?.currentStop))",
                            ["context"] = "watch"
                        }
                    }.ToString(Formatting.None)
                });
        }

        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int stepStopProbeSequence
            && pendingStepStopProbes.TryGetValue(stepStopProbeSequence, out var pendingStepStop))
        {
            pendingStepStopProbes.Remove(stepStopProbeSequence);
            var stop = message.Value<bool?>("success") == true
                ? ManagedStackFrameParser.ParseStopProbe(message["body"]?.Value<string>("result"))
                : null;
            if (stop != null
                && string.Equals(stop.Reason, "step", StringComparison.Ordinal)
                && pendingStepStop.Step != null)
            {
                stackState.ActiveProjection = stackExpansionCoordinator.ResolveFrameProjection(
                    stop.Frame,
                    breakpointCoordinator.SourcePaths);
                stackState.ActiveBinding = stackState.ActiveProjection?.Binding
                    ?? pendingStepStop.Step.AnchorBinding;
                stackState.ActiveFrameId = null;
                stackState.ActiveStopNeedsIdentity = stackState.ActiveProjection == null;
                stepCoordinator.ClearAwaitingStop();
                if (pendingStepStop.Event["body"] is JObject body)
                {
                    body["reason"] = "step";
                    body["description"] = "Managed step";
                }
                diagnosticSink?.Invoke("stop-step-correlated");
                return ClientOnly(pendingStepStop.Event.ToString(Formatting.None));
            }
            if (stop != null
                && string.Equals(stop.Reason, "exception", StringComparison.Ordinal))
            {
                // The runtime treats a reported exception as the terminal result of
                // the pending step. Mirror that lifecycle here so a later native stop
                // cannot be mistaken for completion of the superseded step request.
                stepCoordinator.ClearAwaitingStop();
                var activeManagedException = exceptionCoordinator.BeginExceptionStop(
                    stop.ExceptionStage ?? "thrown",
                    stop.ExceptionType ?? "System.Exception");
                stackState.ActiveProjection = stackExpansionCoordinator.ResolveFrameProjection(
                    stop.Frame,
                    breakpointCoordinator.SourcePaths);
                stackState.ActiveBinding = stackState.ActiveProjection?.Binding;
                stackState.ActiveFrameId = null;
                stackState.ActiveStopNeedsIdentity = stackState.ActiveBinding != null
                    && stackState.ActiveProjection == null;
                if (pendingStepStop.Event["body"] is JObject body)
                {
                    body["reason"] = "exception";
                    body["description"] = string.Format(
                        CultureInfo.InvariantCulture,
                        "Managed exception ({0}): {1}",
                        activeManagedException.Stage,
                        activeManagedException.Type);
                }
                diagnosticSink?.Invoke("stop-exception-correlated");
                return ClientOnly(pendingStepStop.Event.ToString(Formatting.None));
            }
            diagnosticSink?.Invoke(stepCoordinator.AwaitingStop != null
                ? "stop-step-bypassed-native"
                : "stop-exception-bypassed-native");
            return ClientOnly(TransformAdapterToClientCore(
                pendingStepStop.OriginalPayload));
        }

        if (exceptionCoordinator.TryHandleAdapterResponse(
            message,
            () => nextProxyRequestSequence++,
            out var exceptionDispatch))
        {
            return exceptionDispatch;
        }
        if (stackExpansionCoordinator.TryHandleEvaluationResponse(
            message,
            () => nextProxyRequestSequence++,
            out var stackDispatch))
        {
            return stackDispatch;
        }

        PendingStackTrace? stackRequest = null;
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "stackTrace", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int stackRequestSequence)
        {
            stackState.PendingStackTraces.TryGetValue(stackRequestSequence, out stackRequest);
        }

        if (variableResponseCoordinator.TryBeginManagedScalarMaterialization(
            message,
            () => nextProxyRequestSequence++,
            out var scalarDispatch))
        {
            return scalarDispatch;
        }

        var transformed = TransformAdapterToClientCore(payload);
        return stackExpansionCoordinator.BeginProjection(
            transformed,
            stackRequest,
            () => nextProxyRequestSequence++);
    }

    private static DapTransformDispatch ClientOnly(string payload) =>
        new DapTransformDispatch(new[] { payload }, Array.Empty<string>());

    private string TransformAdapterToClientCore(string payload)
    {
        JObject message;
        try
        {
            message = JObject.Parse(payload);
        }
        catch (JsonException)
        {
            return payload;
        }

        if ((string?)message["type"] == "response"
            && (string?)message["command"] == "initialize"
            && (bool?)message["success"] == true)
        {
            if (message["body"] is not JObject initializeBody)
            {
                initializeBody = new JObject();
                message["body"] = initializeBody;
            }
            exceptionCoordinator.AddCapabilities(initializeBody);
            initializeBody["supportsEvaluateForHovers"] = true;
            initializeBody["supportsSetVariable"] = true;
            diagnosticSink?.Invoke("exception-capabilities-composed");
            return message.ToString(Formatting.None);
        }

        if (string.Equals(message.Value<string>("type"), "event", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("event"), "stopped", StringComparison.Ordinal))
        {
            TrackStoppedBreakpoint(message);
            return stackState.ActiveBinding != null
                ? message.ToString(Formatting.None)
                : payload;
        }
        if (string.Equals(message.Value<string>("type"), "event", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("event"), "continued", StringComparison.Ordinal))
        {
            stackState.ResetForContinuation();
            exceptionCoordinator.ClearActiveException();
            variableState.ResetForSuspension();
            return payload;
        }
        if (variableResponseCoordinator.TryTransformAdapterResponse(
            message,
            out var variableResponse))
        {
            return variableResponse;
        }
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "stackTrace", StringComparison.Ordinal))
        {
            return stackRequestCoordinator.TransformAdapterResponse(
                payload,
                message,
                runtimeCoordinator);
        }
        return breakpointCoordinator.TransformAdapterMessage(payload, message);
    }

    private void TrackStoppedBreakpoint(JObject message)
    {
        variableState.ResetForSuspension();
        // A stop begins a new suspension even when the underlying adapter did not
        // emit the optional continued event for the preceding resume. Do not let
        // Exception Helper requests at a later native stop observe stale managed
        // exception details from the prior suspension.
        exceptionCoordinator.ClearActiveException();
        if (stackRequestCoordinator.TrackBreakpointStop(
            message,
            breakpointCoordinator))
        {
            stepCoordinator.ClearAwaitingStop();
        }
    }

    internal static string BuildManagedLocalsExpression(ManagedFrameLayout layout) =>
        ManagedVariableExpressionBuilder.BuildManagedLocalsExpression(layout);
}
