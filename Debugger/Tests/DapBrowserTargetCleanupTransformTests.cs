using FrontEndCSharp.Debugging;
using Newtonsoft.Json.Linq;

namespace Debugger.Tests;

public sealed class DapBrowserTargetCleanupTransformTests
{
    [Test]
    public void ClosesBlankPageAfterConfigurationCompletes()
    {
        var transform = new DapBrowserTargetCleanupTransform();

        Assert.Empty(transform.TransformAdapterToClientDuplex(
            "{\"seq\":1,\"type\":\"event\",\"event\":\"process\",\"body\":{\"name\":\"about:blank (about:blank)\"}}")
            .AdapterPayloads);
        var configured = transform.TransformAdapterToClientDuplex(
            "{\"seq\":2,\"type\":\"response\",\"request_seq\":1,\"command\":\"configurationDone\",\"success\":true}");

        var request = JObject.Parse(Assert.Single(configured.AdapterPayloads));
        Assert.Equal("evaluate", request.Value<string>("command"));
        Assert.Equal("window.close()", request["arguments"]!.Value<string>("expression"));
        Assert.Equal("repl", request["arguments"]!.Value<string>("context"));
        var response = new JObject
        {
            ["seq"] = 3,
            ["type"] = "response",
            ["request_seq"] = request.Value<int>("seq"),
            ["command"] = "evaluate",
            ["success"] = true
        }.ToString();
        Assert.Empty(transform.TransformAdapterToClientDuplex(response).ClientPayloads);
    }

    [Test]
    public void ClosesLegacyDataBootstrapAfterConfigurationCompletes()
    {
        var transform = new DapBrowserTargetCleanupTransform();
        transform.TransformAdapterToClientDuplex(
            "{\"seq\":1,\"type\":\"event\",\"event\":\"process\",\"body\":{\"name\":\"JavaScript debug adapter: data:,\"}}");

        var configured = transform.TransformAdapterToClientDuplex(
            "{\"seq\":2,\"type\":\"response\",\"request_seq\":1,\"command\":\"configurationDone\",\"success\":true}");

        Assert.Single(configured.AdapterPayloads);
        Assert.Single(configured.ClientPayloads);
    }

    [TestCase("data:,frontendcsharp-debugger-bootstrap")]
    [TestCase("JavaScript debug adapter: data:,frontendcsharp-debugger-bootstrap")]
    [TestCase("https://localhost:7120/react")]
    public void DoesNotCloseCurrentBootstrapOrApplicationTarget(string processName)
    {
        var transform = new DapBrowserTargetCleanupTransform();
        transform.TransformAdapterToClientDuplex(
            new JObject
            {
                ["seq"] = 1,
                ["type"] = "event",
                ["event"] = "process",
                ["body"] = new JObject { ["name"] = processName }
            }.ToString());

        var configured = transform.TransformAdapterToClientDuplex(
            "{\"seq\":2,\"type\":\"response\",\"request_seq\":1,\"command\":\"configurationDone\",\"success\":true}");

        Assert.Empty(configured.AdapterPayloads);
        Assert.Single(configured.ClientPayloads);
    }

    [Test]
    public void WaitsForSuccessfulConfigurationAndClosesOnlyOnce()
    {
        var transform = new DapBrowserTargetCleanupTransform();
        transform.TransformAdapterToClientDuplex(
            "{\"seq\":1,\"type\":\"event\",\"event\":\"process\",\"body\":{\"name\":\"JavaScript debug adapter: about:blank (about:blank)\"}}");

        Assert.Empty(transform.TransformAdapterToClientDuplex(
            "{\"seq\":2,\"type\":\"response\",\"request_seq\":1,\"command\":\"configurationDone\",\"success\":false}")
            .AdapterPayloads);
        Assert.Single(transform.TransformAdapterToClientDuplex(
            "{\"seq\":3,\"type\":\"response\",\"request_seq\":2,\"command\":\"configurationDone\",\"success\":true}")
            .AdapterPayloads);
        Assert.Empty(transform.TransformAdapterToClientDuplex(
            "{\"seq\":4,\"type\":\"response\",\"request_seq\":3,\"command\":\"configurationDone\",\"success\":true}")
            .AdapterPayloads);
    }
}
