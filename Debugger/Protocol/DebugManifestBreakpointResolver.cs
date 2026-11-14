using System;
using System.Collections.Generic;
using System.IO;

namespace FrontEndCSharp.Debugging;

public sealed class DebugManifestBreakpointResolver : IManagedBreakpointResolver
{
    private readonly DebugManifestRepository repository;

    public DebugManifestBreakpointResolver(
        string expectedBuildId,
        IEnumerable<string>? manifestPaths = null)
    {
        if (string.IsNullOrWhiteSpace(expectedBuildId))
        {
            throw new ArgumentException(
                "The expected build identity is required.",
                nameof(expectedBuildId));
        }

        repository = new DebugManifestRepository(expectedBuildId, manifestPaths);
    }

    public bool OwnsSource(string sourcePath) => repository.FindBySource(sourcePath) != null;

    public ManagedBreakpointResolution Resolve(string sourcePath, int requestedLine)
    {
        var index = repository.FindBySource(sourcePath);
        if (index == null)
        {
            return ManagedBreakpointResolution.Unresolved(
                "The source file is not present in the active FrontEndCSharp build.",
                requestedLine);
        }

        return index.Resolve(sourcePath, requestedLine);
    }

    public ManagedFrameInspection ResolveFrame(
        string sourcePath,
        ManagedRuntimeFrameSnapshot frame) =>
        GetRequiredIndex(sourcePath).ResolveFrame(frame);

    public ManagedFrameInspection ResolveFrame(ManagedRuntimeFrameSnapshot frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        return repository.FindByFrame(frame).ResolveFrame(frame);
    }

    public ManagedFrameLayout ResolveFrameLayout(
        string sourcePath,
        ManagedBreakpointResolution resolution) =>
        GetRequiredIndex(sourcePath).ResolveFrameLayout(resolution);

    public IReadOnlyList<ManagedRuntimeLocation> ResolveStepLocations(string sourcePath) =>
        GetRequiredIndex(sourcePath).ResolveStepLocations();

    private DebugManifestIndex GetRequiredIndex(string sourcePath) =>
        repository.FindBySource(sourcePath)
        ?? throw new InvalidDataException(
            "The source file is not present in the active FrontEndCSharp build.");
}
