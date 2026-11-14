using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedVariableRequestCoordinator
{
    private readonly ManagedVariableState state;
    private readonly Action<string>? diagnosticSink;

    internal ManagedVariableRequestCoordinator(
        ManagedVariableState state,
        Action<string>? diagnosticSink)
    {
        this.state = state;
        this.diagnosticSink = diagnosticSink;
    }

    internal bool TryTransformClientRequest(
        string payload,
        IManagedBreakpointResolver resolver,
        IReadOnlyDictionary<int, LogicalFrameProjection> logicalFrames,
        IReadOnlyDictionary<int, BreakpointBinding> projectedFrames,
        out string transformed)
    {
        if (TryTransformSetVariableRequest(payload, out transformed))
        {
            return true;
        }
        return TryTransformInspectionRequest(
            payload,
            resolver,
            logicalFrames,
            projectedFrames,
            out transformed);
    }

    private bool TryTransformInspectionRequest(
        string payload,
        IManagedBreakpointResolver resolver,
        IReadOnlyDictionary<int, LogicalFrameProjection> logicalFrames,
        IReadOnlyDictionary<int, BreakpointBinding> projectedFrames,
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
            || message.Value<int?>("seq") is not int sequence)
        {
            return false;
        }

        var command = message.Value<string>("command");
        var arguments = message["arguments"] as JObject;
        if (string.Equals(command, "evaluate", StringComparison.Ordinal)
            && IsManagedHoverRequest(arguments)
            && arguments?.Value<string>("expression") is string hoverExpression
            && arguments.Value<int?>("frameId") is int hoverFrameId)
        {
            if (logicalFrames.TryGetValue(hoverFrameId, out var logicalHoverFrame))
            {
                var sourcePath = logicalHoverFrame.Binding.Source.Value<string>("path")
                    ?? throw new InvalidOperationException(
                        "The logical managed source path is unavailable.");
                var layout = resolver.ResolveFrameLayout(
                    sourcePath,
                    logicalHoverFrame.Binding.Resolution);
                if (ManagedVariableExpressionBuilder.TryResolveManagedHoverName(
                    layout,
                    hoverExpression,
                    out var managedHoverName))
                {
                    arguments["expression"] =
                        ManagedVariableExpressionBuilder.BuildManagedHoverExpression(
                            ManagedVariableExpressionBuilder.BuildManagedParentLocalsExpression(
                                layout,
                                logicalHoverFrame.Index,
                                logicalHoverFrame.IsSnapshot),
                            managedHoverName);
                    arguments["frameId"] = logicalHoverFrame.BackingFrameId;
                    state.PendingHovers[sequence] = logicalHoverFrame.IsSnapshot;
                    diagnosticSink?.Invoke("hover-logical-request-correlated");
                    transformed = message.ToString(Formatting.None);
                    return true;
                }
            }
            else if (projectedFrames.TryGetValue(hoverFrameId, out var hoverBinding))
            {
                var sourcePath = hoverBinding.Source.Value<string>("path")
                    ?? throw new InvalidOperationException(
                        "The managed source path is unavailable.");
                var layout = resolver.ResolveFrameLayout(sourcePath, hoverBinding.Resolution);
                if (ManagedVariableExpressionBuilder.TryResolveManagedHoverName(
                    layout,
                    hoverExpression,
                    out var managedHoverName))
                {
                    arguments["expression"] =
                        ManagedVariableExpressionBuilder.BuildManagedHoverExpression(
                            ManagedVariableExpressionBuilder.BuildManagedLocalsExpression(layout),
                            managedHoverName);
                    state.PendingHovers[sequence] = false;
                    diagnosticSink?.Invoke("hover-request-correlated");
                    transformed = message.ToString(Formatting.None);
                    return true;
                }
            }
        }
        if (string.Equals(command, "scopes", StringComparison.Ordinal)
            && arguments?.Value<int?>("frameId") is int logicalFrameId
            && logicalFrames.TryGetValue(logicalFrameId, out var logicalFrame))
        {
            state.PendingInspections[sequence] = logicalFrame.IsSnapshot;
            var sourcePath = logicalFrame.Binding.Source.Value<string>("path")
                ?? throw new InvalidOperationException(
                    "The logical managed source path is unavailable.");
            var layout = resolver.ResolveFrameLayout(sourcePath, logicalFrame.Binding.Resolution);
            message["command"] = "evaluate";
            message["arguments"] = new JObject
            {
                ["expression"] =
                    ManagedVariableExpressionBuilder.BuildManagedParentLocalsExpression(
                        layout,
                        logicalFrame.Index,
                        logicalFrame.IsSnapshot),
                ["frameId"] = logicalFrame.BackingFrameId,
                ["context"] = "watch"
            };
            diagnosticSink?.Invoke("scopes-logical-request-correlated");
            transformed = message.ToString(Formatting.None);
            return true;
        }
        if (string.Equals(command, "scopes", StringComparison.Ordinal)
            && arguments?.Value<int?>("frameId") is int frameId
            && projectedFrames.TryGetValue(frameId, out var binding))
        {
            state.PendingInspections[sequence] = false;
            var sourcePath = binding.Source.Value<string>("path")
                ?? throw new InvalidOperationException("The managed source path is unavailable.");
            var layout = resolver.ResolveFrameLayout(sourcePath, binding.Resolution);
            message["command"] = "evaluate";
            message["arguments"] = new JObject
            {
                ["expression"] = ManagedVariableExpressionBuilder.BuildManagedLocalsExpression(layout),
                ["frameId"] = frameId,
                ["context"] = "watch"
            };
            diagnosticSink?.Invoke("scopes-request-correlated");
            transformed = message.ToString(Formatting.None);
            return true;
        }
        if (string.Equals(command, "variables", StringComparison.Ordinal)
            && arguments?.Value<int?>("variablesReference") is int variablesReference
            && state.LiveReferences.Contains(variablesReference))
        {
            state.PendingVariableRequests[sequence] = variablesReference;
            diagnosticSink?.Invoke("variables-request-correlated");
            return true;
        }

        return false;
    }

    private bool TryTransformSetVariableRequest(string payload, out string transformed)
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
            || !string.Equals(message.Value<string>("command"), "setVariable", StringComparison.Ordinal)
            || message.Value<int?>("seq") is not int sequence
            || message["arguments"] is not JObject arguments
            || arguments.Value<int?>("variablesReference") is not int variablesReference
            || arguments.Value<string>("name") is not string name)
        {
            return false;
        }

        if (state.LiveReferences.Contains(variablesReference))
        {
            if (state.ImmutableReferences.Contains(variablesReference))
            {
                transformed = RejectManagedSetVariable(
                    message,
                    sequence,
                    "Async logical-frame snapshots are immutable.");
                return true;
            }
            if (!state.RuntimeNames.TryGetValue(
                new ManagedVariableIdentity(variablesReference, name),
                out var runtimeName))
            {
                transformed = RejectManagedSetVariable(
                    message,
                    sequence,
                    "This managed value does not have a writable live storage location.");
                return true;
            }
            arguments["name"] = runtimeName;
            diagnosticSink?.Invoke("set-variable-request-correlated");
            transformed = message.ToString(Formatting.None);
            return true;
        }
        if (state.StaleReferences.Contains(variablesReference))
        {
            transformed = RejectManagedSetVariable(
                message,
                sequence,
                "The requested managed variables are stale or no longer available.");
            return true;
        }
        return false;
    }

    private string RejectManagedSetVariable(JObject message, int sequence, string error)
    {
        state.PendingSetErrors[sequence] = error;
        message["command"] = "evaluate";
        message["arguments"] = new JObject
        {
            ["expression"] = "(()=>{throw new Error("
                + JsonConvert.SerializeObject(error)
                + ");})()",
            ["context"] = "watch"
        };
        diagnosticSink?.Invoke("set-variable-request-rejected");
        return message.ToString(Formatting.None);
    }

    private static bool IsManagedHoverRequest(JObject? arguments)
    {
        if (arguments == null)
        {
            return false;
        }
        var context = arguments.Value<string>("context");
        return string.Equals(context, "hover", StringComparison.Ordinal)
            || string.Equals(context, "watch", StringComparison.Ordinal)
            || (context == null
                && (arguments["evaluationOptions"]?.Value<bool?>("noSideEffects") == true
                    || arguments["evaluationOptions"]?.Value<bool?>("allowImplicitVars") == true));
    }
}
