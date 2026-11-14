using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedRuntimeCoordinator
{
    private const string RuntimeBuildMismatchResultPrefix =
        "__frontendcsharp_build_mismatch__:";
    private readonly IReadOnlyList<DapBreakpointCompositionCandidate> candidates;
    private readonly Action<string>? diagnosticSink;
    private readonly HashSet<int> pendingContextSelections = new HashSet<int>();
    private readonly HashSet<int> pendingConfigurations = new HashSet<int>();
    private readonly HashSet<int> abandonedConfigurationRequests = new HashSet<int>();
    private string? expectedBuildId;
    private bool sourceLoaded;
    private bool reloadPending;
    private bool configurationStarted;

    internal ManagedRuntimeCoordinator(
        IReadOnlyList<DapBreakpointCompositionCandidate> candidates,
        string? initialBuildId,
        Action<string>? diagnosticSink)
    {
        this.candidates = candidates;
        this.diagnosticSink = diagnosticSink;
        var initial = candidates[0];
        Resolver = initial.Resolver;
        RuntimeSourcePath = initial.RuntimeSourcePath;
        RuntimeLine = initial.RuntimeLine;
        expectedBuildId = initialBuildId;
    }

    internal IManagedBreakpointResolver Resolver { get; private set; }
    internal string RuntimeSourcePath { get; private set; }
    internal int RuntimeLine { get; private set; }

    internal void SelectForSource(string sourcePath)
    {
        var candidate = candidates.FirstOrDefault(item => item.Resolver.OwnsSource(sourcePath));
        if (candidate != null)
        {
            Select(candidate);
        }
    }

    internal bool OwnsRuntimeSourcePath(string? sourcePath) =>
        PathsEqual(sourcePath, RuntimeSourcePath);

    internal bool TryHandleAdapterMessage(
        JObject message,
        string originalPayload,
        IEnumerable<string> exceptionFilters,
        Func<int> allocateSequence,
        Func<string, string> transformAdapterToClient,
        out DapTransformDispatch dispatch)
    {
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int abandonedSequence
            && abandonedConfigurationRequests.Remove(abandonedSequence))
        {
            dispatch = EmptyDispatch();
            return true;
        }

        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int contextSequence
            && pendingContextSelections.Remove(contextSequence))
        {
            if (message.Value<bool?>("success") != true)
            {
                diagnosticSink?.Invoke("runtime-context-selection-failed");
                dispatch = EmptyDispatch();
                return true;
            }

            var configurationSequence = allocateSequence();
            pendingConfigurations.Add(configurationSequence);
            diagnosticSink?.Invoke("runtime-context-selection-complete");
            diagnosticSink?.Invoke("runtime-configuration-started");
            dispatch = new DapTransformDispatch(
                Array.Empty<string>(),
                new[]
                {
                    BuildConfigurationRequest(configurationSequence, exceptionFilters)
                });
            return true;
        }

        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int configurationResponseSequence
            && pendingConfigurations.Remove(configurationResponseSequence))
        {
            var result = message["body"]?.Value<string>("result");
            diagnosticSink?.Invoke(message.Value<bool?>("success") != true
                ? "runtime-configuration-failed"
                : result != null && result.StartsWith(
                    RuntimeBuildMismatchResultPrefix,
                    StringComparison.Ordinal)
                    ? "runtime-configuration-skipped-build-mismatch"
                    : "runtime-configuration-complete");
            dispatch = EmptyDispatch();
            return true;
        }

        if (IsRemovedSource(message))
        {
            sourceLoaded = false;
            reloadPending = true;
            if (configurationStarted)
            {
                abandonedConfigurationRequests.UnionWith(pendingContextSelections);
                abandonedConfigurationRequests.UnionWith(pendingConfigurations);
                pendingContextSelections.Clear();
                pendingConfigurations.Clear();
                configurationStarted = false;
            }
        }
        SelectForRuntimeSource(message);
        if (IsLoadedSource(message))
        {
            sourceLoaded = true;
            if (reloadPending)
            {
                reloadPending = false;
                diagnosticSink?.Invoke("runtime-source-reloaded");
            }
        }
        if (!configurationStarted && expectedBuildId != null && sourceLoaded)
        {
            configurationStarted = true;
            var contextRequestSequence = allocateSequence();
            pendingContextSelections.Add(contextRequestSequence);
            diagnosticSink?.Invoke("runtime-context-selection-started");
            dispatch = new DapTransformDispatch(
                new[] { transformAdapterToClient(originalPayload) },
                new[] { BuildContextSelectionRequest(contextRequestSequence) });
            return true;
        }

        dispatch = null!;
        return false;
    }

    private void SelectForRuntimeSource(JObject message)
    {
        if (!string.Equals(message.Value<string>("type"), "event", StringComparison.Ordinal)
            || !string.Equals(message.Value<string>("event"), "loadedSource", StringComparison.Ordinal))
        {
            return;
        }
        var sourcePath = message["body"]?["source"]?.Value<string>("path");
        var candidate = candidates.FirstOrDefault(item =>
            PathsEqual(sourcePath, item.RuntimeSourcePath));
        if (candidate != null)
        {
            Select(candidate);
        }
    }

    private void Select(DapBreakpointCompositionCandidate candidate)
    {
        if (ReferenceEquals(Resolver, candidate.Resolver) || configurationStarted)
        {
            return;
        }
        Resolver = candidate.Resolver;
        RuntimeSourcePath = candidate.RuntimeSourcePath;
        RuntimeLine = candidate.RuntimeLine;
        expectedBuildId = candidate.BuildId;
        diagnosticSink?.Invoke("debugger-build-candidate-selected");
    }

    private bool IsLoadedSource(JObject message) =>
        string.Equals(message.Value<string>("type"), "event", StringComparison.Ordinal)
        && string.Equals(message.Value<string>("event"), "loadedSource", StringComparison.Ordinal)
        && !string.Equals(
            message["body"]?.Value<string>("reason"),
            "removed",
            StringComparison.Ordinal)
        && OwnsRuntimeSourcePath(message["body"]?["source"]?.Value<string>("path"));

    private bool IsRemovedSource(JObject message) =>
        string.Equals(message.Value<string>("type"), "event", StringComparison.Ordinal)
        && string.Equals(message.Value<string>("event"), "loadedSource", StringComparison.Ordinal)
        && string.Equals(
            message["body"]?.Value<string>("reason"),
            "removed",
            StringComparison.Ordinal)
        && OwnsRuntimeSourcePath(message["body"]?["source"]?.Value<string>("path"));

    private string BuildConfigurationRequest(
        int sequence,
        IEnumerable<string> exceptionFilters)
    {
        var configuration = new JObject
        {
            ["buildId"] = expectedBuildId,
            ["breakpoints"] = new JArray(),
            ["exceptionFilters"] = new JArray(
                exceptionFilters.OrderBy(item => item, StringComparer.Ordinal))
        }.ToString(Formatting.None);
        return new JObject
        {
            ["seq"] = sequence,
            ["type"] = "request",
            ["command"] = "evaluate",
            ["arguments"] = new JObject
            {
                ["expression"] = "(async()=>{const configuration=" + configuration
                    + ";const activeBuildId=globalThis.__FrontEndCSharpBuildIdentity?.buildId??null;"
                    + "if(activeBuildId!==configuration.buildId)return '"
                    + RuntimeBuildMismatchResultPrefix
                    + "'+String(activeBuildId);"
                    + "const runtime=globalThis.__FrontEndCSharpDebugger;"
                    + "if(runtime)return await runtime.configure(configuration);"
                    + "globalThis.__FrontEndCSharpDebuggerInitialConfiguration=configuration;"
                    + "return {buildId:configuration.buildId,pending:true};})()",
                ["context"] = "watch"
            }
        }.ToString(Formatting.None);
    }

    private static string BuildContextSelectionRequest(int sequence) => new JObject
    {
        ["seq"] = sequence,
        ["type"] = "request",
        ["command"] = "evaluate",
        ["arguments"] = new JObject
        {
            ["expression"] = "cd top",
            ["context"] = "repl"
        }
    }.ToString(Formatting.None);

    private static DapTransformDispatch EmptyDispatch() =>
        new DapTransformDispatch(Array.Empty<string>(), Array.Empty<string>());

    private static bool PathsEqual(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return false;
        }
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is NotSupportedException
            || exception is PathTooLongException)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
