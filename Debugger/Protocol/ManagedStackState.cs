using System.Collections.Generic;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedStackState
{
    internal Dictionary<int, PendingStackTrace> PendingStackTraces { get; } =
        new Dictionary<int, PendingStackTrace>();
    internal Dictionary<int, BreakpointBinding> ProjectedFrames { get; } =
        new Dictionary<int, BreakpointBinding>();
    internal Dictionary<int, PendingNestedFrame> PendingNestedFrames { get; } =
        new Dictionary<int, PendingNestedFrame>();
    internal Dictionary<int, PendingAsyncParentCount> PendingAsyncParentCounts { get; } =
        new Dictionary<int, PendingAsyncParentCount>();
    internal Dictionary<int, PendingLogicalFrame> PendingLogicalFrames { get; } =
        new Dictionary<int, PendingLogicalFrame>();
    internal Dictionary<int, LogicalFrameProjection> LogicalFrames { get; } =
        new Dictionary<int, LogicalFrameProjection>();

    internal BreakpointBinding? ActiveBinding { get; set; }
    internal ManagedFrameProjection? ActiveProjection { get; set; }
    internal int? ActiveFrameId { get; set; }
    internal bool ActiveStopNeedsIdentity { get; set; }
    internal int NextProxyFrameId { get; set; } = 2000000000;

    internal void ResetActiveStop()
    {
        ActiveBinding = null;
        ActiveProjection = null;
        ActiveFrameId = null;
        ActiveStopNeedsIdentity = false;
    }

    internal void ResetForContinuation()
    {
        ResetActiveStop();
        PendingStackTraces.Clear();
        ProjectedFrames.Clear();
        PendingNestedFrames.Clear();
        PendingAsyncParentCounts.Clear();
        PendingLogicalFrames.Clear();
        LogicalFrames.Clear();
    }
}
