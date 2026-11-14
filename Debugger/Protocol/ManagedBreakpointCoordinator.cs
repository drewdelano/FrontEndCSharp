using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedBreakpointCoordinator
{
    private readonly Action<string>? diagnosticSink;
    private readonly Dictionary<string, SourceBreakpoints> sources =
        new Dictionary<string, SourceBreakpoints>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, PendingRequest> pending =
        new Dictionary<int, PendingRequest>();
    private readonly Dictionary<string, BreakpointBindingGroup> bindings =
        new Dictionary<string, BreakpointBindingGroup>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> logicalBreakpointIds =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private int nextProxyBreakpointId = 1800000000;

    internal ManagedBreakpointCoordinator(Action<string>? diagnosticSink)
    {
        this.diagnosticSink = diagnosticSink;
    }

    internal IReadOnlyList<string> SourcePaths => sources.Values
        .Select(source => source.Source.Value<string>("path"))
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => path!)
        .ToList();

    internal string TransformClientRequest(
        JObject message,
        JObject arguments,
        string sourcePath,
        IManagedBreakpointResolver resolver,
        string runtimeSourcePath,
        int runtimeLine)
    {
        var source = (JObject)arguments["source"]!;
        var requestSequence = message.Value<int>("seq");
        var requested = new List<RequestedBreakpoint>();
        foreach (var breakpoint in arguments["breakpoints"] as JArray ?? new JArray())
        {
            var requestedLine = breakpoint.Value<int?>("line") ?? 0;
            var resolution = requestedLine > 0
                ? resolver.Resolve(sourcePath, requestedLine)
                : ManagedBreakpointResolution.Unresolved(
                    "A managed breakpoint requires a positive source line.",
                    requestedLine);
            requested.Add(new RequestedBreakpoint(requestedLine, resolution));
        }
        diagnosticSink?.Invoke(
            requested.Count == 0
                ? "request-owned-empty"
                : requested.All(item => item.Resolution.IsResolved)
                    ? "request-owned-all-resolved"
                    : "request-owned-some-unresolved");

        var sourceKey = CanonicalSourceKey(sourcePath);
        sources[sourceKey] = new SourceBreakpoints((JObject)source.DeepClone(), requested);

        var aggregate = sources
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(item => item.Value.Breakpoints
                .Where(breakpoint => breakpoint.Resolution.IsResolved)
                .Select(breakpoint => new AggregateBreakpoint(
                    item.Key,
                    item.Value.Source,
                    breakpoint)))
            .ToList();
        var currentIndexes = aggregate
            .Where(item => string.Equals(
                item.SourceKey,
                sourceKey,
                StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Breakpoint, _ => 0);
        var aggregateBindings = aggregate.Select(item => new BreakpointBinding(
            item.Source,
            item.Breakpoint.Resolution,
            GetLogicalBreakpointId(item.SourceKey, item.Breakpoint.Resolution))).ToList();
        var currentBindings = aggregate
            .Select((item, index) => new AggregateBreakpointBinding(
                item,
                aggregateBindings[index]))
            .Where(item => string.Equals(
                item.Breakpoint.SourceKey,
                sourceKey,
                StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Breakpoint.Breakpoint, item => item.Binding);
        pending[requestSequence] = new PendingRequest(
            (JObject)source.DeepClone(),
            requested.Select(item => new PendingBreakpoint(
                item,
                currentIndexes.TryGetValue(item, out var index) ? (int?)index : null,
                currentBindings.TryGetValue(item, out var binding)
                    ? binding.LogicalId
                    : null)).ToList(),
            aggregateBindings.Count == 0
                ? Array.Empty<BreakpointBindingGroup>()
                : new[] { new BreakpointBindingGroup(aggregateBindings) });

        arguments["source"] = new JObject
        {
            ["name"] = Path.GetFileName(runtimeSourcePath),
            ["path"] = runtimeSourcePath
        };
        arguments["breakpoints"] = aggregate.Count == 0
            ? new JArray()
            : new JArray(new JObject
            {
                ["line"] = runtimeLine,
                ["column"] = 1,
                ["condition"] = aggregate.Count == 1
                    ? BuildCondition(aggregate[0].Breakpoint.Resolution)
                    : string.Join(
                        " || ",
                        aggregate.Select(item =>
                            "(" + BuildCondition(item.Breakpoint.Resolution) + ")"))
            });
        arguments.Remove("lines");
        return message.ToString(Formatting.None);
    }

    internal string TransformAdapterMessage(string payload, JObject message)
    {
        if (!string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            || !string.Equals(
                message.Value<string>("command"),
                "setBreakpoints",
                StringComparison.Ordinal))
        {
            return TransformBreakpointEvent(payload, message);
        }
        if (message.Value<int?>("request_seq") is not int requestSequence
            || !pending.TryGetValue(requestSequence, out var request))
        {
            return payload;
        }

        pending.Remove(requestSequence);
        var adapterBreakpoints = (message["body"] as JObject)?["breakpoints"] as JArray
            ?? new JArray();
        diagnosticSink?.Invoke(
            adapterBreakpoints.Count == 0
                ? "response-empty"
                : adapterBreakpoints.Children<JObject>().All(item =>
                    item.Value<bool?>("verified") == true)
                    ? "response-all-verified"
                    : "response-some-unverified");
        for (var index = 0;
            index < adapterBreakpoints.Count && index < request.Aggregate.Count;
            index++)
        {
            if (adapterBreakpoints[index] is JObject adapter
                && adapter["id"] is JToken id)
            {
                bindings[id.ToString(Formatting.None)] = request.Aggregate[index];
            }
        }

        var projected = new JArray();
        foreach (var breakpoint in request.Breakpoints)
        {
            if (!breakpoint.AggregateIndex.HasValue)
            {
                projected.Add(new JObject
                {
                    ["verified"] = false,
                    ["message"] = breakpoint.Request.Resolution.Error,
                    ["source"] = request.Source.DeepClone(),
                    ["line"] = breakpoint.Request.RequestedLine,
                    ["column"] = 1
                });
                continue;
            }

            var adapter = breakpoint.AggregateIndex.Value < adapterBreakpoints.Count
                ? adapterBreakpoints[breakpoint.AggregateIndex.Value] as JObject
                : null;
            var resolution = breakpoint.Request.Resolution;
            var result = adapter == null
                ? new JObject
                {
                    ["verified"] = false,
                    ["message"] = "The JavaScript debugger did not return the projected managed breakpoint."
                }
                : (JObject)adapter.DeepClone();
            result["source"] = request.Source.DeepClone();
            result["line"] = resolution.Line;
            result["column"] = resolution.Column;
            if (breakpoint.LogicalId.HasValue)
            {
                result["id"] = breakpoint.LogicalId.Value;
            }
            projected.Add(result);
        }

        var body = message["body"] as JObject ?? new JObject();
        body["breakpoints"] = projected;
        message["body"] = body;
        return message.ToString(Formatting.None);
    }

    internal bool TryTransformBreakpointEvents(
        JObject message,
        out IReadOnlyList<string> events)
    {
        events = Array.Empty<string>();
        if (!string.Equals(message.Value<string>("type"), "event", StringComparison.Ordinal)
            || !string.Equals(message.Value<string>("event"), "breakpoint", StringComparison.Ordinal)
            || message["body"]?["breakpoint"] is not JObject breakpoint
            || breakpoint["id"] is not JToken id
            || !bindings.TryGetValue(id.ToString(Formatting.None), out var bindingGroup))
        {
            return false;
        }

        events = bindingGroup.Bindings.Select(binding =>
        {
            var projected = (JObject)message.DeepClone();
            var projectedBreakpoint = (JObject)projected["body"]!["breakpoint"]!;
            if (binding.LogicalId.HasValue)
            {
                projectedBreakpoint["id"] = binding.LogicalId.Value;
            }
            projectedBreakpoint["source"] = binding.Source.DeepClone();
            projectedBreakpoint["line"] = binding.Resolution.Line;
            projectedBreakpoint["column"] = binding.Resolution.Column;
            return projected.ToString(Formatting.None);
        }).ToArray();
        diagnosticSink?.Invoke(
            breakpoint.Value<bool?>("verified") == true
                ? "event-verified"
                : "event-unverified");
        return true;
    }

    internal bool TryGetBinding(string id, out BreakpointBindingGroup binding)
    {
        if (bindings.TryGetValue(id, out var found))
        {
            binding = found;
            return true;
        }

        binding = null!;
        return false;
    }

    internal static bool TryParseSetBreakpointsRequest(
        string payload,
        out JObject message,
        out JObject arguments)
    {
        try
        {
            message = JObject.Parse(payload);
        }
        catch (JsonException)
        {
            message = new JObject();
            arguments = new JObject();
            return false;
        }

        arguments = message["arguments"] as JObject ?? new JObject();
        return string.Equals(message.Value<string>("type"), "request", StringComparison.Ordinal)
            && string.Equals(
                message.Value<string>("command"),
                "setBreakpoints",
                StringComparison.Ordinal)
            && message["arguments"] is JObject;
    }

    private string TransformBreakpointEvent(string payload, JObject message)
    {
        if (!TryTransformBreakpointEvents(message, out var events))
        {
            return payload;
        }
        return events[0];
    }

    private int GetLogicalBreakpointId(
        string sourceKey,
        ManagedBreakpointResolution resolution)
    {
        var key = string.Format(
            CultureInfo.InvariantCulture,
            "{0}\0{1}\0{2}\0{3}\0{4}",
            sourceKey,
            resolution.ModuleId,
            resolution.MethodToken,
            resolution.BlockIndex,
            resolution.InstructionIndex);
        if (!logicalBreakpointIds.TryGetValue(key, out var id))
        {
            id = nextProxyBreakpointId++;
            logicalBreakpointIds.Add(key, id);
        }
        return id;
    }

    private static string BuildCondition(ManagedBreakpointResolution breakpoint)
    {
        var module = JsonConvert.SerializeObject(breakpoint.ModuleId);
        return string.Format(
            CultureInfo.InvariantCulture,
            "opts?.methodInfo?.debugModuleId === {0} && opts?.methodInfo?.debugMethodToken === {1} && opts?.currentBlockIndex === {2} && opts?.instructionIndex === {3}",
            module,
            breakpoint.MethodToken,
            breakpoint.BlockIndex,
            breakpoint.InstructionIndex);
    }

    private static string CanonicalSourceKey(string sourcePath)
    {
        try
        {
            return Path.GetFullPath(sourcePath);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is NotSupportedException
            || exception is PathTooLongException)
        {
            return sourcePath;
        }
    }
}
