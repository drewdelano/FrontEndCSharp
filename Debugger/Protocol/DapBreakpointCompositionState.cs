using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedVariableIdentity : IEquatable<ManagedVariableIdentity>
{
    internal ManagedVariableIdentity(int reference, string name)
    {
        Reference = reference;
        Name = name;
    }

    internal int Reference { get; }
    internal string Name { get; }

    public bool Equals(ManagedVariableIdentity? other) => other != null
        && Reference == other.Reference
        && string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is ManagedVariableIdentity other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (Reference * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
        }
    }
}

internal sealed class SourceBreakpoints
{
    internal SourceBreakpoints(JObject source, IReadOnlyList<RequestedBreakpoint> breakpoints)
    {
        Source = source;
        Breakpoints = breakpoints;
    }

    internal JObject Source { get; }
    internal IReadOnlyList<RequestedBreakpoint> Breakpoints { get; }
}

internal sealed class RequestedBreakpoint
{
    internal RequestedBreakpoint(int requestedLine, ManagedBreakpointResolution resolution)
    {
        RequestedLine = requestedLine;
        Resolution = resolution;
    }

    internal int RequestedLine { get; }
    internal ManagedBreakpointResolution Resolution { get; }
}

internal sealed class AggregateBreakpoint
{
    internal AggregateBreakpoint(
        string sourceKey,
        JObject source,
        RequestedBreakpoint breakpoint)
    {
        SourceKey = sourceKey;
        Source = source;
        Breakpoint = breakpoint;
    }

    internal string SourceKey { get; }
    internal JObject Source { get; }
    internal RequestedBreakpoint Breakpoint { get; }
}

internal sealed class AggregateBreakpointBinding
{
    internal AggregateBreakpointBinding(
        AggregateBreakpoint breakpoint,
        BreakpointBinding binding)
    {
        Breakpoint = breakpoint;
        Binding = binding;
    }

    internal AggregateBreakpoint Breakpoint { get; }
    internal BreakpointBinding Binding { get; }
}

internal sealed class PendingRequest
{
    internal PendingRequest(
        JObject source,
        IReadOnlyList<PendingBreakpoint> breakpoints,
        IReadOnlyList<BreakpointBindingGroup> aggregate)
    {
        Source = source;
        Breakpoints = breakpoints;
        Aggregate = aggregate;
    }

    internal JObject Source { get; }
    internal IReadOnlyList<PendingBreakpoint> Breakpoints { get; }
    internal IReadOnlyList<BreakpointBindingGroup> Aggregate { get; }
}

internal sealed class PendingBreakpoint
{
    internal PendingBreakpoint(
        RequestedBreakpoint request,
        int? aggregateIndex,
        int? logicalId)
    {
        Request = request;
        AggregateIndex = aggregateIndex;
        LogicalId = logicalId;
    }

    internal RequestedBreakpoint Request { get; }
    internal int? AggregateIndex { get; }
    internal int? LogicalId { get; }
}

internal sealed class BreakpointBinding
{
    internal BreakpointBinding(
        JObject source,
        ManagedBreakpointResolution resolution,
        int? logicalId = null)
    {
        Source = (JObject)source.DeepClone();
        Resolution = resolution;
        LogicalId = logicalId;
    }

    internal JObject Source { get; }
    internal ManagedBreakpointResolution Resolution { get; }
    internal int? LogicalId { get; }
}

internal sealed class BreakpointBindingGroup
{
    internal BreakpointBindingGroup(IReadOnlyList<BreakpointBinding> bindings)
    {
        if (bindings.Count == 0)
        {
            throw new ArgumentException(
                "A physical breakpoint must represent at least one managed breakpoint.",
                nameof(bindings));
        }

        Bindings = bindings;
    }

    internal IReadOnlyList<BreakpointBinding> Bindings { get; }
}

internal sealed class ManagedFrameProjection
{
    internal ManagedFrameProjection(string methodName, BreakpointBinding binding)
    {
        MethodName = methodName;
        Binding = binding;
    }

    internal string MethodName { get; }
    internal BreakpointBinding Binding { get; }
}

internal sealed class PendingStackTrace
{
    internal PendingStackTrace(BreakpointBinding binding, int startFrame)
    {
        Binding = binding;
        StartFrame = startFrame;
    }

    internal BreakpointBinding Binding { get; }
    internal int StartFrame { get; }
}

internal sealed class PendingManagedScalarMaterialization
{
    internal PendingManagedScalarMaterialization(JObject response, int remaining)
    {
        Response = response;
        Remaining = remaining;
    }

    internal JObject Response { get; }
    internal int Remaining { get; set; }
}

internal sealed class PendingManagedScalarValue
{
    internal PendingManagedScalarValue(
        PendingManagedScalarMaterialization materialization,
        JObject variable)
    {
        Materialization = materialization;
        Variable = variable;
    }

    internal PendingManagedScalarMaterialization Materialization { get; }
    internal JObject Variable { get; }
}

