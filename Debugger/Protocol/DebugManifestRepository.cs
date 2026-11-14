using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace FrontEndCSharp.Debugging;

internal sealed class DebugManifestRepository
{
    private readonly string expectedBuildId;
    private readonly IReadOnlyList<DebugManifestIndex> preloadedIndexes;
    private readonly Dictionary<string, DebugManifestIndex?> sourceIndexes =
        new Dictionary<string, DebugManifestIndex?>(StringComparer.OrdinalIgnoreCase);

    internal DebugManifestRepository(
        string expectedBuildId,
        IEnumerable<string>? manifestPaths)
    {
        this.expectedBuildId = expectedBuildId;
        preloadedIndexes = (manifestPaths ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(TryLoadIndex)
            .Where(index => index != null
                && string.Equals(index.BuildId, expectedBuildId, StringComparison.Ordinal))
            .Cast<DebugManifestIndex>()
            .ToList();
    }

    internal DebugManifestIndex? FindBySource(string sourcePath)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(sourcePath);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is NotSupportedException
            || exception is PathTooLongException)
        {
            return null;
        }

        lock (sourceIndexes)
        {
            if (sourceIndexes.TryGetValue(fullPath, out var cached))
            {
                return cached;
            }
        }

        var preloaded = preloadedIndexes.FirstOrDefault(index => index.OwnsSource(fullPath));
        if (preloaded != null)
        {
            lock (sourceIndexes)
            {
                sourceIndexes[fullPath] = preloaded;
            }
            return preloaded;
        }

        var discovered = DiscoverIndex(fullPath);
        lock (sourceIndexes)
        {
            sourceIndexes[fullPath] = discovered;
        }
        return discovered;
    }

    internal DebugManifestIndex FindByFrame(ManagedRuntimeFrameSnapshot frame)
    {
        var matches = preloadedIndexes
            .Where(index => index.OwnsFrame(frame))
            .ToList();
        if (matches.Count != 1)
        {
            throw new InvalidDataException(matches.Count == 0
                ? "The managed frame is not present in an active FrontEndCSharp debug manifest."
                : "The managed frame is present in more than one active FrontEndCSharp debug manifest.");
        }
        return matches[0];
    }

    private static DebugManifestIndex? TryLoadIndex(string manifestPath)
    {
        try
        {
            return LoadIndex(manifestPath);
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is JsonException
            || exception is InvalidDataException
            || exception is ArgumentException
            || exception is NotSupportedException)
        {
            return null;
        }
    }

    private DebugManifestIndex? DiscoverIndex(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath);
        while (!string.IsNullOrEmpty(directory))
        {
            if (ContainsProjectFile(directory))
            {
                var manifestRoots = new[]
                {
                    Path.Combine(directory, "bin"),
                    Path.Combine(directory, "obj", "FrontEndCSharp")
                };

                foreach (var manifestPath in manifestRoots
                    .SelectMany(EnumerateManifestFiles)
                    .OrderByDescending(File.GetLastWriteTimeUtc))
                {
                    DebugManifestIndex index;
                    try
                    {
                        index = LoadIndex(manifestPath);
                    }
                    catch (Exception exception) when (
                        exception is IOException
                        || exception is UnauthorizedAccessException
                        || exception is JsonException
                        || exception is InvalidDataException)
                    {
                        continue;
                    }
                    if (string.Equals(index.BuildId, expectedBuildId, StringComparison.Ordinal)
                        && index.OwnsSource(sourcePath))
                    {
                        return index;
                    }
                }
                return null;
            }

            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }

    private static DebugManifestIndex LoadIndex(string manifestPath) =>
        DebugManifestIndex.Load(manifestPath);

    private static bool ContainsProjectFile(string directory)
    {
        try
        {
            return Directory.Exists(directory)
                && Directory.EnumerateFiles(
                    directory,
                    "*.csproj",
                    SearchOption.TopDirectoryOnly).Any();
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is NotSupportedException)
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateManifestFiles(string directory)
    {
        try
        {
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(
                    directory,
                    "*.frontendcsharp.debug.json",
                    SearchOption.AllDirectories).ToArray()
                : Array.Empty<string>();
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is NotSupportedException)
        {
            return Array.Empty<string>();
        }
    }
}
