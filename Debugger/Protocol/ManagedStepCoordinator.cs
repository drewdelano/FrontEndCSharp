using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedStepCoordinator
{
    private readonly Action<string>? diagnosticSink;
    private readonly Dictionary<int, PendingManagedStep> pendingArms =
        new Dictionary<int, PendingManagedStep>();
    private readonly Dictionary<int, PendingManagedStep> pendingFrameLookups =
        new Dictionary<int, PendingManagedStep>();
    private readonly Dictionary<int, PendingManagedStep> pendingContinues =
        new Dictionary<int, PendingManagedStep>();

    internal ManagedStepCoordinator(Action<string>? diagnosticSink)
    {
        this.diagnosticSink = diagnosticSink;
    }

    internal PendingManagedStep? AwaitingStop { get; private set; }

    internal void ClearAwaitingStop()
    {
        AwaitingStop = null;
    }

    internal bool TryTransformClientRequest(
        string payload,
        BreakpointBinding? activeBinding,
        int? activeFrameId,
        IManagedBreakpointResolver resolver,
        out string transformed)
    {
        transformed = payload;
        JObject message;
        try
        {
            message = JObject.Parse(payload);
        }
        catch (JsonException)
        {
            return false;
        }
        if (!string.Equals(message.Value<string>("type"), "request", StringComparison.Ordinal)
            || message.Value<int?>("seq") is not int originalSequence
            || activeBinding == null)
        {
            return false;
        }

        var command = message.Value<string>("command");
        var kind = string.Equals(command, "stepIn", StringComparison.Ordinal)
            ? "in"
            : string.Equals(command, "next", StringComparison.Ordinal)
                ? "over"
                : string.Equals(command, "stepOut", StringComparison.Ordinal)
                    ? "out"
                    : null;
        if (kind == null)
        {
            return false;
        }

        var sourcePath = activeBinding.Source.Value<string>("path")
            ?? throw new InvalidOperationException("The managed source path is unavailable.");
        var locations = resolver.ResolveStepLocations(sourcePath);
        var serializedLocations = new JArray(locations.Select(location => new JObject
        {
            ["moduleId"] = location.ModuleId,
            ["methodToken"] = location.MethodToken,
            ["blockIndex"] = location.BlockIndex,
            ["instructionIndex"] = location.InstructionIndex
        })).ToString(Formatting.None);
        var threadId = message["arguments"]?.Value<int?>("threadId") ?? 0;
        var pendingStep = new PendingManagedStep(
            originalSequence,
            command!,
            threadId,
            activeBinding,
            string.Format(
                CultureInfo.InvariantCulture,
                "(()=>{{const debuggerRuntime=globalThis.__FrontEndCSharpDebugger;const ctx=typeof opts==='object'&&opts?.ctx?opts.ctx:debuggerRuntime?.currentStop?.frame?.ctx;const baselineDepth=ctx?.debugDepth+(Array.isArray(ctx?.asyncParents)?ctx.asyncParents.length:0);const locations={0};debuggerRuntime.requestStep({1},baselineDepth,locations);}})()",
                serializedLocations,
                JsonConvert.SerializeObject(kind)));
        if (!activeFrameId.HasValue)
        {
            pendingFrameLookups[originalSequence] = pendingStep;
            message["command"] = "stackTrace";
            message["arguments"] = new JObject
            {
                ["threadId"] = threadId,
                ["startFrame"] = 0
            };
            diagnosticSink?.Invoke("step-frame-lookup-started");
            transformed = message.ToString(Formatting.None);
            return true;
        }

        pendingArms[originalSequence] = pendingStep;
        message["command"] = "evaluate";
        message["arguments"] = BuildArmArguments(pendingStep, activeFrameId.Value);
        diagnosticSink?.Invoke("step-request-correlated");
        transformed = message.ToString(Formatting.None);
        return true;
    }

    internal bool TryHandleAdapterResponse(
        JObject message,
        ManagedRuntimeCoordinator runtime,
        Action<int> setActiveFrameId,
        Func<int> allocateSequence,
        out DapTransformDispatch dispatch)
    {
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "stackTrace", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int frameSequence
            && pendingFrameLookups.TryGetValue(frameSequence, out var frameStep))
        {
            pendingFrameLookups.Remove(frameSequence);
            var runtimeFrame = (message["body"]?["stackFrames"] as JArray)?
                .Children<JObject>()
                .FirstOrDefault(frame =>
                    frame.Value<int?>("id").HasValue
                    && runtime.OwnsRuntimeSourcePath(
                        frame["source"]?.Value<string>("path")));
            if (message.Value<bool?>("success") != true || runtimeFrame == null)
            {
                message["command"] = frameStep.Command;
                message["request_seq"] = frameStep.OriginalSequence;
                message["success"] = false;
                message["message"] =
                    "FrontEndCSharp could not locate the paused managed runtime frame.";
                message.Remove("body");
                diagnosticSink?.Invoke("step-frame-lookup-failed");
                dispatch = ClientOnly(message.ToString(Formatting.None));
                return true;
            }

            var frameId = runtimeFrame.Value<int>("id");
            setActiveFrameId(frameId);
            var newArmSequence = allocateSequence();
            pendingArms[newArmSequence] = frameStep;
            diagnosticSink?.Invoke("step-frame-lookup-complete");
            dispatch = new DapTransformDispatch(
                Array.Empty<string>(),
                new[] { BuildArmRequest(newArmSequence, frameStep, frameId) });
            return true;
        }

        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int armResponseSequence
            && pendingArms.TryGetValue(armResponseSequence, out var pendingStep))
        {
            pendingArms.Remove(armResponseSequence);
            if (message.Value<bool?>("success") != true)
            {
                message["command"] = pendingStep.Command;
                message["request_seq"] = pendingStep.OriginalSequence;
                message["message"] = "FrontEndCSharp could not arm managed stepping.";
                message.Remove("body");
                diagnosticSink?.Invoke("step-arm-failed");
                dispatch = ClientOnly(message.ToString(Formatting.None));
                return true;
            }
            var newContinueSequence = allocateSequence();
            pendingContinues[newContinueSequence] = pendingStep;
            AwaitingStop = pendingStep;
            diagnosticSink?.Invoke("step-arm-complete");
            dispatch = new DapTransformDispatch(
                Array.Empty<string>(),
                new[]
                {
                    new JObject
                    {
                        ["seq"] = newContinueSequence,
                        ["type"] = "request",
                        ["command"] = "continue",
                        ["arguments"] = new JObject
                        {
                            ["threadId"] = pendingStep.ThreadId,
                            ["singleThread"] = false
                        }
                    }.ToString(Formatting.None)
                });
            return true;
        }

        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "continue", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int continueResponseSequence
            && pendingContinues.TryGetValue(continueResponseSequence, out pendingStep))
        {
            pendingContinues.Remove(continueResponseSequence);
            message["command"] = pendingStep.Command;
            message["request_seq"] = pendingStep.OriginalSequence;
            message.Remove("body");
            if (message.Value<bool?>("success") == true)
            {
                diagnosticSink?.Invoke("step-resume-complete");
            }
            else
            {
                if (ReferenceEquals(AwaitingStop, pendingStep))
                {
                    AwaitingStop = null;
                }
                message["message"] =
                    "FrontEndCSharp could not resume after arming managed stepping.";
                diagnosticSink?.Invoke("step-resume-failed");
            }
            dispatch = ClientOnly(message.ToString(Formatting.None));
            return true;
        }

        dispatch = null!;
        return false;
    }

    private static string BuildArmRequest(
        int sequence,
        PendingManagedStep step,
        int frameId) => new JObject
    {
        ["seq"] = sequence,
        ["type"] = "request",
        ["command"] = "evaluate",
        ["arguments"] = BuildArmArguments(step, frameId)
    }.ToString(Formatting.None);

    private static JObject BuildArmArguments(PendingManagedStep step, int frameId) =>
        new JObject
        {
            ["expression"] = step.Expression,
            ["frameId"] = frameId,
            ["context"] = "watch"
        };

    private static DapTransformDispatch ClientOnly(string payload) =>
        new DapTransformDispatch(new[] { payload }, Array.Empty<string>());
}
