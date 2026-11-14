using System.Net;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class FrontEndDebugConfiguration
{
    internal string? BrowserPath { get; private init; }
    internal Uri? BrowserEndpoint { get; private init; }
    internal Uri? BrowserUrl { get; private init; }
    internal bool BrowserHeadless { get; private init; }
    internal bool BrowserAttachOnly { get; private init; }
    internal string? BrowserTargetId { get; private init; }
    internal string GeneratedOutputPath { get; private init; } = string.Empty;
    internal string? EditAndContinuePipe { get; private init; }
    internal string? EditAndContinueToken { get; private init; }
    internal string ProgramPath { get; private init; } = string.Empty;
    internal DebugManifestIndex Manifest { get; private init; } = null!;

    internal static FrontEndDebugConfiguration Parse(
        IReadOnlyDictionary<string, JToken> properties)
    {
        var browserPath = OptionalPath(properties, "browserPath");
        var browserEndpointText = OptionalValue(properties, "browserWebSocketUrl");
        var browserEndpoint = browserEndpointText == null
            ? null
            : new Uri(browserEndpointText);
        var browserUrlText = OptionalValue(properties, "browserUrl");
        var browserUrl = browserUrlText == null ? null : new Uri(browserUrlText);
        var browserHeadless = OptionalBoolean(properties, "browserHeadless");
        var browserAttachOnly = OptionalBoolean(properties, "browserAttachOnly");
        var browserTargetId = OptionalValue(properties, "browserTargetId");

        if (browserEndpoint != null && !IsLoopbackWebSocket(browserEndpoint))
        {
            throw new ProtocolException(
                "'browserWebSocketUrl' must be a loopback ws:// endpoint.");
        }
        if ((browserPath == null) == (browserEndpoint == null))
        {
            throw new ProtocolException(
                "Launch configuration must specify exactly one of 'browserPath' or 'browserWebSocketUrl'.");
        }
        if (browserUrl != null && !IsLoopbackHttp(browserUrl))
        {
            throw new ProtocolException(
                "'browserUrl' must be a loopback http:// or https:// URL.");
        }
        if (browserAttachOnly
            && (browserEndpoint == null
                || browserUrl == null
                || string.IsNullOrWhiteSpace(browserTargetId)))
        {
            throw new ProtocolException(
                "Attach-only launch configuration requires 'browserWebSocketUrl', 'browserUrl', and 'browserTargetId'.");
        }
        if (!browserAttachOnly && browserTargetId != null)
        {
            throw new ProtocolException(
                "'browserTargetId' is valid only when 'browserAttachOnly' is true.");
        }

        var editAndContinuePipe = OptionalValue(properties, "editAndContinuePipe");
        var editAndContinueToken = OptionalValue(properties, "editAndContinueToken");
        if ((editAndContinuePipe == null) != (editAndContinueToken == null))
        {
            throw new ProtocolException(
                "Launch configuration must specify both 'editAndContinuePipe' and 'editAndContinueToken'.");
        }

        return new FrontEndDebugConfiguration
        {
            BrowserPath = browserPath,
            BrowserEndpoint = browserEndpoint,
            BrowserUrl = browserUrl,
            BrowserHeadless = browserHeadless,
            BrowserAttachOnly = browserAttachOnly,
            BrowserTargetId = browserTargetId,
            GeneratedOutputPath = RequiredPath(properties, "generatedOutputPath"),
            EditAndContinuePipe = editAndContinuePipe,
            EditAndContinueToken = editAndContinueToken,
            ProgramPath = RequiredPath(properties, "program"),
            Manifest = DebugManifestIndex.Load(RequiredPath(properties, "debugManifestPath"))
        };
    }

    private static string RequiredPath(
        IReadOnlyDictionary<string, JToken> properties,
        string name)
    {
        var value = properties.TryGetValue(name, out var token)
            ? (string?)token
            : null;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProtocolException($"Debug configuration must specify '{name}'.");
        }
        return Path.GetFullPath(value);
    }

    private static string? OptionalPath(
        IReadOnlyDictionary<string, JToken> properties,
        string name)
    {
        var value = OptionalValue(properties, name);
        return value == null ? null : Path.GetFullPath(value);
    }

    private static string? OptionalValue(
        IReadOnlyDictionary<string, JToken> properties,
        string name)
    {
        var value = properties.TryGetValue(name, out var token)
            ? (string?)token
            : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool OptionalBoolean(
        IReadOnlyDictionary<string, JToken> properties,
        string name)
    {
        if (!properties.TryGetValue(name, out var token))
        {
            return false;
        }
        try
        {
            return (bool)token;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or FormatException
            or InvalidCastException)
        {
            throw new ProtocolException(
                $"Debug configuration property '{name}' must be true or false.");
        }
    }

    private static bool IsLoopbackWebSocket(Uri endpoint) =>
        endpoint.Scheme == "ws"
        && IsLoopbackHost(endpoint);

    private static bool IsLoopbackHttp(Uri endpoint) =>
        (endpoint.Scheme == "http" || endpoint.Scheme == "https")
        && IsLoopbackHost(endpoint);

    private static bool IsLoopbackHost(Uri endpoint) =>
        string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(endpoint.Host, out var address) && IPAddress.IsLoopback(address));
}