internal sealed class PendingNestedStack
{
    internal PendingNestedStack(
        JObject response,
        BreakpointBinding activeBinding,
        IReadOnlyList<JObject> physicalFrames,
        int remainingFrames)
    {
        Response = response;
        ActiveBinding = activeBinding;
        PhysicalFrames = physicalFrames;
        InitialFrameCount = (response["body"]?["stackFrames"] as JArray)?.Count ?? 0;
        RemainingFrames = remainingFrames;
    }

    internal JObject Response { get; }
    internal BreakpointBinding ActiveBinding { get; }
    internal IReadOnlyList<JObject> PhysicalFrames { get; }
    internal int InitialFrameCount { get; }
    internal SortedDictionary<int, JObject> LogicalFrames { get; } =
        new SortedDictionary<int, JObject>();
    internal int RemainingFrames { get; set; }
}

internal sealed class PendingNestedFrame
{
    internal PendingNestedFrame(
        PendingNestedStack stack,
        JObject frame,
        bool isActiveFrame)
    {
        Stack = stack;
        Frame = frame;
        IsActiveFrame = isActiveFrame;
    }

    internal PendingNestedStack Stack { get; }
    internal JObject Frame { get; }
    internal bool IsActiveFrame { get; }
}

internal sealed class PendingLogicalFrame
{
    internal PendingLogicalFrame(
        PendingNestedStack stack,
        int backingFrameId,
        int index,
        int outputIndex,
        bool isSnapshot)
    {
        Stack = stack;
        BackingFrameId = backingFrameId;
        Index = index;
        OutputIndex = outputIndex;
        IsSnapshot = isSnapshot;
    }

    internal PendingNestedStack Stack { get; }
    internal int BackingFrameId { get; }
    internal int Index { get; }
    internal int OutputIndex { get; }
    internal bool IsSnapshot { get; }
}

internal sealed class PendingAsyncParentCount
{
    internal PendingAsyncParentCount(PendingNestedStack stack, int backingFrameId)
    {
        Stack = stack;
        BackingFrameId = backingFrameId;
    }

    internal PendingNestedStack Stack { get; }
    internal int BackingFrameId { get; }
}

internal sealed class LogicalFrameProjection
{
    internal LogicalFrameProjection(
        BreakpointBinding binding,
        int backingFrameId,
        int index,
        bool isSnapshot)
    {
        Binding = binding;
        BackingFrameId = backingFrameId;
        Index = index;
        IsSnapshot = isSnapshot;
    }

    internal BreakpointBinding Binding { get; }
    internal int BackingFrameId { get; }
    internal int Index { get; }
    internal bool IsSnapshot { get; }
}

internal sealed class ManagedStackCounts
{
    internal ManagedStackCounts(int physicalCount, int asyncCount)
    {
        PhysicalCount = physicalCount;
        AsyncCount = asyncCount;
    }

    internal int PhysicalCount { get; }
    internal int AsyncCount { get; }
}

internal sealed class PendingManagedStep
{
    internal PendingManagedStep(
        int originalSequence,
        string command,
        int threadId,
        BreakpointBinding anchorBinding,
        string expression)
    {
        OriginalSequence = originalSequence;
        Command = command;
        ThreadId = threadId;
        AnchorBinding = anchorBinding;
        Expression = expression;
    }

    internal int OriginalSequence { get; }
    internal string Command { get; }
    internal int ThreadId { get; }
    internal BreakpointBinding AnchorBinding { get; }
    internal string Expression { get; }
}

internal sealed class PendingManagedStepStop
{
    internal PendingManagedStepStop(
        string originalPayload,
        JObject @event,
        PendingManagedStep? step)
    {
        OriginalPayload = originalPayload;
        Event = @event;
        Step = step;
    }

    internal string OriginalPayload { get; }
    internal JObject Event { get; }
    internal PendingManagedStep? Step { get; }
}

internal sealed class PendingExceptionConfiguration
{
    internal PendingExceptionConfiguration(IReadOnlyCollection<string> filters)
    {
        Filters = filters;
    }

    internal IReadOnlyCollection<string> Filters { get; }
    internal JObject? NativeResponse { get; set; }
}

internal sealed class ManagedStopProbe
{
    internal ManagedStopProbe(
        string reason,
        string? exceptionStage,
        string? exceptionType,
        ManagedRuntimeFrameSnapshot? frame)
    {
        Reason = reason;
        ExceptionStage = exceptionStage;
        ExceptionType = exceptionType;
        Frame = frame;
    }

    internal string Reason { get; }
    internal string? ExceptionStage { get; }
    internal string? ExceptionType { get; }
    internal ManagedRuntimeFrameSnapshot? Frame { get; }
}

internal sealed class ManagedExceptionStop
{
    internal ManagedExceptionStop(string stage, string type)
    {
        Stage = stage;
        Type = type;
    }

    internal string Stage { get; }
    internal string Type { get; }
}
