using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedStackRequestCoordinator
{
    private readonly ManagedStackState state;
    private readonly Action<string>? diagnosticSink;

    internal ManagedStackRequestCoordinator(
        ManagedStackState state,
        Action<string>? diagnosticSink)
    {
        this.state = state;
        this.diagnosticSink = diagnosticSink;
    }

    internal bool TryTrackClientRequest(string payload)
    {
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
            || !string.Equals(message.Value<string>("command"), "stackTrace", StringComparison.Ordinal))
        {
            return false;
        }
        if (message.Value<int?>("seq") is int sequence && state.ActiveBinding != null)
        {
            var arguments = message["arguments"] as JObject;
            state.PendingStackTraces[sequence] = new PendingStackTrace(
                state.ActiveBinding,
                Math.Max(0, arguments?.Value<int?>("startFrame") ?? 0));
            diagnosticSink?.Invoke("stack-request-correlated");
        }
        else
        {
            diagnosticSink?.Invoke("stack-request-uncorrelated");
        }
        return true;
    }

    internal bool TrackBreakpointStop(
        JObject message,
        ManagedBreakpointCoordinator breakpointCoordinator)
    {
        state.ResetActiveStop();
        foreach (var id in message["body"]?["hitBreakpointIds"] as JArray ?? new JArray())
        {
            if (breakpointCoordinator.TryGetBinding(
                id.ToString(Formatting.None),
                out var bindingGroup))
            {
                state.ActiveBinding = bindingGroup.Bindings[0];
                state.ActiveStopNeedsIdentity = bindingGroup.Bindings.Count > 1;
                (message["body"] as JObject)?.Remove("hitBreakpointIds");
                diagnosticSink?.Invoke("stop-binding-correlated");
                return true;
            }
        }
        diagnosticSink?.Invoke("stop-binding-uncorrelated");
        return false;
    }

    internal string TransformAdapterResponse(
        string payload,
        JObject message,
        ManagedRuntimeCoordinator runtime)
    {
        if (message.Value<int?>("request_seq") is not int requestSequence
            || !state.PendingStackTraces.TryGetValue(requestSequence, out var request))
        {
            return payload;
        }
        state.PendingStackTraces.Remove(requestSequence);

        if (request.StartFrame == 0 && state.ActiveProjection != null)
        {
            diagnosticSink?.Invoke("stack-projection-deferred-to-stop");
            return payload;
        }
        if (request.StartFrame == 0 && state.ActiveStopNeedsIdentity)
        {
            diagnosticSink?.Invoke("stack-active-identity-required");
            return payload;
        }
        if (request.StartFrame > 0)
        {
            diagnosticSink?.Invoke("stack-page-deferred-to-identity");
            return message.ToString(Formatting.None);
        }

        var binding = request.Binding;
        var frames = (message["body"] as JObject)?["stackFrames"] as JArray ?? new JArray();
        var frame = frames.Children<JObject>().FirstOrDefault(item =>
            runtime.OwnsRuntimeSourcePath(item["source"]?.Value<string>("path")));
        if (frame == null)
        {
            diagnosticSink?.Invoke("stack-runtime-frame-not-found");
            return payload;
        }

        frame["source"] = binding.Source.DeepClone();
        frame["line"] = binding.Resolution.Line;
        frame["column"] = binding.Resolution.Column;
        if (frame.Value<int?>("id") is int frameId)
        {
            state.ProjectedFrames[frameId] = binding;
            state.ActiveFrameId = frameId;
        }
        diagnosticSink?.Invoke("stack-frame-projected");
        return message.ToString(Formatting.None);
    }
}
