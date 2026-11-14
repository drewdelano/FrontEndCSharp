using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

public sealed class DapBrowserTargetCleanupTransform
{
    private const int CloseRequestSequence = 1700000000;
    private readonly Action<string>? diagnosticSink;
    private bool isStaleBootstrapPage;
    private bool closeRequested;

    public DapBrowserTargetCleanupTransform(Action<string>? diagnosticSink = null)
    {
        this.diagnosticSink = diagnosticSink;
    }

    public DapTransformDispatch TransformAdapterToClientDuplex(string payload)
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

        if (string.Equals(message.Value<string>("type"), "event", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("event"), "process", StringComparison.Ordinal)
            && message["body"]?.Value<string>("name") is string processName
            && IsStaleBootstrapProcess(processName))
        {
            isStaleBootstrapPage = true;
        }

        if (message.Value<int?>("request_seq") == CloseRequestSequence
            && string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal))
        {
            diagnosticSink?.Invoke(
                message.Value<bool?>("success") == true
                    ? "close-complete"
                    : "close-failed");
            return new DapTransformDispatch(Array.Empty<string>(), Array.Empty<string>());
        }

        if (isStaleBootstrapPage
            && !closeRequested
            && string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "configurationDone", StringComparison.Ordinal)
            && message.Value<bool?>("success") == true)
        {
            closeRequested = true;
            diagnosticSink?.Invoke("close-requested");
            return new DapTransformDispatch(
                new[] { payload },
                new[]
                {
                    new JObject
                    {
                        ["seq"] = CloseRequestSequence,
                        ["type"] = "request",
                        ["command"] = "evaluate",
                        ["arguments"] = new JObject
                        {
                            ["expression"] = "window.close()",
                            ["context"] = "repl"
                        }
                    }.ToString(Formatting.None)
                });
        }

        return ClientOnly(payload);
    }

    private static DapTransformDispatch ClientOnly(string payload) =>
        new DapTransformDispatch(new[] { payload }, Array.Empty<string>());

    private static bool IsStaleBootstrapProcess(string processName)
    {
        if (processName.IndexOf("about:blank", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        var normalizedName = processName.Trim();
        const string adapterPrefix = "JavaScript debug adapter:";
        if (normalizedName.StartsWith(adapterPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedName = normalizedName.Substring(adapterPrefix.Length).Trim();
        }

        return string.Equals(normalizedName, "data:,", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedName, "data:, (data:,)", StringComparison.OrdinalIgnoreCase);
    }
}
