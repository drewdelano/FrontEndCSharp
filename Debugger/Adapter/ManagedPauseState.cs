namespace FrontEndCSharp.DebugAdapter;

internal sealed class ManagedPauseState
{
    private int nextFrameId;
    private int nextVariablesReference;

    internal List<RuntimeFrame> Frames { get; private set; } = [];
    internal int ManagedDepth { get; private set; }
    internal string? ExceptionStage { get; private set; }
    internal string? ExceptionType { get; private set; }
    internal Dictionary<int, IReadOnlyList<ProjectedManagedVariable>> Variables { get; } = [];
    internal Dictionary<int, RuntimeFrame> VariableFrames { get; } = [];

    internal ManagedPauseSnapshot Project(
        IReadOnlyList<CdpManagedFrame> cdpFrames,
        DebugManifestIndex manifest)
    {
        Variables.Clear();
        VariableFrames.Clear();
        var runtimeFrames = new List<RuntimeFrame>();
        foreach (var frame in cdpFrames)
        {
            var location = manifest.ResolveFrame(
                frame.ModuleId,
                frame.MethodToken,
                frame.BlockIndex,
                frame.InstructionIndex,
                frame.Arguments,
                frame.Locals,
                frame.Fields,
                frame.Objects);
            var previous = runtimeFrames.LastOrDefault();
            if (previous?.Location.MethodToken == location.MethodToken
                && string.Equals(
                    previous.Location.ModuleId,
                    location.ModuleId,
                    StringComparison.OrdinalIgnoreCase)
                && (frame.IsLogical
                    || frame.MethodToken != location.MethodToken
                    || previous.RawMethodToken != previous.Location.MethodToken))
            {
                continue;
            }
            var projection = ManagedVariableGraph.Project(
                location.Arguments.Select(value => new ManagedValueSnapshot(
                    value.Type,
                    value.Value,
                    value.ObjectId)).ToList(),
                location.Locals.Select(value => new ManagedValueSnapshot(
                    value.Type,
                    value.Value,
                    value.ObjectId)).ToList(),
                frame.Objects,
                () => Interlocked.Increment(ref nextVariablesReference),
                location.Arguments.Select(value => value.Name).ToList(),
                location.Locals.Select(value => value.Name).ToList(),
                location.Arguments.Select(value => value.Target).ToList(),
                location.Locals.Select(value => value.Target).ToList());
            var runtimeFrame = new RuntimeFrame(
                Interlocked.Increment(ref nextFrameId),
                location,
                projection.ScopeReference,
                frame.IsLogical,
                frame.MethodToken,
                frame.CallFrameId);
            foreach (var item in projection.Variables)
            {
                Variables[item.Key] = item.Value;
                VariableFrames[item.Key] = runtimeFrame;
            }
            runtimeFrames.Add(runtimeFrame);
        }

        Frames = runtimeFrames;
        var activeFrame = cdpFrames.FirstOrDefault();
        ManagedDepth = activeFrame != null
            ? activeFrame.DebugDepth + cdpFrames.Count(frame => frame.IsLogical)
            : runtimeFrames.Count;
        ExceptionStage = activeFrame?.StopReason == "exception"
            ? activeFrame.ExceptionStage
            : null;
        ExceptionType = activeFrame?.StopReason == "exception"
            ? activeFrame.ExceptionType
            : null;
        return new ManagedPauseSnapshot(
            activeFrame?.StopReason,
            activeFrame?.BreakpointIds ?? [],
            ExceptionStage,
            ExceptionType);
    }

    internal void Reset()
    {
        Frames = [];
        ManagedDepth = 0;
        ExceptionStage = null;
        ExceptionType = null;
        Variables.Clear();
        VariableFrames.Clear();
    }
}

internal sealed class ManagedPauseSnapshot
{
    internal ManagedPauseSnapshot(
        string? stopReason,
        List<int> hitBreakpointIds,
        string? exceptionStage,
        string? exceptionType)
    {
        StopReason = stopReason;
        HitBreakpointIds = hitBreakpointIds;
        ExceptionStage = exceptionStage;
        ExceptionType = exceptionType;
    }

    internal string? StopReason { get; }
    internal List<int> HitBreakpointIds { get; }
    internal string? ExceptionStage { get; }
    internal string? ExceptionType { get; }
}

internal sealed class RuntimeFrame
{
    internal RuntimeFrame(
        int id,
        ResolvedFrame location,
        int localsReference,
        bool isLogical,
        int rawMethodToken,
        string? callFrameId)
    {
        Id = id;
        Location = location;
        LocalsReference = localsReference;
        IsLogical = isLogical;
        RawMethodToken = rawMethodToken;
        CallFrameId = callFrameId;
    }

    internal int Id { get; }
    internal ResolvedFrame Location { get; }
    internal int LocalsReference { get; }
    internal bool IsLogical { get; }
    internal int RawMethodToken { get; }
    internal string? CallFrameId { get; }
}
