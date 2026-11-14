using System;
using System.Collections.Generic;

namespace FrontEndCSharp.Debugging;

public interface IManagedBreakpointResolver
{
    bool OwnsSource(string sourcePath);

    ManagedBreakpointResolution Resolve(string sourcePath, int requestedLine);

    ManagedFrameInspection ResolveFrame(string sourcePath, ManagedRuntimeFrameSnapshot frame);

    ManagedFrameInspection ResolveFrame(ManagedRuntimeFrameSnapshot frame);

    ManagedFrameLayout ResolveFrameLayout(
        string sourcePath,
        ManagedBreakpointResolution resolution);

    IReadOnlyList<ManagedRuntimeLocation> ResolveStepLocations(string sourcePath);
}

public sealed class DapBreakpointCompositionCandidate
{
    public DapBreakpointCompositionCandidate(
        IManagedBreakpointResolver resolver,
        string runtimeSourcePath,
        int runtimeLine,
        string buildId)
    {
        Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        RuntimeSourcePath = string.IsNullOrWhiteSpace(runtimeSourcePath)
            ? throw new ArgumentException("The runtime source path is required.", nameof(runtimeSourcePath))
            : runtimeSourcePath;
        RuntimeLine = runtimeLine > 0
            ? runtimeLine
            : throw new ArgumentOutOfRangeException(nameof(runtimeLine));
        BuildId = string.IsNullOrWhiteSpace(buildId)
            ? throw new ArgumentException("The build identity is required.", nameof(buildId))
            : buildId;
    }

    public IManagedBreakpointResolver Resolver { get; }
    public string RuntimeSourcePath { get; }
    public int RuntimeLine { get; }
    public string BuildId { get; }
}

public sealed class DapTransformDispatch
{
    public DapTransformDispatch(
        IReadOnlyList<string> clientPayloads,
        IReadOnlyList<string> adapterPayloads)
    {
        ClientPayloads = clientPayloads;
        AdapterPayloads = adapterPayloads;
    }

    public IReadOnlyList<string> ClientPayloads { get; }
    public IReadOnlyList<string> AdapterPayloads { get; }
}
