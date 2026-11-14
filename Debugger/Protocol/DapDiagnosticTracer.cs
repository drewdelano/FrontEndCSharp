using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class DapDiagnosticTracer
{
    private readonly Action<string>? diagnosticSink;
    private bool browserNavigationObserved;

    internal DapDiagnosticTracer(Action<string>? diagnosticSink)
    {
        this.diagnosticSink = diagnosticSink;
    }

    internal void TraceClientMilestone(string payload)
    {
        if (browserNavigationObserved)
        {
            return;
        }
        try
        {
            var message = JObject.Parse(payload);
            if (string.Equals(message.Value<string>("type"), "request", StringComparison.Ordinal)
                && string.Equals(message.Value<string>("command"), "launch", StringComparison.Ordinal))
            {
                browserNavigationObserved = true;
                diagnosticSink?.Invoke("browser-navigation-requested");
            }
        }
        catch (JsonException)
        {
        }
    }

    internal void TraceAdapterMilestone(JObject message)
    {
        if (!string.Equals(message.Value<string>("type"), "event", StringComparison.Ordinal)
            || !string.Equals(message.Value<string>("event"), "output", StringComparison.Ordinal))
        {
            return;
        }
        var output = message["body"]?.Value<string>("output") ?? string.Empty;
        if (output.IndexOf("Now listening on:", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            diagnosticSink?.Invoke("aspnet-host-listening");
        }
        var browserRefresh = output.IndexOf(
                "browser link",
                StringComparison.OrdinalIgnoreCase) >= 0
            || output.IndexOf("browser refresh", StringComparison.OrdinalIgnoreCase) >= 0;
        var injectionFailure = output.IndexOf("inject", StringComparison.OrdinalIgnoreCase) >= 0
            && (output.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("unable", StringComparison.OrdinalIgnoreCase) >= 0);
        if (browserRefresh && injectionFailure)
        {
            var brotli = output.IndexOf("brotli", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf(
                    "content-encoding: br",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            diagnosticSink?.Invoke(brotli
                ? "browser-refresh-injection-failed-brotli"
                : "browser-refresh-injection-failed");
        }
    }
}
