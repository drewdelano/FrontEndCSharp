using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedExceptionCoordinator
{
    private const string ManagedThrownExceptionFilter = "frontendcsharp-thrown";
    private const string ManagedUnhandledExceptionFilter = "frontendcsharp-unhandled";
    private readonly Action<string>? diagnosticSink;
    private readonly Dictionary<int, PendingExceptionConfiguration> pendingConfigurations =
        new Dictionary<int, PendingExceptionConfiguration>();
    private readonly Dictionary<int, PendingExceptionConfiguration> pendingEvaluations =
        new Dictionary<int, PendingExceptionConfiguration>();
    private readonly Dictionary<int, ManagedExceptionStop> pendingExceptionInfo =
        new Dictionary<int, ManagedExceptionStop>();
    private readonly HashSet<string> filters = new HashSet<string>(StringComparer.Ordinal);

    internal ManagedExceptionCoordinator(Action<string>? diagnosticSink)
    {
        this.diagnosticSink = diagnosticSink;
    }

    internal bool HasFilters => filters.Count > 0;
    internal IEnumerable<string> Filters => filters;
    internal ManagedExceptionStop? ActiveException { get; private set; }

    internal void AddCapabilities(JObject initializeBody)
    {
        if (initializeBody["exceptionBreakpointFilters"] is not JArray exceptionFilters)
        {
            exceptionFilters = new JArray();
            initializeBody["exceptionBreakpointFilters"] = exceptionFilters;
        }
        AddFilter(
            exceptionFilters,
            ManagedThrownExceptionFilter,
            "FrontEndCSharp: thrown managed exceptions",
            isDefault: false);
        AddFilter(
            exceptionFilters,
            ManagedUnhandledExceptionFilter,
            "FrontEndCSharp: unhandled managed exceptions",
            isDefault: true);
        initializeBody["supportsExceptionInfoRequest"] = true;
    }

    internal void ClearActiveException()
    {
        ActiveException = null;
    }

    internal ManagedExceptionStop BeginExceptionStop(string stage, string type)
    {
        ActiveException = new ManagedExceptionStop(stage, type);
        return ActiveException;
    }

    internal bool TryTransformClientRequest(string payload, out string transformed)
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
        if (string.Equals(command, "exceptionInfo", StringComparison.Ordinal)
            && ActiveException != null)
        {
            pendingExceptionInfo[sequence] = ActiveException;
            message["command"] = "evaluate";
            message["arguments"] = new JObject
            {
                ["expression"] = "void 0",
                ["context"] = "watch"
            };
            diagnosticSink?.Invoke("exception-info-request-correlated");
            transformed = message.ToString(Formatting.None);
            return true;
        }
        if (!string.Equals(command, "setExceptionBreakpoints", StringComparison.Ordinal)
            || message["arguments"] is not JObject arguments)
        {
            return false;
        }

        var previouslyHadManagedFilters = filters.Count > 0;
        var requestedFilters = arguments["filters"] as JArray ?? new JArray();
        var requestedManaged = new HashSet<string>(StringComparer.Ordinal);
        foreach (var filterToken in requestedFilters.ToList())
        {
            var filter = filterToken.Value<string>();
            if (TryGetStage(filter, out var stage))
            {
                requestedManaged.Add(stage);
                filterToken.Remove();
            }
        }
        if (arguments["filterOptions"] is JArray filterOptions)
        {
            foreach (var option in filterOptions.Children<JObject>().ToList())
            {
                var filterId = option.Value<string>("filterId");
                if (TryGetStage(filterId, out var stage))
                {
                    requestedManaged.Add(stage);
                    option.Remove();
                }
            }
        }

        if (requestedManaged.Count == 0 && !previouslyHadManagedFilters)
        {
            return false;
        }

        pendingConfigurations[sequence] = new PendingExceptionConfiguration(
            requestedManaged.ToArray());
        diagnosticSink?.Invoke("exception-configuration-request-correlated");
        transformed = message.ToString(Formatting.None);
        return true;
    }

    internal bool TryHandleAdapterResponse(
        JObject message,
        Func<int> allocateSequence,
        out DapTransformDispatch dispatch)
    {
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(
                message.Value<string>("command"),
                "setExceptionBreakpoints",
                StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int configurationSequence
            && pendingConfigurations.TryGetValue(
                configurationSequence,
                out var pendingException))
        {
            pendingConfigurations.Remove(configurationSequence);
            pendingException.NativeResponse = message;
            if (message.Value<bool?>("success") != true)
            {
                diagnosticSink?.Invoke("exception-configuration-native-failed");
                dispatch = ClientOnly(message.ToString(Formatting.None));
                return true;
            }

            var runtimeSequence = allocateSequence();
            pendingEvaluations[runtimeSequence] = pendingException;
            diagnosticSink?.Invoke("exception-configuration-runtime-started");
            dispatch = new DapTransformDispatch(
                Array.Empty<string>(),
                new[]
                {
                    new JObject
                    {
                        ["seq"] = runtimeSequence,
                        ["type"] = "request",
                        ["command"] = "evaluate",
                        ["arguments"] = new JObject
                        {
                            ["expression"] = BuildConfigurationExpression(
                                pendingException.Filters),
                            ["context"] = "watch"
                        }
                    }.ToString(Formatting.None)
                });
            return true;
        }

        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int evaluationResponseSequence
            && pendingEvaluations.TryGetValue(
                evaluationResponseSequence,
                out var evaluatedException))
        {
            pendingEvaluations.Remove(evaluationResponseSequence);
            var response = evaluatedException.NativeResponse!;
            if (message.Value<bool?>("success") == true)
            {
                filters.Clear();
                filters.UnionWith(evaluatedException.Filters);
                diagnosticSink?.Invoke("exception-configuration-runtime-complete");
            }
            else
            {
                response["success"] = false;
                response["message"] =
                    "FrontEndCSharp could not configure managed exception stopping.";
                response.Remove("body");
                diagnosticSink?.Invoke("exception-configuration-runtime-failed");
            }
            dispatch = ClientOnly(response.ToString(Formatting.None));
            return true;
        }

        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int infoSequence
            && pendingExceptionInfo.TryGetValue(infoSequence, out var exceptionInfo))
        {
            pendingExceptionInfo.Remove(infoSequence);
            dispatch = ClientOnly(BuildInfoResponse(message, exceptionInfo));
            return true;
        }

        dispatch = null!;
        return false;
    }

    private static DapTransformDispatch ClientOnly(string payload) =>
        new DapTransformDispatch(new[] { payload }, Array.Empty<string>());

    private static void AddFilter(
        JArray filters,
        string filter,
        string label,
        bool isDefault)
    {
        if (filters.Children<JObject>().Any(item => string.Equals(
            item.Value<string>("filter"),
            filter,
            StringComparison.Ordinal)))
        {
            return;
        }
        filters.Add(new JObject
        {
            ["filter"] = filter,
            ["label"] = label,
            ["default"] = isDefault,
            ["supportsCondition"] = false
        });
    }

    private static string BuildConfigurationExpression(
        IReadOnlyCollection<string> filters)
    {
        var serialized = new JArray(filters.OrderBy(item => item, StringComparer.Ordinal))
            .ToString(Formatting.None);
        return "(()=>{const filters=" + serialized
            + ";const runtime=globalThis.__FrontEndCSharpDebugger;"
            + "if(runtime)runtime.setExceptionBreakpoints(filters);"
            + "else globalThis.__FrontEndCSharpManagedExceptionFilters=filters;})()";
    }

    private static bool TryGetStage(string? filter, out string stage)
    {
        if (string.Equals(filter, ManagedThrownExceptionFilter, StringComparison.Ordinal))
        {
            stage = "thrown";
            return true;
        }
        if (string.Equals(filter, ManagedUnhandledExceptionFilter, StringComparison.Ordinal))
        {
            stage = "unhandled";
            return true;
        }

        stage = string.Empty;
        return false;
    }

    private static string BuildInfoResponse(
        JObject evaluateResponse,
        ManagedExceptionStop exception)
    {
        var description = string.Format(
            CultureInfo.InvariantCulture,
            "{0} ({1} managed exception)",
            exception.Type,
            exception.Stage);
        evaluateResponse["command"] = "exceptionInfo";
        evaluateResponse["success"] = true;
        evaluateResponse.Remove("message");
        evaluateResponse["body"] = new JObject
        {
            ["exceptionId"] = "FrontEndCSharp/" + exception.Type,
            ["breakMode"] = string.Equals(
                exception.Stage,
                "unhandled",
                StringComparison.Ordinal)
                ? "unhandled"
                : "always",
            ["description"] = description,
            ["details"] = new JObject
            {
                ["message"] = description,
                ["typeName"] = exception.Type,
                ["fullTypeName"] = exception.Type
            }
        };
        return evaluateResponse.ToString(Formatting.None);
    }
}
