using System;
using System.Collections.Generic;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedVariableState
{
    internal Dictionary<int, bool> PendingInspections { get; } =
        new Dictionary<int, bool>();
    internal Dictionary<int, bool> PendingHovers { get; } =
        new Dictionary<int, bool>();
    internal HashSet<int> LiveReferences { get; } = new HashSet<int>();
    internal HashSet<int> ArrayReferences { get; } = new HashSet<int>();
    internal HashSet<int> ImmutableReferences { get; } = new HashSet<int>();
    internal HashSet<int> StaleReferences { get; } = new HashSet<int>();
    internal Dictionary<ManagedVariableIdentity, string> RuntimeNames { get; } =
        new Dictionary<ManagedVariableIdentity, string>();
    internal Dictionary<int, string> PendingSetErrors { get; } =
        new Dictionary<int, string>();
    internal Dictionary<int, int> PendingVariableRequests { get; } =
        new Dictionary<int, int>();
    internal Dictionary<int, PendingManagedScalarValue> PendingScalarValues { get; } =
        new Dictionary<int, PendingManagedScalarValue>();

    internal void ResetForSuspension()
    {
        StaleReferences.UnionWith(LiveReferences);
        if (StaleReferences.Count > 4096)
        {
            StaleReferences.Clear();
        }
        LiveReferences.Clear();
        ArrayReferences.Clear();
        ImmutableReferences.Clear();
        RuntimeNames.Clear();
        PendingInspections.Clear();
        PendingHovers.Clear();
        PendingVariableRequests.Clear();
    }
}
