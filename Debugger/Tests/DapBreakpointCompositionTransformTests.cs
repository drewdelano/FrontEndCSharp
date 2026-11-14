using FrontEndCSharp.Debugging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Debugger.Tests;

public sealed class DapBreakpointCompositionTransformTests
{
    [Test]
    public void TracesF5NavigationHostReadinessAndBrotliInjectionFailures()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);

        const string launch =
            "{\"seq\":1,\"type\":\"request\",\"command\":\"launch\",\"arguments\":{\"url\":\"http://127.0.0.1:5000\"}}";
        Assert.Same(launch, transform.TransformClientToAdapter(launch));
        const string listening =
            "{\"seq\":2,\"type\":\"event\",\"event\":\"output\",\"body\":{\"output\":\"Now listening on: http://127.0.0.1:5000\"}}";
        Assert.Same(listening, Assert.Single(
            transform.TransformAdapterToClientDuplex(listening).ClientPayloads));
        const string injection =
            "{\"seq\":3,\"type\":\"event\",\"event\":\"output\",\"body\":{\"output\":\"Browser Refresh was unable to inject into a Brotli response\"}}";
        Assert.Same(injection, Assert.Single(
            transform.TransformAdapterToClientDuplex(injection).ClientPayloads));

        Assert.Contains("browser-navigation-requested", diagnostics);
        Assert.Contains("aspnet-host-listening", diagnostics);
        Assert.Contains("browser-refresh-injection-failed-brotli", diagnostics);
    }

    [Test]
    public void ComposedManagedSettersUpdateLiveArgumentsLocalsAndFields()
    {
        var expression = DapBreakpointCompositionTransform.BuildManagedLocalsExpression(
            new ManagedFrameLayout(new[]
            {
                new ManagedRuntimeVariable("value", ManagedRuntimeValueKind.Argument, index: 1),
                new ManagedRuntimeVariable("result", ManagedRuntimeValueKind.Local, index: 0),
                new ManagedRuntimeVariable(
                    "captured",
                    ManagedRuntimeValueKind.LocalField,
                    index: 1,
                    fieldPath: new[] { "captured" }),
                new ManagedRuntimeVariable("Count", ManagedRuntimeValueKind.Field, fieldName: "Count")
            }));
        var scriptPath = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-set-variable-" + Guid.NewGuid().ToString("N") + ".mjs");
        try
        {
            File.WriteAllText(scriptPath, """
                globalThis.opts = {
                    ctx: {},
                    methodArgs: [
                        { value: { t: "Example.Counter", v: { Count: { t: "System.Int32", v: 3 } } } },
                        { value: { t: "System.Int32", v: 1 } }
                    ],
                    locals: [
                        { value: { t: "System.Int32", v: 2 } },
                        { value: { t: "Program.<>c__DisplayClass0_0", v: {
                            captured: { t: "System.Int32", v: 4 }
                        } } }
                    ]
                };
                const scope = EXPRESSION;
                const key = name => Object.keys(scope).find(item => item.startsWith(name + "\uFDD2"));
                scope[key("value")] = 5;
                scope[key("result")] = 6;
                scope[key("Count")] = 7;
                scope[key("captured")] = 8;
                console.log(JSON.stringify([
                    opts.methodArgs[1].value.v,
                    opts.locals[0].value.v,
                    opts.methodArgs[0].value.v.Count.v,
                    opts.locals[1].value.v.captured.v,
                    scope[key("value")],
                    scope[key("result")],
                    scope[key("Count")],
                    scope[key("captured")]
                ]));
                """.Replace("EXPRESSION", expression, StringComparison.Ordinal));
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                Arguments = JsonConvert.SerializeObject(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Node.js did not start.");
            Assert.True(process.WaitForExit(10_000), "Node.js did not complete the setter projection test.");
            var standardOutput = process.StandardOutput.ReadToEnd().Trim();
            var standardError = process.StandardError.ReadToEnd();
            Assert.True(process.ExitCode == 0, standardError);
            Assert.Equal("[5,6,7,8,5,6,7,8]", standardOutput);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Test]
    public void IgnoresBreakpointSourceBeneathMissingDirectory()
    {
        var sourcePath = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-missing-" + Guid.NewGuid().ToString("N"),
            "Read",
            "MethodDecoder.cs");
        var resolver = new DebugManifestBreakpointResolver("build-one");

        Assert.False(resolver.OwnsSource(sourcePath));
    }

    [Test]
    public void PreservesUnownedBreakpointRequestByteForByte()
    {
        const string payload = "{\"seq\":1,\"type\":\"request\",\"command\":\"setBreakpoints\",\"arguments\":{\"source\":{\"path\":\"C:\\\\Fixtures\\\\site.js\"},\"breakpoints\":[{\"line\":7}]}}";
        var transform = CreateTransform();

        var result = transform.TransformClientToAdapter(payload);

        Assert.Same(payload, result);
    }

    [Test]
    public void ConfiguresTheManagedRuntimeWhenItsInterpreterSourceLoads()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add, "build-one");
        const string loadedSource =
            "{\"seq\":2,\"type\":\"event\",\"event\":\"loadedSource\",\"body\":{\"reason\":\"new\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"}}}";

        var loaded = transform.TransformAdapterToClientDuplex(loadedSource);

        Assert.Same(loadedSource, Assert.Single(loaded.ClientPayloads));
        var contextRequest = JObject.Parse(Assert.Single(loaded.AdapterPayloads));
        Assert.Equal("evaluate", contextRequest.Value<string>("command"));
        Assert.Equal("cd top", contextRequest["arguments"]!.Value<string>("expression"));
        Assert.Equal("repl", contextRequest["arguments"]!.Value<string>("context"));
        var configurationDone =
            "{\"seq\":3,\"type\":\"response\",\"request_seq\":2,\"success\":true,\"command\":\"configurationDone\"}";
        var started = transform.TransformAdapterToClientDuplex(configurationDone);

        Assert.Same(configurationDone, Assert.Single(started.ClientPayloads));
        Assert.Empty(started.AdapterPayloads);

        var contextCompleted = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 4,
            ["type"] = "response",
            ["request_seq"] = contextRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        Assert.Empty(contextCompleted.ClientPayloads);
        var request = JObject.Parse(Assert.Single(contextCompleted.AdapterPayloads));
        Assert.Equal("evaluate", request.Value<string>("command"));
        Assert.Contains("build-one", request["arguments"]!.Value<string>("expression"));
        Assert.Contains("__FrontEndCSharpDebuggerInitialConfiguration",
            request["arguments"]!.Value<string>("expression"));
        Assert.Empty(transform.TransformAdapterToClientDuplex(loadedSource).AdapterPayloads);

        var completed = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 3,
            ["type"] = "response",
            ["request_seq"] = request.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        Assert.Empty(completed.ClientPayloads);
        Assert.Empty(completed.AdapterPayloads);
        Assert.Contains("runtime-context-selection-started", diagnostics);
        Assert.Contains("runtime-context-selection-complete", diagnostics);
        Assert.Contains("runtime-configuration-started", diagnostics);
        Assert.Contains("runtime-configuration-complete", diagnostics);
    }

    [Test]
    public void StartsManagedRuntimeConfigurationWithoutAConfigurationDoneResponse()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add, "build-one");
        const string loadedSource =
            "{\"seq\":2,\"type\":\"event\",\"event\":\"loadedSource\",\"body\":{\"reason\":\"new\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"}}}";

        var loaded = transform.TransformAdapterToClientDuplex(loadedSource);

        Assert.Same(loadedSource, Assert.Single(loaded.ClientPayloads));
        var contextRequest = JObject.Parse(Assert.Single(loaded.AdapterPayloads));
        Assert.Equal("evaluate", contextRequest.Value<string>("command"));
        Assert.Equal("cd top", contextRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("runtime-context-selection-started", diagnostics);
    }

    [Test]
    public void SelectsTheBuildCandidateThatOwnsTheManagedBreakpoint()
    {
        var diagnostics = new List<string>();
        var transform = new DapBreakpointCompositionTransform(
            new[]
            {
                new DapBreakpointCompositionCandidate(
                    new StubResolver("Simple.cs"),
                    @"C:\Fixtures\Web\simple\jit-engine\_executeInstruction.js",
                    18,
                    "simple-build"),
                new DapBreakpointCompositionCandidate(
                    new StubResolver("React.cs"),
                    @"C:\Fixtures\Web\react\jit-engine\_executeInstruction.js",
                    28,
                    "react-build")
            },
            diagnostics.Add);

        var request = JObject.Parse(transform.TransformClientToAdapter(
            Request(1, @"C:\Fixtures\Managed\React.cs", 7)));
        Assert.Equal(
            @"C:\Fixtures\Web\react\jit-engine\_executeInstruction.js",
            request["arguments"]!["source"]!.Value<string>("path"));
        Assert.Equal(28, request["arguments"]!["breakpoints"]![0]!.Value<int>("line"));

        var loaded = transform.TransformAdapterToClientDuplex(
            "{\"seq\":2,\"type\":\"event\",\"event\":\"loadedSource\",\"body\":{\"reason\":\"new\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\Web\\\\react\\\\jit-engine\\\\_executeInstruction.js\"}}}");

        var contextRequest = JObject.Parse(Assert.Single(loaded.AdapterPayloads));
        Assert.Equal("cd top", contextRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("debugger-build-candidate-selected", diagnostics);
        Assert.Contains("runtime-context-selection-started", diagnostics);
    }

    [Test]
    public void SkipsRuntimeConfigurationForAChildPageFromAnotherBuild()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add, "build-one");
        const string loadedSource =
            "{\"seq\":2,\"type\":\"event\",\"event\":\"loadedSource\",\"body\":{\"reason\":\"new\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"}}}";

        var contextRequest = JObject.Parse(Assert.Single(
            transform.TransformAdapterToClientDuplex(loadedSource).AdapterPayloads));
        var configurationRequest = JObject.Parse(Assert.Single(
            transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 4,
                ["type"] = "response",
                ["request_seq"] = contextRequest.Value<int>("seq"),
                ["success"] = true,
                ["command"] = "evaluate"
            }.ToString(Formatting.None)).AdapterPayloads));
        var expression = configurationRequest["arguments"]!.Value<string>("expression");

        Assert.Contains("__FrontEndCSharpBuildIdentity?.buildId", expression);
        Assert.Contains("activeBuildId!==configuration.buildId", expression);

        var completed = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 5,
            ["type"] = "response",
            ["request_seq"] = configurationRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = "__frontendcsharp_build_mismatch__:older-build"
            }
        }.ToString(Formatting.None));

        Assert.Empty(completed.ClientPayloads);
        Assert.Empty(completed.AdapterPayloads);
        Assert.Contains("runtime-configuration-skipped-build-mismatch", diagnostics);
        Assert.DoesNotContain("runtime-configuration-complete", diagnostics);
    }

    [Test]
    public void ReconfiguresTheManagedRuntimeWhenTheInterpreterReloads()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add, "build-one");
        const string loadedSource =
            "{\"seq\":2,\"type\":\"event\",\"event\":\"loadedSource\",\"body\":{\"reason\":\"new\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"}}}";
        const string removedSource =
            "{\"seq\":6,\"type\":\"event\",\"event\":\"loadedSource\",\"body\":{\"reason\":\"removed\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"}}}";

        var firstContext = JObject.Parse(Assert.Single(
            transform.TransformAdapterToClientDuplex(loadedSource).AdapterPayloads));
        var firstConfiguration = JObject.Parse(Assert.Single(
            transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 4,
                ["type"] = "response",
                ["request_seq"] = firstContext.Value<int>("seq"),
                ["success"] = true,
                ["command"] = "evaluate"
            }.ToString(Formatting.None)).AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 5,
            ["type"] = "response",
            ["request_seq"] = firstConfiguration.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        transform.TransformAdapterToClientDuplex(removedSource);
        var reloaded = transform.TransformAdapterToClientDuplex(loadedSource);

        Assert.Same(loadedSource, Assert.Single(reloaded.ClientPayloads));
        var secondContext = JObject.Parse(Assert.Single(reloaded.AdapterPayloads));
        Assert.Equal("evaluate", secondContext.Value<string>("command"));
        Assert.Equal("cd top", secondContext["arguments"]!.Value<string>("expression"));
        Assert.NotEqual(firstContext.Value<int>("seq"), secondContext.Value<int>("seq"));
        Assert.Equal(2, diagnostics.Count(item => item == "runtime-context-selection-started"));
    }

    [Test]
    public void RestartsManagedRuntimeConfigurationWhenTheInterpreterReloadsInFlight()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add, "build-one");
        const string loadedSource =
            "{\"seq\":2,\"type\":\"event\",\"event\":\"loadedSource\",\"body\":{\"reason\":\"new\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"}}}";
        const string removedSource =
            "{\"seq\":5,\"type\":\"event\",\"event\":\"loadedSource\",\"body\":{\"reason\":\"removed\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"}}}";

        var firstContext = JObject.Parse(Assert.Single(
            transform.TransformAdapterToClientDuplex(loadedSource).AdapterPayloads));
        var firstConfiguration = JObject.Parse(Assert.Single(
            transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 4,
                ["type"] = "response",
                ["request_seq"] = firstContext.Value<int>("seq"),
                ["success"] = true,
                ["command"] = "evaluate"
            }.ToString(Formatting.None)).AdapterPayloads));

        transform.TransformAdapterToClientDuplex(removedSource);
        var reloaded = transform.TransformAdapterToClientDuplex(loadedSource);

        Assert.Same(loadedSource, Assert.Single(reloaded.ClientPayloads));
        var secondContext = JObject.Parse(Assert.Single(reloaded.AdapterPayloads));
        Assert.NotEqual(firstContext.Value<int>("seq"), secondContext.Value<int>("seq"));
        Assert.Contains("runtime-source-reloaded", diagnostics);

        var staleResponse = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 5,
            ["type"] = "response",
            ["request_seq"] = firstConfiguration.Value<int>("seq"),
            ["success"] = false,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        Assert.Empty(staleResponse.ClientPayloads);
        Assert.Empty(staleResponse.AdapterPayloads);
        var secondConfiguration = JObject.Parse(Assert.Single(
            transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 6,
                ["type"] = "response",
                ["request_seq"] = secondContext.Value<int>("seq"),
                ["success"] = true,
                ["command"] = "evaluate"
            }.ToString(Formatting.None)).AdapterPayloads));

        var completed = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 7,
            ["type"] = "response",
            ["request_seq"] = secondConfiguration.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        Assert.Empty(completed.ClientPayloads);
        Assert.Empty(completed.AdapterPayloads);
        Assert.Contains("runtime-configuration-complete", diagnostics);
    }

    [Test]
    public void DoesNotConfigureTheManagedRuntimeWhenTopContextSelectionFails()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add, "build-one");
        var started = transform.TransformAdapterToClientDuplex(
            "{\"seq\":2,\"type\":\"event\",\"event\":\"loadedSource\",\"body\":{\"reason\":\"new\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"}}}");
        var contextRequest = JObject.Parse(Assert.Single(started.AdapterPayloads));

        var failed = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 4,
            ["type"] = "response",
            ["request_seq"] = contextRequest.Value<int>("seq"),
            ["success"] = false,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        Assert.Empty(failed.ClientPayloads);
        Assert.Empty(failed.AdapterPayloads);
        Assert.Contains("runtime-context-selection-failed", diagnostics);
        Assert.DoesNotContain("runtime-configuration-started", diagnostics);
    }

    [Test]
    public void ComposesManagedExceptionFiltersWithNativeJavaScriptFilters()
    {
        var transform = CreateTransform();
        const string response =
            "{\"seq\":2,\"type\":\"response\",\"request_seq\":1,\"success\":true,\"command\":\"initialize\",\"body\":{\"exceptionBreakpointFilters\":[{\"filter\":\"all\",\"label\":\"All Exceptions\"},{\"filter\":\"uncaught\",\"label\":\"Uncaught Exceptions\"}]}}";

        var projected = JObject.Parse(transform.TransformAdapterToClient(response));
        var filters = projected["body"]!["exceptionBreakpointFilters"]!
            .Children<JObject>()
            .ToDictionary(item => item.Value<string>("filter")!);

        Assert.True(filters.Count == 4, projected.ToString(Formatting.None));
        Assert.Equal("All Exceptions", filters["all"].Value<string>("label"));
        Assert.Equal("Uncaught Exceptions", filters["uncaught"].Value<string>("label"));
        Assert.False(filters["frontendcsharp-thrown"].Value<bool>("default"));
        Assert.True(filters["frontendcsharp-unhandled"].Value<bool>("default"));
        Assert.True(projected["body"]!.Value<bool>("supportsExceptionInfoRequest"));
        Assert.True(projected["body"]!.Value<bool>("supportsEvaluateForHovers"));
        Assert.True(projected["body"]!.Value<bool>("supportsSetVariable"));
    }

    [Test]
    public void AdvertisedExceptionFiltersAreReachableFromTheJavaScriptEnginesPrimaryCategory()
    {
        var transform = CreateTransform();
        const string response =
            "{\"seq\":2,\"type\":\"response\",\"request_seq\":1,\"success\":true,\"command\":\"initialize\",\"body\":{\"exceptionBreakpointFilters\":[{\"filter\":\"all\",\"label\":\"All Exceptions\",\"default\":false},{\"filter\":\"uncaught\",\"label\":\"Uncaught Exceptions\",\"default\":true}]}}";
        var capabilities = JObject.Parse(transform.TransformAdapterToClient(response))["body"]!;
        var registration = new VisualStudioExceptionRegistration();
        var accessible = registration.AccessibleFilters(VisualStudioExceptionRegistration.JavaScriptEngine);
        foreach (var filter in capabilities["exceptionBreakpointFilters"]!.Children<JObject>())
        {
            var id = filter.Value<string>("filter")!;
            Assert.True(accessible.ContainsKey(id), "Advertised filter is disconnected from the active IDE exception category: " + id);
            Assert.Equal(filter.Value<bool>("default"), accessible[id]);
        }
        Assert.Equal("{" + VisualStudioExceptionRegistration.JavaScriptCategory + "}",
            registration.Metric(VisualStudioExceptionRegistration.JavaScriptEngine, "ExceptionBreakpointCategory"),
            ignoreCase: true);
        Assert.Equal("{D136063F-F239-4BFC-93CE-C0CF4C4BCFAC}",
            registration.Metric(VisualStudioExceptionRegistration.JavaScriptEngine, "CustomProtocolExtension"));
    }

    [Test]
    public void StandaloneManagedExceptionFiltersRemainReachableInTheirOwnCategory()
    {
        var registration = new VisualStudioExceptionRegistration();
        var accessible = registration.AccessibleFilters(VisualStudioExceptionRegistration.ManagedEngine);
        Assert.Equal(2, accessible.Count);
        Assert.False(accessible["frontendcsharp-thrown"]);
        Assert.True(accessible["frontendcsharp-unhandled"]);
        Assert.NotEqual(registration.Metric(VisualStudioExceptionRegistration.JavaScriptEngine, "ExceptionBreakpointCategory"),
            registration.Metric(VisualStudioExceptionRegistration.ManagedEngine, "ExceptionBreakpointCategory"));
    }

    [Test]
    public void PreservesNativeOnlyExceptionConfigurationByteForByte()
    {
        var transform = CreateTransform();
        const string request =
            "{\"seq\":7,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[\"uncaught\"]}}";

        var projected = transform.TransformClientToAdapter(request);

        Assert.Same(request, projected);
    }

    [Test]
    public void ComposesManagedExceptionFilterOptionsAndPreservesNativeOptions()
    {
        var transform = CreateTransform();
        var nativeConfiguration = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":8,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filterOptions\":[{\"filterId\":\"uncaught\",\"condition\":\"nativeCondition\"},{\"filterId\":\"frontendcsharp-thrown\"}]}}"));

        var nativeOptions = nativeConfiguration["arguments"]!["filterOptions"]!
            .Children<JObject>()
            .ToArray();
        var nativeOption = Assert.Single(nativeOptions);
        Assert.Equal("uncaught", nativeOption.Value<string>("filterId"));
        Assert.Equal("nativeCondition", nativeOption.Value<string>("condition"));

        var pendingConfiguration = transform.TransformAdapterToClientDuplex(
            "{\"seq\":9,\"type\":\"response\",\"request_seq\":8,\"success\":true,\"command\":\"setExceptionBreakpoints\"}");
        var runtimeConfiguration = JObject.Parse(
            Assert.Single(pendingConfiguration.AdapterPayloads));

        Assert.Contains("thrown",
            runtimeConfiguration["arguments"]!.Value<string>("expression"));
    }

    [Test]
    public void PreservesAmbiguousNativeFilterIdsWhileComposingManagedFilters()
    {
        var transform = CreateTransform();
        var nativeConfiguration = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":10,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[\"unhandled\",\"frontendcsharp-unhandled\"],\"filterOptions\":[{\"filterId\":\"frontendcsharp-thrown\"}]}}"));

        Assert.Equal(
            new[] { "unhandled" },
            nativeConfiguration["arguments"]!["filters"]!.Values<string>());
        Assert.Empty(nativeConfiguration["arguments"]!["filterOptions"]!);

        var pendingConfiguration = transform.TransformAdapterToClientDuplex(
            "{\"seq\":11,\"type\":\"response\",\"request_seq\":10,\"success\":true,\"command\":\"setExceptionBreakpoints\"}");
        var runtimeConfiguration = JObject.Parse(
            Assert.Single(pendingConfiguration.AdapterPayloads));
        var expression = runtimeConfiguration["arguments"]!.Value<string>("expression");

        Assert.Contains("thrown", expression);
        Assert.Contains("unhandled", expression);
    }

    [Test]
    public void FailedNativeExceptionConfigurationDoesNotActivateManagedPolicy()
    {
        var transform = CreateTransform();
        transform.TransformClientToAdapter(
            "{\"seq\":7,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[\"frontendcsharp-unhandled\"]}}");
        var failedConfiguration = transform.TransformAdapterToClientDuplex(
            "{\"seq\":8,\"type\":\"response\",\"request_seq\":7,\"success\":false,\"command\":\"setExceptionBreakpoints\",\"message\":\"rejected\"}");
        Assert.Empty(failedConfiguration.AdapterPayloads);
        Assert.Single(failedConfiguration.ClientPayloads);

        const string stopped =
            "{\"seq\":9,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"exception\",\"threadId\":1}}";
        var forwardedStop = transform.TransformAdapterToClientDuplex(stopped);

        Assert.Empty(forwardedStop.AdapterPayloads);
        Assert.Same(stopped, Assert.Single(forwardedStop.ClientPayloads));
    }

    [Test]
    public void FailedRuntimeExceptionReconfigurationKeepsPreviousPolicy()
    {
        var transform = CreateTransform();
        transform.TransformClientToAdapter(
            "{\"seq\":10,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[\"frontendcsharp-unhandled\"]}}");
        var firstNativeResponse = transform.TransformAdapterToClientDuplex(
            "{\"seq\":11,\"type\":\"response\",\"request_seq\":10,\"success\":true,\"command\":\"setExceptionBreakpoints\"}");
        var firstRuntimeRequest = JObject.Parse(Assert.Single(firstNativeResponse.AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 12,
            ["type"] = "response",
            ["request_seq"] = firstRuntimeRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        transform.TransformClientToAdapter(
            "{\"seq\":13,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[]}}");
        var secondNativeResponse = transform.TransformAdapterToClientDuplex(
            "{\"seq\":14,\"type\":\"response\",\"request_seq\":13,\"success\":true,\"command\":\"setExceptionBreakpoints\"}");
        var secondRuntimeRequest = JObject.Parse(Assert.Single(secondNativeResponse.AdapterPayloads));
        var failedRuntimeConfiguration = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 15,
            ["type"] = "response",
            ["request_seq"] = secondRuntimeRequest.Value<int>("seq"),
            ["success"] = false,
            ["command"] = "evaluate",
            ["message"] = "runtime rejected configuration"
        }.ToString(Formatting.None));
        Assert.False(JObject.Parse(
            Assert.Single(failedRuntimeConfiguration.ClientPayloads)).Value<bool>("success"));

        var pendingStop = transform.TransformAdapterToClientDuplex(
            "{\"seq\":16,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"exception\",\"threadId\":1}}");

        Assert.Empty(pendingStop.ClientPayloads);
        Assert.Single(pendingStop.AdapterPayloads);
    }

    [Test]
    public void ConfiguresAndReportsManagedExceptionsWithoutConsumingNativeFilters()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);

        var nativeConfiguration = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":30,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[\"uncaught\",\"frontendcsharp-unhandled\"]}}"));
        Assert.Equal(new[] { "uncaught" },
            nativeConfiguration["arguments"]!["filters"]!.Values<string>());

        var pendingConfiguration = transform.TransformAdapterToClientDuplex(
            "{\"seq\":31,\"type\":\"response\",\"request_seq\":30,\"success\":true,\"command\":\"setExceptionBreakpoints\"}");
        Assert.Empty(pendingConfiguration.ClientPayloads);
        var runtimeConfiguration = JObject.Parse(
            Assert.Single(pendingConfiguration.AdapterPayloads));
        Assert.Equal("evaluate", runtimeConfiguration.Value<string>("command"));
        Assert.Null(runtimeConfiguration["arguments"]!["frameId"]);
        Assert.Contains("setExceptionBreakpoints(filters)",
            runtimeConfiguration["arguments"]!.Value<string>("expression"));
        Assert.Contains("unhandled",
            runtimeConfiguration["arguments"]!.Value<string>("expression"));

        var completedConfiguration = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 32,
            ["type"] = "response",
            ["request_seq"] = runtimeConfiguration.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "undefined", ["variablesReference"] = 0 }
        }.ToString(Formatting.None));
        var configurationResponse = JObject.Parse(
            Assert.Single(completedConfiguration.ClientPayloads));
        Assert.Equal("setExceptionBreakpoints", configurationResponse.Value<string>("command"));
        Assert.Equal(30, configurationResponse.Value<int>("request_seq"));

        var pendingStop = transform.TransformAdapterToClientDuplex(
            "{\"seq\":33,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"pause\",\"threadId\":1,\"allThreadsStopped\":true}}");
        Assert.Empty(pendingStop.ClientPayloads);
        var stopProbe = JObject.Parse(Assert.Single(pendingStop.AdapterPayloads));
        var completedStop = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 34,
            ["type"] = "response",
            ["request_seq"] = stopProbe.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = JsonConvert.SerializeObject(
                    "[\"exception\",\"unhandled\",\"System.InvalidOperationException\",\"module-a\",100663297,2,3]"),
                ["variablesReference"] = 0
            }
        }.ToString(Formatting.None));
        var stopped = JObject.Parse(Assert.Single(completedStop.ClientPayloads));
        Assert.Equal("exception", stopped["body"]!.Value<string>("reason"));
        Assert.Contains("System.InvalidOperationException",
            stopped["body"]!.Value<string>("description"));

        var exceptionInfoRequest = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":35,\"type\":\"request\",\"command\":\"exceptionInfo\",\"arguments\":{\"threadId\":1}}"));
        Assert.Equal("evaluate", exceptionInfoRequest.Value<string>("command"));
        var exceptionInfoResponse = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 36,
            ["type"] = "response",
            ["request_seq"] = 35,
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "undefined", ["variablesReference"] = 0 }
        }.ToString(Formatting.None));
        var exceptionInfo = JObject.Parse(Assert.Single(exceptionInfoResponse.ClientPayloads));
        Assert.Equal("exceptionInfo", exceptionInfo.Value<string>("command"));
        Assert.Equal("FrontEndCSharp/System.InvalidOperationException",
            exceptionInfo["body"]!.Value<string>("exceptionId"));
        Assert.Equal("unhandled", exceptionInfo["body"]!.Value<string>("breakMode"));
        Assert.Contains("exception-configuration-runtime-complete", diagnostics);
        Assert.Contains("stop-exception-correlated", diagnostics);
        Assert.Contains("exception-info-request-correlated", diagnostics);
    }

    [Test]
    public void ManagedExceptionPolicyPreservesAnUnrelatedNativeJavaScriptStop()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        var forwardedConfiguration = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":30,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[\"unhandled\",\"frontendcsharp-unhandled\"]}}"));
        Assert.Equal(
            new[] { "unhandled" },
            forwardedConfiguration["arguments"]!["filters"]!.Values<string>());
        var nativeConfiguration = transform.TransformAdapterToClientDuplex(
            "{\"seq\":31,\"type\":\"response\",\"request_seq\":30,\"success\":true,\"command\":\"setExceptionBreakpoints\"}");
        var runtimeConfiguration = JObject.Parse(
            Assert.Single(nativeConfiguration.AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 32,
            ["type"] = "response",
            ["request_seq"] = runtimeConfiguration.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        const string nativeStopped =
            "{\"seq\":33,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"exception\",\"description\":\"TypeError\",\"threadId\":1,\"allThreadsStopped\":true}}";
        var pendingStop = transform.TransformAdapterToClientDuplex(nativeStopped);
        var stopProbe = JObject.Parse(Assert.Single(pendingStop.AdapterPayloads));
        var released = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 34,
            ["type"] = "response",
            ["request_seq"] = stopProbe.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "null", ["variablesReference"] = 0 }
        }.ToString(Formatting.None));

        Assert.Same(nativeStopped, Assert.Single(released.ClientPayloads));
        Assert.Contains("stop-exception-bypassed-native", diagnostics);
    }

    [Test]
    public void NativeStopDoesNotRetainPriorManagedExceptionInfoWithoutContinuedEvent()
    {
        var transform = CreateTransform();
        transform.TransformClientToAdapter(
            "{\"seq\":40,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[\"frontendcsharp-unhandled\"]}}");
        var nativeConfiguration = transform.TransformAdapterToClientDuplex(
            "{\"seq\":41,\"type\":\"response\",\"request_seq\":40,\"success\":true,\"command\":\"setExceptionBreakpoints\"}");
        var runtimeConfiguration = JObject.Parse(
            Assert.Single(nativeConfiguration.AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 42,
            ["type"] = "response",
            ["request_seq"] = runtimeConfiguration.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        var managedStop = transform.TransformAdapterToClientDuplex(
            "{\"seq\":43,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"pause\",\"threadId\":1}}");
        var managedProbe = JObject.Parse(Assert.Single(managedStop.AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 44,
            ["type"] = "response",
            ["request_seq"] = managedProbe.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = JsonConvert.SerializeObject(
                    "[\"exception\",\"unhandled\",\"System.InvalidOperationException\",\"module-a\",100663297,2,3]")
            }
        }.ToString(Formatting.None));

        var nativeStop = transform.TransformAdapterToClientDuplex(
            "{\"seq\":45,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"exception\",\"threadId\":1}}");
        var nativeProbe = JObject.Parse(Assert.Single(nativeStop.AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 46,
            ["type"] = "response",
            ["request_seq"] = nativeProbe.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "null" }
        }.ToString(Formatting.None));

        const string exceptionInfoRequest =
            "{\"seq\":47,\"type\":\"request\",\"command\":\"exceptionInfo\",\"arguments\":{\"threadId\":1}}";
        var forwarded = transform.TransformClientToAdapter(exceptionInfoRequest);

        Assert.Same(exceptionInfoRequest, forwarded);
    }

    [Test]
    public void ProjectsManagedBreakpointOntoExecuteInstructionCondition()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        var payload = Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6);

        var result = JObject.Parse(transform.TransformClientToAdapter(payload));

        Assert.Equal(@"C:\Fixtures\FrontEnd.Demo\wwwroot\js\jit-engine\_executeInstruction.js",
            result["arguments"]!["source"]!["path"]!.Value<string>());
        var breakpoint = Assert.Single(result["arguments"]!["breakpoints"]!.Children<JObject>());
        Assert.Equal(18, breakpoint.Value<int>("line"));
        Assert.Equal(
            "opts?.methodInfo?.debugModuleId === \"module-a\" && opts?.methodInfo?.debugMethodToken === 100663297 && opts?.currentBlockIndex === 2 && opts?.instructionIndex === 3",
            breakpoint.Value<string>("condition"));
        Assert.Equal(new[] { "request-owned-all-resolved" }, diagnostics);
    }

    [Test]
    public void AggregatesManagedDocumentsThatShareTheRuntimeSource()
    {
        var transform = CreateTransform();
        transform.TransformClientToAdapter(Request(3, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));

        var result = JObject.Parse(transform.TransformClientToAdapter(
            Request(4, @"C:\Fixtures\FrontEnd.Program\Other.cs", 10)));

        var breakpoints = result["arguments"]!["breakpoints"]!.Children<JObject>().ToArray();
        var breakpoint = Assert.Single(breakpoints);
        var condition = breakpoint.Value<string>("condition");
        Assert.Contains("100663297", condition);
        Assert.Contains("100663298", condition);
        Assert.Contains(" || ", condition);
    }

    [Test]
    public void ProjectsMultipleLiveManagedBreakpointsThroughOnePhysicalBreakpoint()
    {
        var transform = CreateTransform();
        var request = new JObject
        {
            ["seq"] = 17,
            ["type"] = "request",
            ["command"] = "setBreakpoints",
            ["arguments"] = new JObject
            {
                ["source"] = new JObject { ["path"] = @"C:\Fixtures\FrontEnd.Program\Program.cs" },
                ["breakpoints"] = new JArray(
                    new JObject { ["line"] = 6 },
                    new JObject { ["line"] = 21 })
            }
        };

        var projectedRequest = JObject.Parse(
            transform.TransformClientToAdapter(request.ToString(Formatting.None)));
        var physical = Assert.Single(
            projectedRequest["arguments"]!["breakpoints"]!.Children<JObject>());
        Assert.Contains("100663297", physical.Value<string>("condition"));
        Assert.Contains("100663298", physical.Value<string>("condition"));

        var projectedResponse = JObject.Parse(transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}"));
        var logical = projectedResponse["body"]!["breakpoints"]!.Children<JObject>().ToArray();
        Assert.Equal(2, logical.Length);
        Assert.All(logical, breakpoint => Assert.True(breakpoint.Value<bool>("verified")));
        var logicalIds = logical.Select(breakpoint => breakpoint.Value<int>("id")).ToArray();
        Assert.Equal(2, logicalIds.Distinct().Count());
        Assert.DoesNotContain(91, logicalIds);

        var stopped = transform.TransformAdapterToClientDuplex(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");
        Assert.Null(JObject.Parse(Assert.Single(stopped.ClientPayloads))["body"]!["hitBreakpointIds"]);
        transform.TransformClientToAdapter(
            "{\"seq\":22,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
        var pendingStack = transform.TransformAdapterToClientDuplex(
            "{\"seq\":23,\"type\":\"response\",\"request_seq\":22,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":701,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5}]}}");
        Assert.Empty(pendingStack.ClientPayloads);
        var stackRequests = pendingStack.AdapterPayloads.Select(JObject.Parse).ToArray();
        var identityRequest = Assert.Single(stackRequests, requestMessage =>
            requestMessage["arguments"]!.Value<string>("expression")!.Contains("debugMethodToken"));
        var countRequest = Assert.Single(stackRequests, requestMessage =>
            requestMessage["arguments"]!.Value<string>("expression")!.Contains("debugFrames?.length"));

        var pendingCount = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 24,
            ["type"] = "response",
            ["request_seq"] = identityRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = "[\"module-a\",100663298,4,5]",
                ["variablesReference"] = 0
            }
        }.ToString(Formatting.None));
        Assert.Empty(pendingCount.ClientPayloads);
        var completedStack = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 25,
            ["type"] = "response",
            ["request_seq"] = countRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "[1,0]", ["variablesReference"] = 0 }
        }.ToString(Formatting.None));
        var firstFrame = JObject.Parse(
            Assert.Single(completedStack.ClientPayloads))["body"]!["stackFrames"]!.First!;
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Other.cs", firstFrame["source"]!["path"]!.Value<string>());
        Assert.Equal(11, firstFrame.Value<int>("line"));

        transform.TransformClientToAdapter(
            "{\"seq\":26,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
        var pendingRefresh = transform.TransformAdapterToClientDuplex(
            "{\"seq\":27,\"type\":\"response\",\"request_seq\":26,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":701,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5}]}}");
        Assert.Empty(pendingRefresh.ClientPayloads);
        var refreshCountRequest = JObject.Parse(Assert.Single(pendingRefresh.AdapterPayloads));
        Assert.Contains("debugFrames?.length",
            refreshCountRequest["arguments"]!.Value<string>("expression"));
        var completedRefresh = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 28,
            ["type"] = "response",
            ["request_seq"] = refreshCountRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "[1,0]", ["variablesReference"] = 0 }
        }.ToString(Formatting.None));
        var refreshedFrame = JObject.Parse(
            Assert.Single(completedRefresh.ClientPayloads))["body"]!["stackFrames"]!.First!;
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Other.cs", refreshedFrame["source"]!["path"]!.Value<string>());
        Assert.Equal(11, refreshedFrame.Value<int>("line"));
    }

    [Test]
    public void ProjectsResponseBackToOriginalManagedSource()
    {
        var transform = CreateTransform();
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        const string response = "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}";

        var result = JObject.Parse(transform.TransformAdapterToClient(response));
        var breakpoint = Assert.Single(result["body"]!["breakpoints"]!.Children<JObject>());

        Assert.True(breakpoint.Value<bool>("verified"));
        Assert.NotEqual(91, breakpoint.Value<int>("id"));
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Program.cs", breakpoint["source"]!["path"]!.Value<string>());
        Assert.Equal(7, breakpoint.Value<int>("line"));
        Assert.Equal(9, breakpoint.Value<int>("column"));
    }

    [Test]
    public void ReturnsManifestResolutionFailureWithoutCreatingRuntimeBreakpoint()
    {
        var transform = CreateTransform();
        var request = transform.TransformClientToAdapter(
            Request(8, @"C:\Fixtures\FrontEnd.Program\Program.cs", 99));
        var projectedRequest = JObject.Parse(request);
        Assert.Empty(projectedRequest["arguments"]!["breakpoints"]!);

        const string response = "{\"seq\":9,\"type\":\"response\",\"request_seq\":8,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[]}}";
        var projectedResponse = JObject.Parse(transform.TransformAdapterToClient(response));
        var breakpoint = Assert.Single(projectedResponse["body"]!["breakpoints"]!.Children<JObject>());

        Assert.False(breakpoint.Value<bool>("verified"));
        Assert.Equal("No executable sequence point.", breakpoint.Value<string>("message"));
        Assert.Equal(99, breakpoint.Value<int>("line"));
    }

    [Test]
    public void ProjectsLateBreakpointBindingEventBackToManagedSource()
    {
        var transform = CreateTransform();
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        var initial = JObject.Parse(transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":false,\"line\":18}]}}"));
        var logicalId = initial["body"]!["breakpoints"]![0]!.Value<int>("id");
        const string changed = "{\"seq\":21,\"type\":\"event\",\"event\":\"breakpoint\",\"body\":{\"reason\":\"changed\",\"breakpoint\":{\"id\":91,\"verified\":true,\"line\":18}}}";

        var result = JObject.Parse(transform.TransformAdapterToClient(changed));
        var breakpoint = (JObject)result["body"]!["breakpoint"]!;

        Assert.True(breakpoint.Value<bool>("verified"));
        Assert.Equal(logicalId, breakpoint.Value<int>("id"));
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Program.cs", breakpoint["source"]!["path"]!.Value<string>());
        Assert.Equal(7, breakpoint.Value<int>("line"));
        Assert.Equal(9, breakpoint.Value<int>("column"));
    }

    [Test]
    public void ProjectsLateSharedBreakpointBindingEventToEveryManagedLocation()
    {
        var transform = CreateTransform();
        transform.TransformClientToAdapter(new JObject
        {
            ["seq"] = 17,
            ["type"] = "request",
            ["command"] = "setBreakpoints",
            ["arguments"] = new JObject
            {
                ["source"] = new JObject { ["path"] = @"C:\Fixtures\FrontEnd.Program\Program.cs" },
                ["breakpoints"] = new JArray(
                    new JObject { ["line"] = 6 },
                    new JObject { ["line"] = 21 })
            }
        }.ToString(Formatting.None));
        var initial = JObject.Parse(transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":false,\"line\":18}]}}"));
        var logicalIds = initial["body"]!["breakpoints"]!
            .Select(breakpoint => breakpoint.Value<int>("id"))
            .ToArray();

        var changed = transform.TransformAdapterToClientDuplex(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"breakpoint\",\"body\":{\"reason\":\"changed\",\"breakpoint\":{\"id\":91,\"verified\":true,\"line\":18}}}");
        var breakpoints = changed.ClientPayloads
            .Select(JObject.Parse)
            .Select(projected => (JObject)projected["body"]!["breakpoint"]!)
            .ToArray();

        Assert.Empty(changed.AdapterPayloads);
        Assert.Equal(2, breakpoints.Length);
        Assert.All(breakpoints, breakpoint => Assert.True(breakpoint.Value<bool>("verified")));
        Assert.Equal(logicalIds, breakpoints.Select(breakpoint => breakpoint.Value<int>("id")));
        Assert.Equal(new[] { 7, 11 }, breakpoints.Select(breakpoint => breakpoint.Value<int>("line")));
    }

    [Test]
    public void ProjectsStoppedRuntimeFrameBackToManagedSource()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");
        const string stackRequest =
            "{\"seq\":22,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}";
        Assert.Same(stackRequest, transform.TransformClientToAdapter(stackRequest));
        const string stackResponse =
            "{\"seq\":23,\"type\":\"response\",\"request_seq\":22,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":1,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5},{\"id\":2,\"name\":\"main\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\site.js\"},\"line\":4,\"column\":1}]}}";

        var result = JObject.Parse(transform.TransformAdapterToClient(stackResponse));
        var frames = result["body"]!["stackFrames"]!.Children<JObject>().ToArray();

        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Program.cs",
            frames[0]["source"]!["path"]!.Value<string>());
        Assert.Equal(7, frames[0].Value<int>("line"));
        Assert.Equal(9, frames[0].Value<int>("column"));
        Assert.Equal(@"C:\Fixtures\site.js", frames[1]["source"]!["path"]!.Value<string>());
        Assert.Contains("stop-binding-correlated", diagnostics);
        Assert.Contains("stack-request-correlated", diagnostics);
        Assert.Contains("stack-frame-projected", diagnostics);
    }

    [Test]
    public void PagedStackHidesLaterInterpreterImplementationFrames()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");

        transform.TransformClientToAdapter(
            "{\"seq\":22,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1,\"startFrame\":0,\"levels\":1}}");
        var firstPage = JObject.Parse(transform.TransformAdapterToClient(
            "{\"seq\":23,\"type\":\"response\",\"request_seq\":22,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"totalFrames\":2,\"stackFrames\":[{\"id\":701,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5}]}}"));
        Assert.Equal(
            @"C:\Fixtures\FrontEnd.Program\Program.cs",
            firstPage["body"]!["stackFrames"]![0]!["source"]!["path"]!.Value<string>());

        transform.TransformClientToAdapter(
            "{\"seq\":24,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1,\"startFrame\":1,\"levels\":20}}");
        var pending = transform.TransformAdapterToClientDuplex(
            "{\"seq\":25,\"type\":\"response\",\"request_seq\":24,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"totalFrames\":2,\"stackFrames\":[{\"id\":702,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5}]}}");

        Assert.Empty(pending.AdapterPayloads);
        var secondPage = JObject.Parse(Assert.Single(pending.ClientPayloads));
        Assert.Empty(secondPage["body"]!["stackFrames"]!);
        Assert.Equal(1, secondPage["body"]!.Value<int>("totalFrames"));
        Assert.Contains("stack-page-deferred-to-identity", diagnostics);
        Assert.Contains("stack-page-runtime-implementation-hidden", diagnostics);
    }

    [Test]
    public void DuplexProjectionBuildsManagedParentsFromTheRuntimeContextStack()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");
        transform.TransformClientToAdapter(
            "{\"seq\":22,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
        const string stackResponse =
            "{\"seq\":23,\"type\":\"response\",\"request_seq\":22,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":701,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5},{\"id\":702,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5},{\"id\":703,\"name\":\"main\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\site.js\"},\"line\":4,\"column\":1}]}}";

        var pending = transform.TransformAdapterToClientDuplex(stackResponse);

        Assert.Empty(pending.ClientPayloads);
        var countRequest = JObject.Parse(Assert.Single(pending.AdapterPayloads));
        Assert.Equal("evaluate", countRequest.Value<string>("command"));
        Assert.Equal(701, countRequest["arguments"]!.Value<int>("frameId"));
        Assert.Contains("typeof opts==='object'&&opts?.ctx?opts.ctx", countRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("ctx?.debugFrames?.length", countRequest["arguments"]!.Value<string>("expression"));
        var pendingIdentity = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 24,
            ["type"] = "response",
            ["request_seq"] = countRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = "[2,0]",
                ["variablesReference"] = 0
            }
        }.ToString(Formatting.None));
        Assert.Empty(pendingIdentity.ClientPayloads);
        var identityRequest = JObject.Parse(Assert.Single(pendingIdentity.AdapterPayloads));
        Assert.Equal(701, identityRequest["arguments"]!.Value<int>("frameId"));
        Assert.Contains("debugFrames?.[1]", identityRequest["arguments"]!.Value<string>("expression"));
        var completed = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 25,
            ["type"] = "response",
            ["request_seq"] = identityRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = "[\"module-a\",100663298,4,5]",
                ["variablesReference"] = 0
            }
        }.ToString(Formatting.None));
        var response = JObject.Parse(Assert.Single(completed.ClientPayloads));
        var frames = response["body"]!["stackFrames"]!.Children<JObject>().ToArray();

        Assert.Empty(completed.AdapterPayloads);
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Program.cs", frames[0]["source"]!["path"]!.Value<string>());
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Other.cs", frames[1]["source"]!["path"]!.Value<string>());
        Assert.Equal("Program.Other", frames[1].Value<string>("name"));
        Assert.Equal(11, frames[1].Value<int>("line"));
        Assert.Equal(@"C:\Fixtures\site.js", frames[2]["source"]!["path"]!.Value<string>());
        var parentScopes = JObject.Parse(transform.TransformClientToAdapter(new JObject
        {
            ["seq"] = 26,
            ["type"] = "request",
            ["command"] = "scopes",
            ["arguments"] = new JObject { ["frameId"] = frames[1].Value<int>("id") }
        }.ToString(Formatting.None)));
        Assert.Equal("evaluate", parentScopes.Value<string>("command"));
        Assert.Equal(701, parentScopes["arguments"]!.Value<int>("frameId"));
        Assert.Contains("const frame=ctx?.debugFrames?.[1]",
            parentScopes["arguments"]!.Value<string>("expression"));
        Assert.Contains("stack-logical-frame-projected", diagnostics);
        Assert.Contains("stack-nested-projection-complete", diagnostics);
    }

    [Test]
    public void DuplexProjectionAddsLogicalAsyncFramesAndRoutesTheirScopes()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");
        transform.TransformClientToAdapter(
            "{\"seq\":22,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");

        var pendingCount = transform.TransformAdapterToClientDuplex(
            "{\"seq\":23,\"type\":\"response\",\"request_seq\":22,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"totalFrames\":2,\"stackFrames\":[{\"id\":701,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5},{\"id\":703,\"name\":\"main\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\site.js\"},\"line\":4,\"column\":1}]}}");
        var countRequest = JObject.Parse(Assert.Single(pendingCount.AdapterPayloads));
        Assert.Contains("debugFrames?.length", countRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("asyncParents?.length", countRequest["arguments"]!.Value<string>("expression"));
        Assert.Equal(701, countRequest["arguments"]!.Value<int>("frameId"));

        var pendingIdentity = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 24,
            ["type"] = "response",
            ["request_seq"] = countRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "[1,1]", ["variablesReference"] = 0 }
        }.ToString(Formatting.None));
        var identityRequest = JObject.Parse(Assert.Single(pendingIdentity.AdapterPayloads));
        Assert.Contains("const frame=ctx?.asyncParents?.[0]", identityRequest["arguments"]!.Value<string>("expression"));
        Assert.Equal(701, identityRequest["arguments"]!.Value<int>("frameId"));

        var completed = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 25,
            ["type"] = "response",
            ["request_seq"] = identityRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = "[\"module-a\",100663298,4,5]",
                ["variablesReference"] = 0
            }
        }.ToString(Formatting.None));
        var stack = JObject.Parse(Assert.Single(completed.ClientPayloads));
        var frames = stack["body"]!["stackFrames"]!.Children<JObject>().ToArray();
        Assert.Equal(3, frames.Length);
        Assert.Equal(3, stack["body"]!.Value<int>("totalFrames"));
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Program.cs", frames[0]["source"]!["path"]!.Value<string>());
        Assert.Equal("Program.Other", frames[1].Value<string>("name"));
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Other.cs", frames[1]["source"]!["path"]!.Value<string>());
        Assert.Equal(@"C:\Fixtures\site.js", frames[2]["source"]!["path"]!.Value<string>());
        var logicalFrameId = frames[1].Value<int>("id");

        var scopesRequest = JObject.Parse(transform.TransformClientToAdapter(new JObject
        {
            ["seq"] = 26,
            ["type"] = "request",
            ["command"] = "scopes",
            ["arguments"] = new JObject { ["frameId"] = logicalFrameId }
        }.ToString(Formatting.None)));
        Assert.Equal("evaluate", scopesRequest.Value<string>("command"));
        Assert.Equal(701, scopesRequest["arguments"]!.Value<int>("frameId"));
        Assert.Contains("const frame=ctx?.asyncParents?.[0]", scopesRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("display(frame?.locals?.[0])", scopesRequest["arguments"]!.Value<string>("expression"));

        var scopesResponse = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 27,
            ["type"] = "response",
            ["request_seq"] = 26,
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "Object", ["variablesReference"] = 444 }
        }.ToString(Formatting.None));
        var projectedScopes = JObject.Parse(Assert.Single(scopesResponse.ClientPayloads));
        Assert.Equal("scopes", projectedScopes.Value<string>("command"));
        Assert.Equal(444, projectedScopes["body"]!["scopes"]![0]!.Value<int>("variablesReference"));

        transform.TransformClientToAdapter(new JObject
        {
            ["seq"] = 28,
            ["type"] = "request",
            ["command"] = "variables",
            ["arguments"] = new JObject { ["variablesReference"] = 444 }
        }.ToString(Formatting.None));
        transform.TransformAdapterToClient(new JObject
        {
            ["seq"] = 29,
            ["type"] = "response",
            ["request_seq"] = 28,
            ["success"] = true,
            ["command"] = "variables",
            ["body"] = new JObject
            {
                ["variables"] = new JArray(new JObject
                {
                    ["name"] = "result",
                    ["value"] = "1",
                    ["variablesReference"] = 0
                })
            }
        }.ToString(Formatting.None));
        var rejectedSet = JObject.Parse(transform.TransformClientToAdapter(new JObject
        {
            ["seq"] = 30,
            ["type"] = "request",
            ["command"] = "setVariable",
            ["arguments"] = new JObject
            {
                ["variablesReference"] = 444,
                ["name"] = "result",
                ["value"] = "2"
            }
        }.ToString(Formatting.None)));
        Assert.Equal("evaluate", rejectedSet.Value<string>("command"));
        var rejectedResponse = JObject.Parse(transform.TransformAdapterToClient(new JObject
        {
            ["seq"] = 31,
            ["type"] = "response",
            ["request_seq"] = 30,
            ["success"] = false,
            ["command"] = "evaluate"
        }.ToString(Formatting.None)));
        Assert.Equal("setVariable", rejectedResponse.Value<string>("command"));
        Assert.False(rejectedResponse.Value<bool>("success"));
        Assert.Contains("immutable", rejectedResponse.Value<string>("message"));
        Assert.Contains("stack-logical-frame-projected", diagnostics);
        Assert.Contains("scopes-logical-request-correlated", diagnostics);
    }

    [Test]
    public void DuplexManagedStepArmsRuntimeResumesAndProjectsTheStepStop()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");
        transform.TransformClientToAdapter(
            "{\"seq\":22,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
        transform.TransformAdapterToClient(
            "{\"seq\":23,\"type\":\"response\",\"request_seq\":22,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":701,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5}]}}");

        var stepRequest = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":24,\"type\":\"request\",\"command\":\"next\",\"arguments\":{\"threadId\":1,\"singleThread\":false}}"));
        Assert.Equal("evaluate", stepRequest.Value<string>("command"));
        Assert.Equal(701, stepRequest["arguments"]!.Value<int>("frameId"));
        var expression = stepRequest["arguments"]!.Value<string>("expression");
        Assert.Contains("typeof opts==='object'&&opts?.ctx?opts.ctx", expression);
        Assert.Contains("const baselineDepth=ctx?.debugDepth+(Array.isArray(ctx?.asyncParents)?ctx.asyncParents.length:0)", expression);
        Assert.Contains("requestStep(\"over\",baselineDepth,locations)", expression);
        Assert.Contains("\"methodToken\":100663298", expression);

        var armed = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 25,
            ["type"] = "response",
            ["request_seq"] = 24,
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "undefined", ["variablesReference"] = 0 }
        }.ToString(Formatting.None));
        Assert.Empty(armed.ClientPayloads);
        var continueRequest = JObject.Parse(Assert.Single(armed.AdapterPayloads));
        Assert.Equal("continue", continueRequest.Value<string>("command"));

        var resumed = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 26,
            ["type"] = "response",
            ["request_seq"] = continueRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "continue",
            ["body"] = new JObject { ["allThreadsContinued"] = true }
        }.ToString(Formatting.None));
        var stepResponse = JObject.Parse(Assert.Single(resumed.ClientPayloads));
        Assert.Equal("next", stepResponse.Value<string>("command"));
        Assert.Equal(24, stepResponse.Value<int>("request_seq"));
        Assert.Null(stepResponse["body"]);

        transform.TransformAdapterToClientDuplex(
            "{\"seq\":27,\"type\":\"event\",\"event\":\"continued\",\"body\":{\"threadId\":1,\"allThreadsContinued\":true}}");
        var stopped = transform.TransformAdapterToClientDuplex(
            "{\"seq\":28,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"pause\",\"threadId\":1,\"allThreadsStopped\":true}}");
        Assert.Empty(stopped.ClientPayloads);
        var stopProbe = JObject.Parse(Assert.Single(stopped.AdapterPayloads));
        Assert.Equal("evaluate", stopProbe.Value<string>("command"));
        Assert.Contains("__FrontEndCSharpDebugger?.currentStop", stopProbe["arguments"]!.Value<string>("expression"));

        var correlatedStop = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 29,
            ["type"] = "response",
            ["request_seq"] = stopProbe.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = JsonConvert.SerializeObject(
                    "[\"step\",null,null,\"module-a\",100663298,4,5]"),
                ["variablesReference"] = 0
            }
        }.ToString(Formatting.None));
        var stoppedEvent = JObject.Parse(Assert.Single(correlatedStop.ClientPayloads));
        Assert.Equal("step", stoppedEvent["body"]!.Value<string>("reason"));

        transform.TransformClientToAdapter(
            "{\"seq\":30,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
        var pendingStack = transform.TransformAdapterToClientDuplex(
            "{\"seq\":31,\"type\":\"response\",\"request_seq\":30,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":801,\"name\":\"pauseAtManagedInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\managed-debugger.js\"},\"line\":517,\"column\":5},{\"id\":803,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5},{\"id\":802,\"name\":\"main\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\site.js\"},\"line\":4,\"column\":1}]}}");
        Assert.Empty(pendingStack.ClientPayloads);
        var stackRequests = pendingStack.AdapterPayloads.Select(JObject.Parse).ToArray();
        var countRequest = Assert.Single(stackRequests);
        Assert.Contains(
            "debugFrames?.length",
            countRequest["arguments"]!.Value<string>("expression"));
        Assert.Equal(801, countRequest["arguments"]!.Value<int>("frameId"));

        var completedStack = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 32,
            ["type"] = "response",
            ["request_seq"] = countRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = "[1,0]",
                ["variablesReference"] = 0
            }
        }.ToString(Formatting.None));
        var stackResponse = JObject.Parse(Assert.Single(completedStack.ClientPayloads));
        var activeFrame = stackResponse["body"]!["stackFrames"]!.First!;
        Assert.Equal("Program.Other", activeFrame.Value<string>("name"));
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Other.cs", activeFrame["source"]!["path"]!.Value<string>());
        Assert.Equal(11, activeFrame.Value<int>("line"));

        transform.TransformClientToAdapter(
            "{\"seq\":33,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1,\"startFrame\":0,\"levels\":1}}");
        var pendingRefresh = transform.TransformAdapterToClientDuplex(
            "{\"seq\":34,\"type\":\"response\",\"request_seq\":33,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"totalFrames\":10,\"stackFrames\":[{\"id\":801,\"name\":\"pauseAtManagedInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\managed-debugger.js\"},\"line\":517,\"column\":5}]}}");
        Assert.Empty(pendingRefresh.ClientPayloads);
        var refreshCountRequest = JObject.Parse(Assert.Single(pendingRefresh.AdapterPayloads));
        var completedRefresh = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 35,
            ["type"] = "response",
            ["request_seq"] = refreshCountRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "[1,0]", ["variablesReference"] = 0 }
        }.ToString(Formatting.None));
        var refreshedFrame = JObject.Parse(
            Assert.Single(completedRefresh.ClientPayloads))["body"]!["stackFrames"]!.First!;
        Assert.Equal("Program.Other", refreshedFrame.Value<string>("name"));
        Assert.Equal(@"C:\Fixtures\FrontEnd.Program\Other.cs", refreshedFrame["source"]!["path"]!.Value<string>());
        Assert.Equal(11, refreshedFrame.Value<int>("line"));

        Assert.Contains("step-request-correlated", diagnostics);
        Assert.Contains("step-arm-complete", diagnostics);
        Assert.Contains("stop-step-correlated", diagnostics);
        Assert.Contains("stack-frame-projected-from-stop", diagnostics);
        Assert.DoesNotContain("stack-active-identity-required", diagnostics);
    }

    [TestCase("stepIn", "in")]
    [TestCase("next", "over")]
    [TestCase("stepOut", "out")]
    public void ManagedStepDiscoversTheRuntimeFrameBeforeArming(
        string command,
        string kind)
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");

        var frameLookup = JObject.Parse(transform.TransformClientToAdapter(new JObject
        {
            ["seq"] = 24,
            ["type"] = "request",
            ["command"] = command,
            ["arguments"] = new JObject { ["threadId"] = 1 }
        }.ToString(Formatting.None)));
        Assert.Equal("stackTrace", frameLookup.Value<string>("command"));
        Assert.Equal(0, frameLookup["arguments"]!.Value<int>("startFrame"));

        var lookupCompleted = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 25,
            ["type"] = "response",
            ["request_seq"] = 24,
            ["success"] = true,
            ["command"] = "stackTrace",
            ["body"] = new JObject
            {
                ["stackFrames"] = new JArray(new JObject
                {
                    ["id"] = 701,
                    ["name"] = "executeInstruction",
                    ["source"] = new JObject { ["path"] = @"C:\Fixtures\FrontEnd.Demo\wwwroot\js\jit-engine\_executeInstruction.js" },
                    ["line"] = 18,
                    ["column"] = 5
                })
            }
        }.ToString(Formatting.None));

        Assert.Empty(lookupCompleted.ClientPayloads);
        var armRequest = JObject.Parse(Assert.Single(lookupCompleted.AdapterPayloads));
        Assert.Equal("evaluate", armRequest.Value<string>("command"));
        Assert.Equal(701, armRequest["arguments"]!.Value<int>("frameId"));
        Assert.Contains(
            $"requestStep(\"{kind}\",baselineDepth,locations)",
            armRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("step-frame-lookup-started", diagnostics);
        Assert.Contains("step-frame-lookup-complete", diagnostics);
    }

    [Test]
    public void ManagedStepFailsWhenThePausedRuntimeFrameCannotBeFound()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");
        transform.TransformClientToAdapter(
            "{\"seq\":24,\"type\":\"request\",\"command\":\"stepOut\",\"arguments\":{\"threadId\":1}}");

        var failed = transform.TransformAdapterToClientDuplex(
            "{\"seq\":25,\"type\":\"response\",\"request_seq\":24,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":702,\"name\":\"main\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\site.js\"},\"line\":4,\"column\":1}]}}");

        Assert.Empty(failed.AdapterPayloads);
        var response = JObject.Parse(Assert.Single(failed.ClientPayloads));
        Assert.Equal("stepOut", response.Value<string>("command"));
        Assert.Equal(24, response.Value<int>("request_seq"));
        Assert.False(response.Value<bool>("success"));
        Assert.Contains("paused managed runtime frame", response.Value<string>("message"));
        Assert.Contains("step-frame-lookup-failed", diagnostics);
    }

    [Test]
    public void DuplexManagedStepPreservesAnInterveningNativeJavaScriptStop()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");
        transform.TransformClientToAdapter(
            "{\"seq\":22,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
        transform.TransformAdapterToClient(
            "{\"seq\":23,\"type\":\"response\",\"request_seq\":22,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":701,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5}]}}");
        transform.TransformClientToAdapter(
            "{\"seq\":24,\"type\":\"request\",\"command\":\"next\",\"arguments\":{\"threadId\":1}}");
        var armed = transform.TransformAdapterToClientDuplex(
            "{\"seq\":25,\"type\":\"response\",\"request_seq\":24,\"success\":true,\"command\":\"evaluate\",\"body\":{\"result\":\"undefined\",\"variablesReference\":0}}");
        var continueRequest = JObject.Parse(Assert.Single(armed.AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 26,
            ["type"] = "response",
            ["request_seq"] = continueRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "continue"
        }.ToString(Formatting.None));

        var stopped = transform.TransformAdapterToClientDuplex(
            "{\"seq\":27,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"allThreadsStopped\":true}}");
        var stopProbe = JObject.Parse(Assert.Single(stopped.AdapterPayloads));
        var released = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 28,
            ["type"] = "response",
            ["request_seq"] = stopProbe.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject { ["result"] = "null", ["variablesReference"] = 0 }
        }.ToString(Formatting.None));

        var nativeStop = JObject.Parse(Assert.Single(released.ClientPayloads));
        Assert.Equal("breakpoint", nativeStop["body"]!.Value<string>("reason"));
        Assert.Contains("stop-step-bypassed-native", diagnostics);

        var laterStopped = transform.TransformAdapterToClientDuplex(
            "{\"seq\":29,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"pause\",\"threadId\":1,\"allThreadsStopped\":true}}");
        Assert.Empty(laterStopped.ClientPayloads);
        Assert.Single(laterStopped.AdapterPayloads);
    }

    [Test]
    public void ManagedExceptionStopCancelsThePendingManagedStep()
    {
        var transform = CreateTransform();
        transform.TransformClientToAdapter(
            "{\"seq\":10,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[\"frontendcsharp-thrown\"]}}");
        var nativeConfiguration = transform.TransformAdapterToClientDuplex(
            "{\"seq\":11,\"type\":\"response\",\"request_seq\":10,\"success\":true,\"command\":\"setExceptionBreakpoints\"}");
        var runtimeConfiguration = JObject.Parse(Assert.Single(nativeConfiguration.AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 12,
            ["type"] = "response",
            ["request_seq"] = runtimeConfiguration.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");
        transform.TransformClientToAdapter(
            "{\"seq\":22,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
        transform.TransformAdapterToClient(
            "{\"seq\":23,\"type\":\"response\",\"request_seq\":22,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":701,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5}]}}");
        transform.TransformClientToAdapter(
            "{\"seq\":24,\"type\":\"request\",\"command\":\"next\",\"arguments\":{\"threadId\":1}}");
        var armed = transform.TransformAdapterToClientDuplex(
            "{\"seq\":25,\"type\":\"response\",\"request_seq\":24,\"success\":true,\"command\":\"evaluate\",\"body\":{\"result\":\"undefined\",\"variablesReference\":0}}");
        var continueRequest = JObject.Parse(Assert.Single(armed.AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 26,
            ["type"] = "response",
            ["request_seq"] = continueRequest.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "continue"
        }.ToString(Formatting.None));

        var pendingException = transform.TransformAdapterToClientDuplex(
            "{\"seq\":27,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"pause\",\"threadId\":1,\"allThreadsStopped\":true}}");
        var exceptionProbe = JObject.Parse(Assert.Single(pendingException.AdapterPayloads));
        var correlatedException = transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 28,
            ["type"] = "response",
            ["request_seq"] = exceptionProbe.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = JsonConvert.SerializeObject(
                    "[\"exception\",\"thrown\",\"System.InvalidOperationException\",\"module-a\",100663297,2,3]"),
                ["variablesReference"] = 0
            }
        }.ToString(Formatting.None));
        Assert.Equal("exception", JObject.Parse(
            Assert.Single(correlatedException.ClientPayloads))["body"]!.Value<string>("reason"));

        transform.TransformClientToAdapter(
            "{\"seq\":30,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[]}}");
        var nativeDisable = transform.TransformAdapterToClientDuplex(
            "{\"seq\":31,\"type\":\"response\",\"request_seq\":30,\"success\":true,\"command\":\"setExceptionBreakpoints\"}");
        var runtimeDisable = JObject.Parse(Assert.Single(nativeDisable.AdapterPayloads));
        transform.TransformAdapterToClientDuplex(new JObject
        {
            ["seq"] = 32,
            ["type"] = "response",
            ["request_seq"] = runtimeDisable.Value<int>("seq"),
            ["success"] = true,
            ["command"] = "evaluate"
        }.ToString(Formatting.None));

        const string laterNativeStop =
            "{\"seq\":33,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"pause\",\"threadId\":1,\"allThreadsStopped\":true}}";
        var forwarded = transform.TransformAdapterToClientDuplex(laterNativeStop);

        Assert.Empty(forwarded.AdapterPayloads);
        Assert.Same(laterNativeStop, Assert.Single(forwarded.ClientPayloads));
    }

    [Test]
    public void ProjectsManagedArgumentsAndLocalsWithoutChangingNativeScopes()
    {
        var diagnostics = new List<string>();
        var transform = CreateTransform(diagnostics.Add);
        transform.TransformClientToAdapter(Request(17, @"C:\Fixtures\FrontEnd.Program\Program.cs", 6));
        transform.TransformAdapterToClient(
            "{\"seq\":20,\"type\":\"response\",\"request_seq\":17,\"success\":true,\"command\":\"setBreakpoints\",\"body\":{\"breakpoints\":[{\"id\":91,\"verified\":true,\"line\":18}]}}");
        transform.TransformAdapterToClient(
            "{\"seq\":21,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"breakpoint\",\"threadId\":1,\"hitBreakpointIds\":[91]}}");
        transform.TransformClientToAdapter(
            "{\"seq\":22,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
        transform.TransformAdapterToClient(
            "{\"seq\":23,\"type\":\"response\",\"request_seq\":22,\"success\":true,\"command\":\"stackTrace\",\"body\":{\"stackFrames\":[{\"id\":701,\"name\":\"executeInstruction\",\"source\":{\"path\":\"C:\\\\Fixtures\\\\FrontEnd.Demo\\\\wwwroot\\\\js\\\\jit-engine\\\\_executeInstruction.js\"},\"line\":18,\"column\":5}]}}");
        var hoverRequest = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":240,\"type\":\"request\",\"command\":\"evaluate\",\"arguments\":{\"expression\":\"result\",\"frameId\":701,\"timeout\":1500,\"evaluationOptions\":{\"noSideEffects\":true,\"forceRealFuncEval\":true}}}"));
        Assert.Equal("evaluate", hoverRequest.Value<string>("command"));
        Assert.Equal(701, hoverRequest["arguments"]!.Value<int>("frameId"));
        Assert.NotEqual("result", hoverRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("frame?.locals?.[0]", hoverRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("Object.keys(scope).find", hoverRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("startsWith(prefix+", hoverRequest["arguments"]!.Value<string>("expression"));
        var memberHoverRequest = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":245,\"type\":\"request\",\"command\":\"evaluate\",\"arguments\":{\"expression\":\"_offset\",\"frameId\":701,\"context\":\"hover\"}}"));
        Assert.NotEqual("_offset", memberHoverRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("const thisKey=Object.keys(scope).find", memberHoverRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("const memberKey=", memberHoverRequest["arguments"]!.Value<string>("expression"));
        var qualifiedMemberHoverRequest = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":246,\"type\":\"request\",\"command\":\"evaluate\",\"arguments\":{\"expression\":\"this._offset\",\"frameId\":701,\"context\":\"hover\"}}"));
        Assert.NotEqual("this._offset", qualifiedMemberHoverRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("const prefix=\"_offset\"", qualifiedMemberHoverRequest["arguments"]!.Value<string>("expression"));
        var watchRequest = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":243,\"type\":\"request\",\"command\":\"evaluate\",\"arguments\":{\"expression\":\"result\",\"frameId\":701,\"context\":\"watch\"}}"));
        Assert.NotEqual("result", watchRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("frame?.locals?.[0]", watchRequest["arguments"]!.Value<string>("expression"));
        var visualStudioWatchRequest = JObject.Parse(transform.TransformClientToAdapter(
            "{\"seq\":244,\"type\":\"request\",\"command\":\"evaluate\",\"arguments\":{\"expression\":\"result\",\"frameId\":701,\"timeout\":5000,\"evaluationOptions\":{\"allowImplicitVars\":true}}}"));
        Assert.NotEqual("result", visualStudioWatchRequest["arguments"]!.Value<string>("expression"));
        Assert.Contains("frame?.locals?.[0]", visualStudioWatchRequest["arguments"]!.Value<string>("expression"));
        var hoverResponse = JObject.Parse(transform.TransformAdapterToClient(new JObject
        {
            ["seq"] = 241,
            ["type"] = "response",
            ["request_seq"] = 240,
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["type"] = "ComplexType",
                ["result"] = "ComplexType {One\uFDD0System.Int32: 1, Two\uFDD0System.Int32: 2}",
                ["variablesReference"] = 450
            }
        }.ToString(Formatting.None)));
        Assert.Equal("{ComplexType}", hoverResponse["body"]!.Value<string>("result"));
        Assert.Equal("ComplexType", hoverResponse["body"]!.Value<string>("type"));
        transform.TransformClientToAdapter(
            "{\"seq\":247,\"type\":\"request\",\"command\":\"evaluate\",\"arguments\":{\"expression\":\"Double\",\"frameId\":701,\"timeout\":5000,\"evaluationOptions\":{\"noSideEffects\":true,\"forceRealFuncEval\":true}}}");
        var unavailableAutoResponse = JObject.Parse(transform.TransformAdapterToClient(new JObject
        {
            ["seq"] = 248,
            ["type"] = "response",
            ["request_seq"] = 247,
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["type"] = "undefined",
                ["result"] = "undefined",
                ["variablesReference"] = 0
            }
        }.ToString(Formatting.None)));
        Assert.False(unavailableAutoResponse.Value<bool>("success"));
        Assert.Null(unavailableAutoResponse["body"]);
        var hoverChildrenRequest = new JObject
        {
            ["seq"] = 242,
            ["type"] = "request",
            ["command"] = "variables",
            ["arguments"] = new JObject { ["variablesReference"] = 450 }
        }.ToString(Formatting.None);
        Assert.Same(hoverChildrenRequest, transform.TransformClientToAdapter(hoverChildrenRequest));
        const string scopesRequest =
            "{\"seq\":24,\"type\":\"request\",\"command\":\"scopes\",\"arguments\":{\"frameId\":701}}";

        var evaluateRequest = JObject.Parse(transform.TransformClientToAdapter(scopesRequest));

        Assert.Equal("evaluate", evaluateRequest.Value<string>("command"));
        Assert.Equal(701, evaluateRequest["arguments"]!.Value<int>("frameId"));
        var expression = evaluateRequest["arguments"]!.Value<string>("expression");
        Assert.Contains("add(scope,\"value\",()=>frame?.methodArgs?.[1]", expression);
        Assert.Contains("add(scope,\"result\",()=>frame?.locals?.[0]", expression);
        Assert.Contains("\"argument\")", expression);
        Assert.Contains("\"local\")", expression);
        Assert.Contains("__FrontEndCSharpDebugger?.currentStop?.frame", expression);
        Assert.Contains("k__BackingField", expression);
        Assert.Contains("Object.defineProperty(prototype,Symbol.toStringTag,{value:type})", expression);
        Assert.Contains("const view=Object.create(prototype)", expression);
        Assert.Contains("const fieldDefinition=storageKey=>", expression);
        Assert.Contains("add(view,name,()=>raw[entry.rawName]", expression);
        Assert.Contains("add(array,String(index),()=>raw[index]", expression);
        Assert.Contains("set:next=>assign(read,write,next)", expression);
        Assert.Contains("const scope=Object.create(null)", expression);
        Assert.DoesNotContain("FEC1", expression);
        Assert.DoesNotContain("JSON.stringify", expression);

        var evaluateResponse = new JObject
        {
            ["seq"] = 25,
            ["type"] = "response",
            ["request_seq"] = 24,
            ["success"] = true,
            ["command"] = "evaluate",
            ["body"] = new JObject
            {
                ["result"] = "Object",
                ["variablesReference"] = 451
            }
        }.ToString(Formatting.None);
        var scopesResponse = JObject.Parse(transform.TransformAdapterToClient(evaluateResponse));
        Assert.True(scopesResponse["body"]?["scopes"] != null,
            scopesResponse.ToString(Formatting.None) + " " + string.Join(",", diagnostics));
        var scopes = scopesResponse["body"]!["scopes"]!.Children<JObject>().ToArray();

        Assert.Equal("scopes", scopesResponse.Value<string>("command"));
        var scope = Assert.Single(scopes);
        Assert.Equal("Locals", scope.Value<string>("name"));
        Assert.Equal(451, scope.Value<int>("variablesReference"));

        var variablesRequest = new JObject
        {
            ["seq"] = 26,
            ["type"] = "request",
            ["command"] = "variables",
            ["arguments"] = new JObject { ["variablesReference"] = 451 }
        }.ToString(Formatting.None);
        Assert.Same(variablesRequest, transform.TransformClientToAdapter(variablesRequest));

        var variablesResponse = new JObject
        {
            ["seq"] = 27,
            ["type"] = "response",
            ["request_seq"] = 26,
            ["success"] = true,
            ["command"] = "variables",
            ["body"] = new JObject
            {
                ["variables"] = new JArray(
                    new JObject
                    {
                        ["name"] = "complex\uFDD2field\uFDD0FrontEnd.Program.ComplexType",
                        ["value"] = "FrontEnd.Program.ComplexType {One: 1, Two: 2}",
                        ["type"] = "Object",
                        ["variablesReference"] = 452
                    },
                    new JObject
                    {
                        ["name"] = "a\uFDD2argument\uFDD0System.Int32",
                        ["value"] = "{int}",
                        ["type"] = "number",
                        ["variablesReference"] = 457
                    },
                    new JObject
                    {
                        ["name"] = "args\uFDD2argument\uFDD0System.String[]\uFDD10",
                        ["value"] = "Array(0)",
                        ["type"] = "Array",
                        ["variablesReference"] = 455
                    },
                    new JObject
                    {
                        ["name"] = "items\uFDD2local\uFDD0System.String[]\uFDD12",
                        ["value"] = "Array(0)",
                        ["type"] = "Array",
                        ["variablesReference"] = 456
                    },
                    new JObject
                    {
                        ["name"] = "Program.NestedLeaf returned",
                        ["value"] = "7",
                        ["presentationHint"] = new JObject
                        {
                            ["vsAttributes"] = new JArray("returnValue")
                        },
                        ["variablesReference"] = 0
                    },
                    new JObject
                    {
                        ["name"] = "[[Prototype]]",
                        ["variablesReference"] = 453
                    })
            }
        }.ToString(Formatting.None);
        var pendingVariables = transform.TransformAdapterToClientDuplex(variablesResponse);
        Assert.Empty(pendingVariables.ClientPayloads);
        var scalarRequest = JObject.Parse(Assert.Single(pendingVariables.AdapterPayloads));
        Assert.Equal("variables", scalarRequest.Value<string>("command"));
        Assert.Equal(457, scalarRequest["arguments"]!.Value<int>("variablesReference"));
        var projectedVariables = JObject.Parse(Assert.Single(
            transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 271,
                ["type"] = "response",
                ["request_seq"] = scalarRequest.Value<int>("seq"),
                ["success"] = true,
                ["command"] = "variables",
                ["body"] = new JObject
                {
                    ["variables"] = new JArray(new JObject
                    {
                        ["name"] = "a",
                        ["value"] = "2",
                        ["type"] = "number",
                        ["variablesReference"] = 0
                    })
                }
            }.ToString(Formatting.None)).ClientPayloads));
        var projectedRows = projectedVariables["body"]!["variables"]!.Children<JObject>().ToArray();
        Assert.Equal(4, projectedRows.Length);
        var projectedComplex = projectedRows.Single(row => row.Value<string>("name") == "complex");
        Assert.Equal("complex", projectedComplex.Value<string>("name"));
        Assert.Equal("{ComplexType}", projectedComplex.Value<string>("value"));
        Assert.Equal("ComplexType", projectedComplex.Value<string>("type"));
        Assert.Equal("field", projectedComplex.Value<string>("__fecsStorageKind"));
        var projectedInteger = projectedRows.Single(row => row.Value<string>("name") == "a");
        Assert.Equal("2", projectedInteger.Value<string>("value"));
        Assert.Equal("int", projectedInteger.Value<string>("type"));
        Assert.Equal(0, projectedInteger.Value<int>("variablesReference"));
        Assert.Equal("argument", projectedInteger.Value<string>("__fecsStorageKind"));
        var projectedEmptyArray = projectedRows.Single(row => row.Value<string>("name") == "args");
        Assert.Equal("{string[0]}", projectedEmptyArray.Value<string>("value"));
        Assert.Equal("string[]", projectedEmptyArray.Value<string>("type"));
        Assert.Equal(0, projectedEmptyArray.Value<int>("variablesReference"));
        Assert.Equal("argument", projectedEmptyArray.Value<string>("__fecsStorageKind"));
        var projectedArray = projectedRows.Single(row => row.Value<string>("name") == "items");
        Assert.Equal("{string[2]}", projectedArray.Value<string>("value"));
        Assert.Equal("string[]", projectedArray.Value<string>("type"));
        Assert.Equal(456, projectedArray.Value<int>("variablesReference"));
        Assert.Equal("local", projectedArray.Value<string>("__fecsStorageKind"));

        var childVariablesRequest = new JObject
        {
            ["seq"] = 28,
            ["type"] = "request",
            ["command"] = "variables",
            ["arguments"] = new JObject { ["variablesReference"] = 452 }
        }.ToString(Formatting.None);
        Assert.Same(childVariablesRequest, transform.TransformClientToAdapter(childVariablesRequest));

        var childVariablesResponse = new JObject
        {
            ["seq"] = 29,
            ["type"] = "response",
            ["request_seq"] = 28,
            ["success"] = true,
            ["command"] = "variables",
            ["body"] = new JObject
            {
                ["variables"] = new JArray(
                    new JObject
                    {
                        ["name"] = "<One>k__BackingField\uFDD0System.Int32",
                        ["value"] = "1",
                        ["type"] = "number",
                        ["variablesReference"] = 0
                    },
                    new JObject
                    {
                        ["name"] = "[[Prototype]]",
                        ["variablesReference"] = 454
                    })
            }
        }.ToString(Formatting.None);
        var projectedChildVariables = JObject.Parse(
            transform.TransformAdapterToClient(childVariablesResponse));
        var projectedChild = Assert.Single(
            projectedChildVariables["body"]!["variables"]!.Children<JObject>());
        Assert.Equal("One", projectedChild.Value<string>("name"));
        Assert.Equal("int", projectedChild.Value<string>("type"));
        Assert.Equal("property", projectedChild["presentationHint"]?.Value<string>("kind"));

        var managedSet = JObject.Parse(transform.TransformClientToAdapter(new JObject
        {
            ["seq"] = 291,
            ["type"] = "request",
            ["command"] = "setVariable",
            ["arguments"] = new JObject
            {
                ["variablesReference"] = 452,
                ["name"] = "One",
                ["value"] = "9"
            }
        }.ToString(Formatting.None)));
        Assert.Equal("setVariable", managedSet.Value<string>("command"));
        Assert.Equal("<One>k__BackingField﷐System.Int32",
            managedSet["arguments"]!.Value<string>("name"));

        var arrayVariablesRequest = new JObject
        {
            ["seq"] = 30,
            ["type"] = "request",
            ["command"] = "variables",
            ["arguments"] = new JObject { ["variablesReference"] = 456 }
        }.ToString(Formatting.None);
        Assert.Same(arrayVariablesRequest, transform.TransformClientToAdapter(arrayVariablesRequest));

        var arrayVariablesResponse = new JObject
        {
            ["seq"] = 31,
            ["type"] = "response",
            ["request_seq"] = 30,
            ["success"] = true,
            ["command"] = "variables",
            ["body"] = new JObject
            {
                ["variables"] = new JArray(
                    new JObject
                    {
                        ["name"] = "0\uFDD0System.String",
                        ["value"] = "\"first\"",
                        ["type"] = "string",
                        ["variablesReference"] = 0
                    },
                    new JObject
                    {
                        ["name"] = "1\uFDD0System.String",
                        ["value"] = "\"second\"",
                        ["type"] = "string",
                        ["variablesReference"] = 0
                    },
                    new JObject
                    {
                        ["name"] = "length",
                        ["value"] = "0",
                        ["type"] = "number",
                        ["variablesReference"] = 0
                    })
            }
        }.ToString(Formatting.None);
        var projectedArrayVariables = JObject.Parse(
            transform.TransformAdapterToClient(arrayVariablesResponse));
        var projectedElements = projectedArrayVariables["body"]!["variables"]!
            .Children<JObject>()
            .ToArray();
        Assert.Equal(new[] { "[0]", "[1]" },
            projectedElements.Select(element => element.Value<string>("name")).ToArray());
        Assert.All(projectedElements, element => Assert.Equal("string", element.Value<string>("type")));

        var arraySet = JObject.Parse(transform.TransformClientToAdapter(new JObject
        {
            ["seq"] = 32,
            ["type"] = "request",
            ["command"] = "setVariable",
            ["arguments"] = new JObject
            {
                ["variablesReference"] = 456,
                ["name"] = "[1]",
                ["value"] = "changed"
            }
        }.ToString(Formatting.None)));
        Assert.Equal("1﷐System.String", arraySet["arguments"]!.Value<string>("name"));

        const string nativeSet =
            "{\"seq\":33,\"type\":\"request\",\"command\":\"setVariable\",\"arguments\":{\"variablesReference\":999,\"name\":\"native\",\"value\":\"3\"}}";
        Assert.Same(nativeSet, transform.TransformClientToAdapter(nativeSet));

        transform.TransformAdapterToClient(
            "{\"seq\":34,\"type\":\"event\",\"event\":\"continued\",\"body\":{\"threadId\":1}}");
        var staleSet = JObject.Parse(transform.TransformClientToAdapter(new JObject
        {
            ["seq"] = 35,
            ["type"] = "request",
            ["command"] = "setVariable",
            ["arguments"] = new JObject
            {
                ["variablesReference"] = 451,
                ["name"] = "a",
                ["value"] = "4"
            }
        }.ToString(Formatting.None)));
        Assert.Equal("evaluate", staleSet.Value<string>("command"));

        const string nativeScopes =
            "{\"seq\":28,\"type\":\"request\",\"command\":\"scopes\",\"arguments\":{\"frameId\":999}}";
        Assert.Same(nativeScopes, transform.TransformClientToAdapter(nativeScopes));
    }

    [Test]
    public void PreservesUncorrelatedResponseByteForByte()
    {
        const string response = "{\"seq\":2,\"type\":\"response\",\"request_seq\":99,\"success\":true,\"command\":\"setBreakpoints\"}";
        var transform = CreateTransform();

        var result = transform.TransformAdapterToClient(response);

        Assert.Same(response, result);
    }

    [Test]
    public void FactoryConfiguresBundledChildSessionFromPublishedSourceMap()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-DapProxy-Bundle-" + Guid.NewGuid().ToString("N"));
        try
        {
            var webRoot = Path.Combine(root, "wwwroot");
            var jsRoot = Path.Combine(webRoot, "js");
            var runtimeSourcePath = Path.Combine(jsRoot, "jit-engine", "_executeInstruction.js");
            var buildIdentityPath = Path.Combine(jsRoot, "debug-build.js");
            Directory.CreateDirectory(jsRoot);
            File.WriteAllText(Path.Combine(jsRoot, "index.js"), "// bundle", Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(jsRoot, "index.js.map"),
                new JObject
                {
                    ["version"] = 3,
                    ["sources"] = new JArray(
                        new Uri(buildIdentityPath).AbsoluteUri,
                        new Uri(runtimeSourcePath).AbsoluteUri),
                    ["sourcesContent"] = new JArray(
                        "export const buildId = \"bundled-build\";",
                        "export function executeInstruction(opts) {\n    return opts;\n}")
                }.ToString(Formatting.None),
                Encoding.UTF8);
            var childLaunch = new JObject
            {
                ["__pendingTargetId"] = "target-1"
            }.ToString();

            Assert.True(DapBreakpointCompositionFactory.TryCreate(
                childLaunch,
                out var transform,
                out var reason,
                additionalRoots: new[] { webRoot }));
            Assert.Equal("configured", reason);
            Assert.NotNull(transform);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public void FactoryConfiguresBundledChildSessionFromNestedPublishedSourceMap()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-DapProxy-NestedBundle-" + Guid.NewGuid().ToString("N"));
        try
        {
            var webRoot = Path.Combine(root, "wwwroot");
            var jsRoot = Path.Combine(webRoot, "js", "react-demo");
            var runtimeSourcePath = Path.Combine(jsRoot, "jit-engine", "_executeInstruction.js");
            var buildIdentityPath = Path.Combine(jsRoot, "debug-build.js");
            var entrySourcePath = Path.Combine(jsRoot, "index.entry.js");
            Directory.CreateDirectory(jsRoot);
            var staleRuntimeDirectory = Path.Combine(webRoot, "js", "jit-engine");
            Directory.CreateDirectory(staleRuntimeDirectory);
            File.WriteAllText(
                Path.Combine(webRoot, "js", "debug-build.js"),
                "export const buildId = \"stale-unbundled-build\";",
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(staleRuntimeDirectory, "_executeInstruction.js"),
                "export function executeInstruction(opts) {\n    return opts;\n}",
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(jsRoot, "index.js"), "// bundle", Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(jsRoot, "index.js.map"),
                new JObject
                {
                    ["version"] = 3,
                    ["sources"] = new JArray(
                        new Uri(buildIdentityPath).AbsoluteUri,
                        new Uri(runtimeSourcePath).AbsoluteUri,
                        new Uri(entrySourcePath).AbsoluteUri),
                    ["sourcesContent"] = new JArray(
                        "export const buildId = \"nested-bundled-build\";",
                        "export function executeInstruction(opts) {\n    return opts;\n}",
                        "throw new Error(\"FrontEndCSharp debugger did not attach within 10000 milliseconds\");")
                }.ToString(Formatting.None),
                Encoding.UTF8);
            var ordinaryJsRoot = Path.Combine(webRoot, "js", "ordinary-demo");
            var ordinaryRuntimeSourcePath = Path.Combine(
                ordinaryJsRoot,
                "jit-engine",
                "_executeInstruction.js");
            Directory.CreateDirectory(ordinaryJsRoot);
            File.WriteAllText(
                Path.Combine(ordinaryJsRoot, "index.js"),
                "// bundle",
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(ordinaryJsRoot, "index.js.map"),
                new JObject
                {
                    ["version"] = 3,
                    ["sources"] = new JArray(
                        new Uri(Path.Combine(ordinaryJsRoot, "debug-build.js")).AbsoluteUri,
                        new Uri(ordinaryRuntimeSourcePath).AbsoluteUri),
                    ["sourcesContent"] = new JArray(
                        "export const buildId = \"ordinary-build\";",
                        "export function executeInstruction(opts) {\n    return opts;\n}")
                }.ToString(Formatting.None),
                Encoding.UTF8);
            var childLaunch = new JObject
            {
                ["__pendingTargetId"] = "target-1"
            }.ToString();

            Assert.True(DapBreakpointCompositionFactory.TryCreate(
                childLaunch,
                out var transform,
                out var reason,
                additionalRoots: new[] { webRoot }));
            Assert.Equal("configured", reason);
            Assert.NotNull(transform);
            var loaded = transform!.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 2,
                ["type"] = "event",
                ["event"] = "loadedSource",
                ["body"] = new JObject
                {
                    ["reason"] = "new",
                    ["source"] = new JObject { ["path"] = runtimeSourcePath }
                }
            }.ToString(Formatting.None));
            var contextSelection = JObject.Parse(Assert.Single(loaded.AdapterPayloads));
            var runtimeConfiguration = transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 4,
                ["type"] = "response",
                ["request_seq"] = contextSelection.Value<int>("seq"),
                ["success"] = true,
                ["command"] = "evaluate",
                ["body"] = new JObject { ["result"] = "undefined", ["variablesReference"] = 0 }
            }.ToString(Formatting.None));
            var runtimeConfigurationRequest = JObject.Parse(
                Assert.Single(runtimeConfiguration.AdapterPayloads));
            var configurationExpression = runtimeConfigurationRequest["arguments"]!
                .Value<string>("expression");
            Assert.Contains("nested-bundled-build", configurationExpression);
            Assert.DoesNotContain("stale-unbundled-build", configurationExpression);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public void FactoryConfiguresOnlyAChildSessionWithMatchingBuildArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "FrontEndCSharp-DapProxy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sourceRoot = Path.Combine(root, "Managed");
            var webRoot = Path.Combine(root, "Web");
            var sourcePath = Path.Combine(sourceRoot, "Program.cs");
            var manifestSourcePath = OperatingSystem.IsWindows()
                ? @"\\?\" + sourcePath
                : sourcePath;
            // Edit-and-Continue compilations live under obj/FrontEndCSharp rather than bin.
            // The active browser build must resolve against that exact manifest identity.
            var manifestDirectory = Path.Combine(
                sourceRoot,
                "obj",
                "FrontEndCSharp",
                "Debug",
                "enc-session");
            var jsRoot = Path.Combine(webRoot, "wwwroot", "js");
            var runtimeDirectory = Path.Combine(jsRoot, "jit-engine");
            Directory.CreateDirectory(manifestDirectory);
            Directory.CreateDirectory(runtimeDirectory);
            File.WriteAllText(Path.Combine(sourceRoot, "Managed.csproj"), "<Project />");
            File.WriteAllText(sourcePath, "class Program {}", Encoding.UTF8);
            File.WriteAllText(Path.Combine(runtimeDirectory, "_executeInstruction.js"),
                "export function executeInstruction(opts) {\n    return opts;\n}", Encoding.UTF8);
            const string buildId = "build-one";
            File.WriteAllText(Path.Combine(jsRoot, "debug-build.js"),
                "export const buildId = \"build-one\";", Encoding.UTF8);
            var checksum = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)));
            var manifest = new JObject
            {
                ["schemaVersion"] = 1,
                ["buildId"] = buildId,
                ["modules"] = new JArray(new JObject
                {
                    ["moduleId"] = "module-one",
                    ["documents"] = new JArray(new JObject
                    {
                        ["id"] = 1,
                        ["path"] = manifestSourcePath,
                        ["checksumAlgorithm"] = "SHA256",
                        ["checksum"] = checksum
                    }),
                    ["methods"] = new JArray(new JObject
                    {
                        ["methodToken"] = 100663297,
                        ["name"] = "Program.Main",
                        ["hasThis"] = false,
                        ["parameters"] = new JArray(new JObject
                        {
                            ["index"] = 0,
                            ["name"] = "value"
                        }),
                        ["instructions"] = new JArray(
                            new JObject
                            {
                                ["ilOffset"] = 1,
                                ["blockIndex"] = 0,
                                ["instructionIndex"] = 0
                            },
                            new JObject
                            {
                                ["ilOffset"] = 5,
                                ["blockIndex"] = 0,
                                ["instructionIndex"] = 1
                            }),
                        ["sequencePoints"] = new JArray(new JObject
                        {
                            ["ilOffset"] = 5,
                            ["documentId"] = 1,
                            ["isHidden"] = false,
                            ["startLine"] = 1,
                            ["startColumn"] = 1,
                            ["endLine"] = 1,
                            ["blockIndex"] = 0,
                            ["instructionIndex"] = 1
                        }),
                        ["localScopes"] = new JArray(new JObject
                        {
                            ["startOffset"] = 0,
                            ["endOffset"] = 10,
                            ["locals"] = new JArray(
                                new JObject
                                {
                                    ["slot"] = 0,
                                    ["name"] = "result"
                                },
                                new JObject
                                {
                                    ["slot"] = 1,
                                    ["name"] = "CS$<>8__locals0",
                                    ["isCompilerGeneratedClosure"] = true,
                                    ["capturedLocals"] = new JArray(new JObject
                                    {
                                        ["name"] = "captured",
                                        ["type"] = "System.Int32",
                                        ["fieldPath"] = new JArray("captured")
                                    })
                                })
                        })
                    })
                })
            };
            var manifestPath = Path.Combine(
                manifestDirectory,
                "Managed.frontendcsharp.debug.json");
            File.WriteAllText(
                manifestPath,
                manifest.ToString(),
                Encoding.UTF8);
            var rootLaunch = new JObject
            {
                ["webRoot"] = webRoot
            }.ToString();
            var childLaunch = new JObject
            {
                ["__pendingTargetId"] = "target-1"
            }.ToString();
            var inheritedRoots = DapBreakpointCompositionFactory.DiscoverCandidateRoots(rootLaunch);

            Assert.True(DapBreakpointCompositionFactory.TryCreate(
                childLaunch,
                out var transform,
                out var reason,
                additionalRoots: inheritedRoots));
            Assert.Equal("configured", reason);

            var exceptionConfiguration = JObject.Parse(transform!.TransformClientToAdapter(
                "{\"seq\":10,\"type\":\"request\",\"command\":\"setExceptionBreakpoints\",\"arguments\":{\"filters\":[\"frontendcsharp-unhandled\"]}}"));
            var pendingExceptionConfiguration = transform.TransformAdapterToClientDuplex(
                new JObject
                {
                    ["seq"] = 11,
                    ["type"] = "response",
                    ["request_seq"] = exceptionConfiguration.Value<int>("seq"),
                    ["success"] = true,
                    ["command"] = "setExceptionBreakpoints"
                }.ToString(Formatting.None));
            var runtimeExceptionConfiguration = JObject.Parse(
                Assert.Single(pendingExceptionConfiguration.AdapterPayloads));
            transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 12,
                ["type"] = "response",
                ["request_seq"] = runtimeExceptionConfiguration.Value<int>("seq"),
                ["success"] = true,
                ["command"] = "evaluate",
                ["body"] = new JObject { ["result"] = "undefined", ["variablesReference"] = 0 }
            }.ToString(Formatting.None));

            var pendingExceptionStop = transform.TransformAdapterToClientDuplex(
                "{\"seq\":13,\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"reason\":\"pause\",\"threadId\":1,\"allThreadsStopped\":true}}");
            var exceptionStopProbe = JObject.Parse(
                Assert.Single(pendingExceptionStop.AdapterPayloads));
            var completedExceptionStop = transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 14,
                ["type"] = "response",
                ["request_seq"] = exceptionStopProbe.Value<int>("seq"),
                ["success"] = true,
                ["command"] = "evaluate",
                ["body"] = new JObject
                {
                    ["result"] = JsonConvert.SerializeObject(
                        "[\"exception\",\"unhandled\",\"System.InvalidOperationException\",\"module-one\",100663297,0,1]"),
                    ["variablesReference"] = 0
                }
            }.ToString(Formatting.None));
            Assert.Equal("exception", JObject.Parse(
                Assert.Single(completedExceptionStop.ClientPayloads))["body"]!.Value<string>("reason"));

            transform.TransformClientToAdapter(
                "{\"seq\":15,\"type\":\"request\",\"command\":\"stackTrace\",\"arguments\":{\"threadId\":1}}");
            var pendingExceptionStack = transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 16,
                ["type"] = "response",
                ["request_seq"] = 15,
                ["success"] = true,
                ["command"] = "stackTrace",
                ["body"] = new JObject
                {
                    ["stackFrames"] = new JArray(new JObject
                    {
                        ["id"] = 901,
                        ["name"] = "executeInstruction",
                        ["source"] = new JObject { ["path"] = Path.Combine(runtimeDirectory, "_executeInstruction.js") },
                        ["line"] = 1,
                        ["column"] = 1
                    })
                }
            }.ToString(Formatting.None));
            var exceptionStackRequests = pendingExceptionStack.AdapterPayloads
                .Select(JObject.Parse)
                .ToArray();
            var exceptionCountRequest = Assert.Single(exceptionStackRequests);
            Assert.Contains(
                "debugFrames?.length",
                exceptionCountRequest["arguments"]!.Value<string>("expression"));
            var completedExceptionStack = transform.TransformAdapterToClientDuplex(new JObject
            {
                ["seq"] = 17,
                ["type"] = "response",
                ["request_seq"] = exceptionCountRequest.Value<int>("seq"),
                ["success"] = true,
                ["command"] = "evaluate",
                ["body"] = new JObject
                {
                    ["result"] = "[1,0]",
                    ["variablesReference"] = 0
                }
            }.ToString(Formatting.None));
            var exceptionFrame = JObject.Parse(
                Assert.Single(completedExceptionStack.ClientPayloads))["body"]!["stackFrames"]!.First!;
            Assert.Equal(manifestSourcePath, exceptionFrame["source"]!["path"]!.Value<string>());
            Assert.Equal(1, exceptionFrame.Value<int>("line"));

            transform.TransformAdapterToClientDuplex(
                "{\"seq\":19,\"type\":\"event\",\"event\":\"continued\",\"body\":{\"threadId\":1,\"allThreadsContinued\":true}}");
            var projected = JObject.Parse(transform!.TransformClientToAdapter(
                Request(1, sourcePath, 1)));
            Assert.Equal(2,
                projected["arguments"]!["breakpoints"]![0]!["line"]!.Value<int>());

            var resolver = new DebugManifestBreakpointResolver(buildId);
            var inspection = resolver.ResolveFrame(
                sourcePath,
                new ManagedRuntimeFrameSnapshot
                {
                    ModuleId = "module-one",
                    MethodToken = 100663297,
                    BlockIndex = 0,
                    InstructionIndex = 0,
                    Arguments = new List<ManagedValueSnapshot>
                    {
                        new ManagedValueSnapshot { Type = "System.Int32", Value = "41" }
                    },
                    Locals = new List<ManagedValueSnapshot>
                    {
                        new ManagedValueSnapshot { Type = "System.Int32", Value = "42" },
                        new ManagedValueSnapshot
                        {
                            Type = "Program+<>c__DisplayClass0_0",
                            Value = "{Program+<>c__DisplayClass0_0}"
                        }
                    }
                });
            Assert.Equal("Program.Main", inspection.MethodName);
            Assert.Equal(manifestSourcePath, inspection.SourcePath);
            Assert.Equal("value", Assert.Single(inspection.Arguments).Name);
            Assert.Equal("result", Assert.Single(inspection.Locals).Name);
            var preloadedResolver = new DebugManifestBreakpointResolver(
                buildId,
                new[] { manifestPath });
            var sourceIndependentInspection = preloadedResolver.ResolveFrame(
                new ManagedRuntimeFrameSnapshot
                {
                    ModuleId = "module-one",
                    MethodToken = 100663297,
                    BlockIndex = 0,
                    InstructionIndex = 1
                });
            Assert.Equal(manifestSourcePath, sourceIndependentInspection.SourcePath);
            Assert.Equal(1, sourceIndependentInspection.Line);
            var layout = resolver.ResolveFrameLayout(
                sourcePath,
                resolver.Resolve(sourcePath, 1));
            Assert.Collection(
                layout.Variables,
                value =>
                {
                    Assert.Equal("value", value.Name);
                    Assert.Equal(ManagedRuntimeValueKind.Argument, value.Kind);
                    Assert.Equal(0, value.Index);
                },
                value =>
                {
                    Assert.Equal("result", value.Name);
                    Assert.Equal(ManagedRuntimeValueKind.Local, value.Kind);
                    Assert.Equal(0, value.Index);
                },
                value =>
                {
                    Assert.Equal("captured", value.Name);
                    Assert.Equal(ManagedRuntimeValueKind.LocalField, value.Kind);
                    Assert.Equal(1, value.Index);
                    Assert.Equal(new[] { "captured" }, value.FieldPath);
                });

            Assert.False(DapBreakpointCompositionFactory.TryCreate(
                rootLaunch,
                out _,
                out var rootReason));
            Assert.Equal("not-browser-child", rootReason);

            File.WriteAllText(sourcePath, "class Changed {}", Encoding.UTF8);
            Assert.Throws<InvalidDataException>(() => preloadedResolver.ResolveFrame(
                new ManagedRuntimeFrameSnapshot
                {
                    ModuleId = "module-one",
                    MethodToken = 100663297,
                    BlockIndex = 0,
                    InstructionIndex = 1
                }));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public async Task WaitsForTheStandardF5DebuggerArmBeforeSelectingBuildArtifacts()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp",
            "DebuggerIntegration",
            "BuildArmTests-" + Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "Demo", "Web", "wwwroot");
        var armPath = Path.Combine(root, ".vs", "FrontEndCSharp", "debugger.arm");
        Directory.CreateDirectory(webRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(armPath)!);
        try
        {
            File.WriteAllText(armPath, "armed", Encoding.ASCII);
            Assert.Equal(
                Path.GetFullPath(armPath),
                Assert.Single(DapBreakpointCompositionFactory.DiscoverDebuggerArmPaths(
                    new[] { webRoot })));

            var release = Task.Run(async () =>
            {
                await Task.Delay(100);
                File.Delete(armPath);
            });

            Assert.True(DapBreakpointCompositionFactory.WaitForDebuggerBuildCompletion(
                new[] { webRoot },
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(10)));
            await release;

            File.WriteAllText(armPath, "stale", Encoding.ASCII);
            Assert.False(DapBreakpointCompositionFactory.WaitForDebuggerBuildCompletion(
                new[] { webRoot },
                TimeSpan.FromMilliseconds(75),
                TimeSpan.FromMilliseconds(10)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static DapBreakpointCompositionTransform CreateTransform(
        Action<string>? diagnosticSink = null,
        string? expectedBuildId = null) => new(
        new StubResolver(),
        @"C:\Fixtures\FrontEnd.Demo\wwwroot\js\jit-engine\_executeInstruction.js",
        18,
        diagnosticSink,
        expectedBuildId);

    private static string Request(int sequence, string sourcePath, int line) =>
        new JObject
        {
            ["seq"] = sequence,
            ["type"] = "request",
            ["command"] = "setBreakpoints",
            ["arguments"] = new JObject
            {
                ["source"] = new JObject { ["path"] = sourcePath },
                ["breakpoints"] = new JArray(new JObject { ["line"] = line })
            }
        }.ToString(Newtonsoft.Json.Formatting.None);

    private sealed class StubResolver : IManagedBreakpointResolver
    {
        private readonly string? ownedFileName;

        public StubResolver(string? ownedFileName = null)
        {
            this.ownedFileName = ownedFileName;
        }

        public bool OwnsSource(string sourcePath) =>
            ownedFileName != null
                ? sourcePath.EndsWith(ownedFileName, StringComparison.OrdinalIgnoreCase)
                : sourcePath.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase)
                    || sourcePath.EndsWith("Other.cs", StringComparison.OrdinalIgnoreCase);

        public ManagedBreakpointResolution Resolve(string sourcePath, int requestedLine)
        {
            if (requestedLine == 99)
            {
                return ManagedBreakpointResolution.Unresolved(
                    "No executable sequence point.",
                    requestedLine);
            }

            return sourcePath.EndsWith("Other.cs", StringComparison.OrdinalIgnoreCase)
                || requestedLine == 21
                ? ManagedBreakpointResolution.Resolved(
                    "module-a", 100663298, 4, 5, 11, 2)
                : ManagedBreakpointResolution.Resolved(
                    "module-a", 100663297, 2, 3, 7, 9);
        }

        public ManagedFrameInspection ResolveFrame(
            string sourcePath,
            ManagedRuntimeFrameSnapshot frame) => frame.MethodToken == 100663298
                ? new ManagedFrameInspection(
                    "Program.Other",
                    @"C:\Fixtures\FrontEnd.Program\Other.cs",
                    11,
                    2,
                    Array.Empty<ManagedVariable>(),
                    new[] { new ManagedVariable("nested", "3", "System.Int32") })
                : new ManagedFrameInspection(
                    "Program.Main",
                    sourcePath,
                    7,
                    9,
                    new[] { new ManagedVariable("value", "1", "System.Int32") },
                    new[] { new ManagedVariable("result", "2", "System.Int32") });

        public ManagedFrameInspection ResolveFrame(ManagedRuntimeFrameSnapshot frame) =>
            ResolveFrame(
                frame.MethodToken == 100663298
                    ? @"C:\Fixtures\FrontEnd.Program\Other.cs"
                    : @"C:\Fixtures\FrontEnd.Program\Program.cs",
                frame);

        public ManagedFrameLayout ResolveFrameLayout(
            string sourcePath,
            ManagedBreakpointResolution resolution) => resolution.MethodToken == 100663298
                ? new ManagedFrameLayout(new[]
                {
                    new ManagedRuntimeVariable("nested", ManagedRuntimeValueKind.Local, 0)
                })
                : new ManagedFrameLayout(new[]
                {
                    new ManagedRuntimeVariable("this", ManagedRuntimeValueKind.Argument, 0),
                    new ManagedRuntimeVariable("value", ManagedRuntimeValueKind.Argument, 1),
                    new ManagedRuntimeVariable("result", ManagedRuntimeValueKind.Local, 0)
                });

        public IReadOnlyList<ManagedRuntimeLocation> ResolveStepLocations(string sourcePath) =>
            new[]
            {
                new ManagedRuntimeLocation("module-a", 100663297, 2, 3),
                new ManagedRuntimeLocation("module-a", 100663298, 4, 5)
            };
    }
}
