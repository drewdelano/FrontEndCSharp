using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedStackExpansionCoordinator
{
    private const string ManagedStackCountsExpression =
        "(()=>{const ctx=typeof opts==='object'&&opts?.ctx?opts.ctx:"
        + "globalThis.__FrontEndCSharpDebugger?.currentStop?.frame?.ctx;"
        + "return JSON.stringify([ctx?.debugFrames?.length??0,ctx?.asyncParents?.length??0]);})()";
    private const string ActiveManagedFrameIdentityExpression =
        "(()=>{const frame=typeof opts==='object'&&opts?.ctx?opts:"
        + "globalThis.__FrontEndCSharpDebugger?.currentStop?.frame;"
        + "return JSON.stringify([frame?.methodInfo?.debugModuleId??'',"
        + "frame?.methodInfo?.debugMethodToken??0,frame?.currentBlockIndex??0,"
        + "frame?.instructionIndex??0]);})()";
    private readonly ManagedStackState state;
    private readonly ManagedRuntimeCoordinator runtime;
    private readonly Action<string>? diagnosticSink;

    internal ManagedStackExpansionCoordinator(
        ManagedStackState state,
        ManagedRuntimeCoordinator runtime,
        Action<string>? diagnosticSink)
    {
        this.state = state;
        this.runtime = runtime;
        this.diagnosticSink = diagnosticSink;
    }

    internal ManagedFrameProjection? ResolveFrameProjection(
        ManagedRuntimeFrameSnapshot? frame,
        IEnumerable<string> sourcePaths) => ManagedFrameProjectionResolver.Resolve(
            frame,
            runtime.Resolver,
            sourcePaths);

    internal bool TryHandleEvaluationResponse(
        JObject message,
        Func<int> allocateSequence,
        out DapTransformDispatch dispatch)
    {
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int countSequence
            && state.PendingAsyncParentCounts.TryGetValue(countSequence, out var pendingCount))
        {
            state.PendingAsyncParentCounts.Remove(countSequence);
            IReadOnlyList<string> followUps = Array.Empty<string>();
            try
            {
                if (message.Value<bool?>("success") == true)
                {
                    var counts = ManagedStackFrameParser.ParseStackCounts(
                        message["body"]?.Value<string>("result"));
                    if (counts.PhysicalCount > 1 || counts.AsyncCount > 0)
                    {
                        followUps = ArmManagedParentFrames(
                            pendingCount.Stack,
                            pendingCount.BackingFrameId,
                            counts.PhysicalCount,
                            counts.AsyncCount,
                            allocateSequence);
                    }
                }
            }
            catch (Exception exception) when (
                exception is JsonException
                || exception is InvalidDataException
                || exception is ArgumentException)
            {
                diagnosticSink?.Invoke("stack-logical-count-unavailable");
            }
            finally
            {
                pendingCount.Stack.RemainingFrames--;
            }
            dispatch = pendingCount.Stack.RemainingFrames > 0
                ? new DapTransformDispatch(Array.Empty<string>(), followUps)
                : CompleteNestedStack(pendingCount.Stack);
            return true;
        }

        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int frameSequence
            && state.PendingNestedFrames.TryGetValue(frameSequence, out var pendingFrame))
        {
            state.PendingNestedFrames.Remove(frameSequence);
            var followUps = CompleteNestedFrame(message, pendingFrame);
            dispatch = pendingFrame.Stack.RemainingFrames > 0
                ? new DapTransformDispatch(Array.Empty<string>(), followUps)
                : CompleteNestedStack(pendingFrame.Stack);
            return true;
        }

        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int logicalSequence
            && state.PendingLogicalFrames.TryGetValue(logicalSequence, out var pendingLogicalFrame))
        {
            state.PendingLogicalFrames.Remove(logicalSequence);
            CompleteLogicalFrame(message, pendingLogicalFrame);
            dispatch = pendingLogicalFrame.Stack.RemainingFrames > 0
                ? EmptyDispatch()
                : CompleteNestedStack(pendingLogicalFrame.Stack);
            return true;
        }

        dispatch = null!;
        return false;
    }

    internal DapTransformDispatch BeginProjection(
        string transformed,
        PendingStackTrace? stackRequest,
        Func<int> allocateSequence)
    {
        JObject transformedMessage;
        try
        {
            transformedMessage = JObject.Parse(transformed);
        }
        catch (JsonException)
        {
            return ClientOnly(transformed);
        }
        if (!string.Equals(transformedMessage.Value<string>("type"), "response", StringComparison.Ordinal)
            || !string.Equals(transformedMessage.Value<string>("command"), "stackTrace", StringComparison.Ordinal)
            || stackRequest == null)
        {
            return ClientOnly(transformed);
        }

        var frames = (transformedMessage["body"] as JObject)?["stackFrames"] as JArray
            ?? new JArray();
        var runtimeFrames = frames.Children<JObject>()
            .Where(frame => runtime.OwnsRuntimeSourcePath(
                frame["source"]?.Value<string>("path")))
            .Where(frame => frame.Value<int?>("id").HasValue)
            .ToList();
        var activeFrame = stackRequest.StartFrame == 0 && state.ActiveFrameId.HasValue
            ? frames.Children<JObject>().FirstOrDefault(
                frame => frame.Value<int?>("id") == state.ActiveFrameId.Value)
            : null;
        if (activeFrame == null
            && stackRequest.StartFrame == 0
            && (state.ActiveStopNeedsIdentity || state.ActiveProjection != null))
        {
            activeFrame = state.ActiveProjection != null
                ? frames.Children<JObject>()
                    .FirstOrDefault(frame => frame.Value<int?>("id").HasValue)
                : runtimeFrames.FirstOrDefault()
                    ?? frames.Children<JObject>()
                        .FirstOrDefault(frame => frame.Value<int?>("id").HasValue);
        }
        if (activeFrame == null)
        {
            if (stackRequest.StartFrame > 0 && runtimeFrames.Count > 0)
            {
                foreach (var runtimeFrame in runtimeFrames)
                {
                    runtimeFrame.Remove();
                }
                if (transformedMessage["body"]?["totalFrames"]?.Value<int?>() is int totalFrames)
                {
                    transformedMessage["body"]!["totalFrames"] =
                        Math.Max(0, totalFrames - runtimeFrames.Count);
                }
                diagnosticSink?.Invoke("stack-page-runtime-implementation-hidden");
                return ClientOnly(transformedMessage.ToString(Formatting.None));
            }
            return ClientOnly(transformed);
        }

        var hiddenRuntimeFrames = runtimeFrames
            .Where(frame => !ReferenceEquals(frame, activeFrame))
            .ToList();
        foreach (var runtimeFrame in hiddenRuntimeFrames)
        {
            runtimeFrame.Remove();
        }
        if (hiddenRuntimeFrames.Count > 0
            && transformedMessage["body"]?["totalFrames"]?.Value<int?>() is int topPageTotalFrames)
        {
            transformedMessage["body"]!["totalFrames"] =
                Math.Max(0, topPageTotalFrames - hiddenRuntimeFrames.Count);
        }
        var needsActiveIdentity = state.ActiveStopNeedsIdentity;
        var backingFrameId = activeFrame.Value<int>("id");
        if (state.ActiveProjection != null)
        {
            activeFrame["name"] = state.ActiveProjection.MethodName;
            activeFrame["source"] = state.ActiveProjection.Binding.Source.DeepClone();
            activeFrame["line"] = state.ActiveProjection.Binding.Resolution.Line;
            activeFrame["column"] = state.ActiveProjection.Binding.Resolution.Column;
            state.ProjectedFrames[backingFrameId] = state.ActiveProjection.Binding;
            state.ActiveFrameId = backingFrameId;
            diagnosticSink?.Invoke("stack-frame-projected-from-stop");
        }
        var stack = new PendingNestedStack(
            transformedMessage,
            stackRequest.Binding,
            new[] { activeFrame },
            1 + (needsActiveIdentity ? 1 : 0));
        var adapterPayloads = new List<string>();
        if (needsActiveIdentity)
        {
            var sequence = allocateSequence();
            state.PendingNestedFrames[sequence] = new PendingNestedFrame(
                stack,
                activeFrame,
                true);
            adapterPayloads.Add(new JObject
            {
                ["seq"] = sequence,
                ["type"] = "request",
                ["command"] = "evaluate",
                ["arguments"] = new JObject
                {
                    ["expression"] = ActiveManagedFrameIdentityExpression,
                    ["frameId"] = backingFrameId,
                    ["context"] = "watch"
                }
            }.ToString(Formatting.None));
        }
        var stackCountSequence = allocateSequence();
        state.PendingAsyncParentCounts[stackCountSequence] = new PendingAsyncParentCount(
            stack,
            backingFrameId);
        adapterPayloads.Add(new JObject
        {
            ["seq"] = stackCountSequence,
            ["type"] = "request",
            ["command"] = "evaluate",
            ["arguments"] = new JObject
            {
                ["expression"] = ManagedStackCountsExpression,
                ["frameId"] = backingFrameId,
                ["context"] = "watch"
            }
        }.ToString(Formatting.None));
        diagnosticSink?.Invoke("stack-nested-projection-started");
        return new DapTransformDispatch(Array.Empty<string>(), adapterPayloads);
    }

    private static DapTransformDispatch ClientOnly(string payload) =>
        new DapTransformDispatch(new[] { payload }, Array.Empty<string>());

    private static DapTransformDispatch EmptyDispatch() =>
        new DapTransformDispatch(Array.Empty<string>(), Array.Empty<string>());

    private IReadOnlyList<string> CompleteNestedFrame(
        JObject response,
        PendingNestedFrame pending)
    {
        IReadOnlyList<string> followUps = Array.Empty<string>();
        var stage = "evaluate";
        try
        {
            if (response.Value<bool?>("success") != true)
            {
                throw new InvalidOperationException("The nested frame identity evaluation failed.");
            }
            var result = response["body"]?.Value<string>("result");
            stage = "identity-" + ManagedStackFrameParser.DescribeIdentityResult(result);
            var snapshot = ManagedStackFrameParser.ParseFrameIdentity(result, out _);
            diagnosticSink?.Invoke("stack-nested-identity-parsed");
            stage = "resolve";
            var activeSourcePath = pending.Stack.ActiveBinding.Source.Value<string>("path")
                ?? throw new InvalidOperationException("The active managed source path is unavailable.");
            var inspection = runtime.Resolver.ResolveFrame(
                activeSourcePath,
                snapshot);
            stage = "source";
            if (string.IsNullOrWhiteSpace(inspection.SourcePath))
            {
                pending.Frame.Remove();
                diagnosticSink?.Invoke("stack-nested-frame-hidden-source-less");
            }
            else
            {
                var source = new JObject
                {
                    ["name"] = System.IO.Path.GetFileName(inspection.SourcePath),
                    ["path"] = inspection.SourcePath
                };
                var resolution = ManagedBreakpointResolution.Resolved(
                    snapshot.ModuleId,
                    snapshot.MethodToken,
                    snapshot.BlockIndex,
                    snapshot.InstructionIndex,
                    inspection.Line,
                    inspection.Column);
                pending.Frame["name"] = inspection.MethodName;
                pending.Frame["source"] = source;
                pending.Frame["line"] = inspection.Line;
                pending.Frame["column"] = inspection.Column;
                if (pending.Frame.Value<int?>("id") is int frameId)
                {
                    var binding = new BreakpointBinding(source, resolution);
                    state.ProjectedFrames[frameId] = binding;
                    if (pending.IsActiveFrame)
                    {
                        state.ActiveFrameId = frameId;
                        // A multiplexed physical breakpoint initially correlates to
                        // the group's first logical binding. Once the suspended
                        // runtime frame supplies its exact managed identity, retain
                        // that binding for every later stack refresh in this stop.
                        state.ActiveBinding = binding;
                    }
                }
                diagnosticSink?.Invoke("stack-nested-frame-projected");
            }
        }
        catch (Exception exception) when (
            exception is JsonException
            || exception is InvalidOperationException
            || exception is InvalidDataException
            || exception is ArgumentException)
        {
            diagnosticSink?.Invoke("stack-nested-frame-unavailable-" + stage);
        }
        finally
        {
            pending.Stack.RemainingFrames--;
            if (pending.Stack.RemainingFrames == 0)
            {
                state.ActiveStopNeedsIdentity = false;
            }
        }
        return followUps;
    }

    private IReadOnlyList<string> ArmManagedParentFrames(
        PendingNestedStack stack,
        int backingFrameId,
        int physicalCount,
        int asyncCount,
        Func<int> allocateSequence)
    {
        var parentCount = Math.Max(0, physicalCount - 1) + asyncCount;
        if (parentCount > ManagedStackFrameParser.MaxLogicalFramesPerStack)
        {
            throw new InvalidDataException("The managed stack contains too many frames.");
        }
        var requests = new List<string>(parentCount);
        stack.RemainingFrames += parentCount;
        var outputIndex = 0;
        for (var index = 1; index < physicalCount; index++, outputIndex++)
        {
            var sequence = allocateSequence();
            state.PendingLogicalFrames[sequence] = new PendingLogicalFrame(
                stack,
                backingFrameId,
                index,
                outputIndex,
                isSnapshot: false);
            requests.Add(new JObject
            {
                ["seq"] = sequence,
                ["type"] = "request",
                ["command"] = "evaluate",
                ["arguments"] = new JObject
                {
                    ["expression"] = ManagedStackFrameParser.BuildParentIdentityExpression(
                        index,
                        isSnapshot: false),
                    ["frameId"] = backingFrameId,
                    ["context"] = "watch"
                }
            }.ToString(Formatting.None));
        }
        for (var index = 0; index < asyncCount; index++, outputIndex++)
        {
            var sequence = allocateSequence();
            state.PendingLogicalFrames[sequence] = new PendingLogicalFrame(
                stack,
                backingFrameId,
                index,
                outputIndex,
                isSnapshot: true);
            requests.Add(new JObject
            {
                ["seq"] = sequence,
                ["type"] = "request",
                ["command"] = "evaluate",
                ["arguments"] = new JObject
                {
                    ["expression"] = ManagedStackFrameParser.BuildParentIdentityExpression(
                        index,
                        isSnapshot: true),
                    ["frameId"] = backingFrameId,
                    ["context"] = "watch"
                }
            }.ToString(Formatting.None));
        }
        diagnosticSink?.Invoke("stack-logical-projection-started");
        return requests;
    }

    private void CompleteLogicalFrame(JObject response, PendingLogicalFrame pending)
    {
        var stage = "evaluate";
        try
        {
            if (response.Value<bool?>("success") != true)
            {
                throw new InvalidOperationException("The logical frame identity evaluation failed.");
            }
            var result = response["body"]?.Value<string>("result");
            stage = "identity-" + ManagedStackFrameParser.DescribeIdentityResult(result);
            var snapshot = ManagedStackFrameParser.ParseFrameIdentity(result, out _);
            snapshot.IsLogical = true;
            stage = "resolve";
            var inspection = runtime.Resolver.ResolveFrame(snapshot);
            stage = "source";
            if (string.IsNullOrWhiteSpace(inspection.SourcePath))
            {
                diagnosticSink?.Invoke("stack-logical-frame-hidden-source-less");
                return;
            }
            var source = new JObject
            {
                ["name"] = System.IO.Path.GetFileName(inspection.SourcePath),
                ["path"] = inspection.SourcePath
            };
            var resolution = ManagedBreakpointResolution.Resolved(
                snapshot.ModuleId,
                snapshot.MethodToken,
                snapshot.BlockIndex,
                snapshot.InstructionIndex,
                inspection.Line,
                inspection.Column);
            var frameId = state.NextProxyFrameId--;
            pending.Stack.LogicalFrames[pending.OutputIndex] = new JObject
            {
                ["id"] = frameId,
                ["name"] = inspection.MethodName,
                ["source"] = source.DeepClone(),
                ["line"] = inspection.Line,
                ["column"] = inspection.Column
            };
            state.LogicalFrames[frameId] = new LogicalFrameProjection(
                new BreakpointBinding(source, resolution),
                pending.BackingFrameId,
                pending.Index,
                pending.IsSnapshot);
            diagnosticSink?.Invoke("stack-logical-frame-projected");
        }
        catch (Exception exception) when (
            exception is JsonException
            || exception is InvalidOperationException
            || exception is InvalidDataException
            || exception is ArgumentException)
        {
            diagnosticSink?.Invoke("stack-logical-frame-unavailable-" + stage);
        }
        finally
        {
            pending.Stack.RemainingFrames--;
            if (pending.Stack.RemainingFrames == 0)
            {
                state.ActiveStopNeedsIdentity = false;
            }
        }
    }

    private DapTransformDispatch CompleteNestedStack(PendingNestedStack stack)
    {
        var frames = (stack.Response["body"] as JObject)?["stackFrames"] as JArray;
        if (frames != null && stack.LogicalFrames.Count > 0)
        {
            var lastPhysicalIndex = stack.PhysicalFrames
                .Where(frame => frame.Parent == frames)
                .Select(frame => frames.IndexOf(frame))
                .DefaultIfEmpty(-1)
                .Max();
            var insertionIndex = lastPhysicalIndex + 1;
            foreach (var logicalFrame in stack.LogicalFrames.OrderBy(item => item.Key))
            {
                frames.Insert(insertionIndex++, logicalFrame.Value);
            }
        }
        if (frames != null
            && stack.Response["body"]?["totalFrames"]?.Value<int?>() is int totalFrames)
        {
            stack.Response["body"]!["totalFrames"] =
                totalFrames + frames.Count - stack.InitialFrameCount;
        }
        state.ActiveStopNeedsIdentity = false;
        diagnosticSink?.Invoke("stack-nested-projection-complete");
        return ClientOnly(stack.Response.ToString(Formatting.None));
    }


}
