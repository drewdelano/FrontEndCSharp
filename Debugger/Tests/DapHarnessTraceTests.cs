using System.Text;
using FrontEndCSharp.VisualStudio;
using Newtonsoft.Json.Linq;

namespace Debugger.Tests;

public sealed class DapHarnessTraceTests
{
    [Test]
    public void RecordsOnlyObservedDapTrafficInsideTheHarnessArtifactRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp",
            "DebuggerIntegration",
            "DapHarnessTraceTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "composed-dap.jsonl");
        var previous = Environment.GetEnvironmentVariable(
            DapHarnessTrace.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(
                DapHarnessTrace.EnvironmentVariableName,
                path);
            var launchId = Guid.NewGuid();
            DapHarnessTrace.Write(
                launchId,
                "child",
                "adapter-to-client",
                "{\"type\":\"event\",\"event\":\"initialized\"}");
            DapHarnessTrace.Write(
                launchId,
                "child",
                "adapter-to-client",
                "{\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\"}}");
            DapHarnessTrace.Write(
                launchId,
                "child",
                "adapter-to-client",
                "{\"type\":\"event\",\"event\":\"output\",\"body\":{\"category\":\"stderr\",\"output\":\"Failed to load resource: net::ERR_CONNECTION_REFUSED\"}}");
            DapHarnessTrace.Write(
                launchId,
                "child",
                "client-to-adapter",
                "{\"seq\":7,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
            DapHarnessTrace.Write(
                launchId,
                "child",
                "client-to-adapter",
                "{\"seq\":8,\"type\":\"request\",\"command\":\"configurationDone\"}");
            DapHarnessTrace.Write(
                launchId,
                "child",
                "adapter-to-client",
                "{\"seq\":9,\"type\":\"response\",\"request_seq\":8,\"success\":true,\"command\":\"configurationDone\"}");
            DapHarnessTrace.Write(
                launchId,
                "child",
                "client-to-adapter",
                "{\"seq\":10,\"type\":\"request\",\"command\":\"setVariable\",\"arguments\":{\"variablesReference\":42,\"name\":\"value\",\"value\":\"5\"}}");
            DapHarnessTrace.Write(
                launchId,
                "child",
                "adapter-to-client",
                "{\"seq\":11,\"type\":\"response\",\"request_seq\":10,\"success\":true,\"command\":\"setVariable\",\"body\":{\"value\":\"5\",\"type\":\"int\"}}");

            var deadline = DateTime.UtcNow.AddSeconds(5);
            string[] lines;
            do
            {
                Thread.Sleep(25);
                lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
            }
            while (lines.Length < 7 && DateTime.UtcNow < deadline);

            Assert.Equal(7, lines.Length);
            var records = lines.Select(JObject.Parse).ToArray();
            Assert.All(records, record => Assert.Equal(
                launchId.ToString("D"),
                record.Value<string>("launchId")));
            var payloads = records
                .Select(record => Encoding.UTF8.GetString(Convert.FromBase64String(
                    record.Value<string>("payloadBase64")!)))
                .Select(JObject.Parse)
                .ToArray();
            Assert.Contains(payloads, payload => payload.Value<string>("event") == "stopped");
            Assert.Contains(payloads, payload => payload.Value<string>("event") == "output");
            Assert.Contains(payloads, payload => payload.Value<string>("command") == "stackTrace");
            Assert.Equal(
                2,
                payloads.Count(payload => payload.Value<string>("command") == "configurationDone"));
            Assert.Equal(
                2,
                payloads.Count(payload => payload.Value<string>("command") == "setVariable"));
            var setVariableResponse = Assert.Single(payloads, payload =>
                payload.Value<string>("command") == "setVariable"
                && payload.Value<string>("type") == "response");
            Assert.Equal("[REDACTED]", setVariableResponse["body"]!.Value<string>("value"));
            Assert.DoesNotContain(payloads, payload => payload.Value<string>("event") == "initialized");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DapHarnessTrace.EnvironmentVariableName,
                previous);
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void RedactsSensitiveDapValuesExpressionsHeadersQueriesAndUserPaths()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp",
            "DebuggerIntegration",
            "DapHarnessTraceRedactionTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "composed-dap.jsonl");
        var previous = Environment.GetEnvironmentVariable(
            DapHarnessTrace.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(
                DapHarnessTrace.EnvironmentVariableName,
                path);
            var launchId = Guid.NewGuid();
            var variablesPayload = new JObject
            {
                ["type"] = "response",
                ["command"] = "variables",
                ["body"] = new JObject
                {
                    ["variables"] = new JArray(
                        new JObject
                        {
                            ["name"] = "password",
                            ["value"] = "raw-password",
                            ["evaluateName"] = "raw-password"
                        },
                        new JObject
                        {
                            ["name"] = "message",
                            ["value"] = "Authorization: Bearer raw-bearer"
                        },
                        new JObject
                        {
                            ["name"] = "url",
                            ["value"] = "https://example.test/?access_token=raw-query"
                        },
                        new JObject
                        {
                            ["name"] = "path",
                            ["value"] = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                "secret.txt")
                        })
                }
            }.ToString(Newtonsoft.Json.Formatting.None);
            DapHarnessTrace.Write(
                launchId,
                "child",
                "adapter-to-client",
                variablesPayload);
            DapHarnessTrace.Write(
                launchId,
                "child",
                "client-to-adapter",
                "{\"type\":\"request\",\"command\":\"evaluate\",\"arguments\":{\"expression\":\"raw-expression\"}}");
            DapHarnessTrace.Write(
                launchId,
                "child",
                "client-to-adapter",
                "{\"type\":\"request\",\"command\":\"setVariable\",\"arguments\":{\"variablesReference\":42,\"name\":\"password\",\"value\":\"raw-set-password\"}}");

            var deadline = DateTime.UtcNow.AddSeconds(5);
            string[] lines;
            do
            {
                Thread.Sleep(25);
                lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
            }
            while (lines.Length < 3 && DateTime.UtcNow < deadline);

            Assert.Equal(3, lines.Length);
            var decoded = lines
                .Select(JObject.Parse)
                .Select(record => Encoding.UTF8.GetString(Convert.FromBase64String(
                    record.Value<string>("payloadBase64")!)))
                .ToArray();
            var joined = string.Join("\n", decoded);
            Assert.DoesNotContain("raw-password", joined, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-bearer", joined, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-query", joined, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-expression", joined, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-set-password", joined, StringComparison.Ordinal);
            Assert.DoesNotContain(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                joined,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[REDACTED]", joined, StringComparison.Ordinal);
            Assert.Contains("%USERPROFILE%", joined, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DapHarnessTrace.EnvironmentVariableName,
                previous);
            Directory.Delete(root, recursive: true);
        }
    }
}
