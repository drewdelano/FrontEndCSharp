using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

public static class DapBreakpointCompositionFactory
{
    private static readonly Regex BuildIdPattern = new Regex(
        "export\\s+const\\s+buildId\\s*=\\s*\"(?<id>[^\"]+)\"",
        RegexOptions.CultureInvariant);

    public static bool WaitForDebuggerBuildCompletion(
        IEnumerable<string> roots,
        TimeSpan timeout,
        TimeSpan? pollingInterval = null)
    {
        if (roots == null)
        {
            throw new ArgumentNullException(nameof(roots));
        }
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(25);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollingInterval));
        }

        var armPaths = DiscoverDebuggerArmPaths(roots);
        if (armPaths.Count == 0 || armPaths.All(path => !File.Exists(path)))
        {
            return true;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            Thread.Sleep(interval);
            if (armPaths.All(path => !File.Exists(path)))
            {
                return true;
            }
        }
        return armPaths.All(path => !File.Exists(path));
    }

    public static IReadOnlyList<string> DiscoverDebuggerArmPaths(IEnumerable<string> roots)
    {
        if (roots == null)
        {
            throw new ArgumentNullException(nameof(roots));
        }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            string? current;
            try
            {
                var fullPath = Path.GetFullPath(root);
                current = Directory.Exists(fullPath)
                    ? fullPath
                    : Path.GetDirectoryName(fullPath);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                continue;
            }

            while (!string.IsNullOrWhiteSpace(current))
            {
                var candidate = Path.Combine(
                    current!,
                    ".vs",
                    "FrontEndCSharp",
                    "debugger.arm");
                if (File.Exists(candidate))
                {
                    paths.Add(candidate);
                }
                var parent = Path.GetDirectoryName(current);
                if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                current = parent;
            }
        }
        return paths.ToList();
    }

    public static bool TryCreate(
        string launchJson,
        out DapBreakpointCompositionTransform? transform,
        out string reason,
        Action<string>? diagnosticSink = null,
        IEnumerable<string>? additionalRoots = null)
    {
        transform = null;
        JObject launch;
        try
        {
            launch = JObject.Parse(launchJson);
        }
        catch (JsonException)
        {
            reason = "launch-json-invalid";
            return false;
        }

        var properties = launch.DescendantsAndSelf().OfType<JProperty>().ToList();
        if (!IsBrowserChildLaunch(launch))
        {
            reason = "not-browser-child";
            return false;
        }

        var roots = properties
            .Where(property => property.Value.Type == JTokenType.String
                && (string.Equals(property.Name, "webRoot", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Name, "rootPath", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Name, "cwd", StringComparison.OrdinalIgnoreCase)))
            .Select(property => property.Value.Value<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Concat(additionalRoots ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var manifestPaths = DiscoverManifestPaths(roots);
        var sourceMapPaths = roots
            .SelectMany(SourceMapCandidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(ContainsDebuggerRendezvous)
            .ThenByDescending(path => File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue)
            .ToList();
        var candidates = new List<DapBreakpointCompositionCandidate>();
        foreach (var sourceMapPath in sourceMapPaths)
        {
            if (!TryReadBundledRuntime(
                sourceMapPath,
                out var runtimeSourcePath,
                out var runtimeSource,
                out var buildIdentitySource))
            {
                continue;
            }

            var match = BuildIdPattern.Match(buildIdentitySource);
            if (!match.Success)
            {
                continue;
            }

            var runtimeLine = FindExecuteInstructionLineInSource(runtimeSource);
            if (!runtimeLine.HasValue)
            {
                continue;
            }

            candidates.Add(new DapBreakpointCompositionCandidate(
                new DebugManifestBreakpointResolver(
                    match.Groups["id"].Value,
                    manifestPaths),
                runtimeSourcePath,
                runtimeLine.Value,
                match.Groups["id"].Value));
        }
        if (candidates.Count > 0)
        {
            transform = new DapBreakpointCompositionTransform(candidates, diagnosticSink);
            reason = "configured";
            return true;
        }

        foreach (var root in roots)
        {
            foreach (var runtimeSourcePath in RuntimeSourceCandidates(root!))
            {
                if (!File.Exists(runtimeSourcePath))
                {
                    continue;
                }
                var jsRoot = Directory.GetParent(Path.GetDirectoryName(runtimeSourcePath)!)!.FullName;
                var buildIdentityPath = Path.Combine(jsRoot, "debug-build.js");
                if (!File.Exists(buildIdentityPath))
                {
                    continue;
                }

                var match = BuildIdPattern.Match(File.ReadAllText(buildIdentityPath));
                if (!match.Success)
                {
                    continue;
                }

                var runtimeLine = FindExecuteInstructionLine(runtimeSourcePath);
                if (!runtimeLine.HasValue)
                {
                    continue;
                }

                candidates.Add(new DapBreakpointCompositionCandidate(
                    new DebugManifestBreakpointResolver(
                        match.Groups["id"].Value,
                        manifestPaths),
                    runtimeSourcePath,
                    runtimeLine.Value,
                    match.Groups["id"].Value));
            }
        }

        if (candidates.Count > 0)
        {
            transform = new DapBreakpointCompositionTransform(candidates, diagnosticSink);
            reason = "configured";
            return true;
        }

        reason = "runtime-source-not-found";
        return false;
    }

    private static IReadOnlyList<string> DiscoverManifestPaths(IEnumerable<string> roots)
    {
        var searchDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            string? fullRoot;
            try
            {
                fullRoot = Path.GetFullPath(root);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                continue;
            }

            var current = Directory.Exists(fullRoot)
                ? fullRoot
                : Path.GetDirectoryName(fullRoot);
            for (var depth = 0; depth < 4 && !string.IsNullOrWhiteSpace(current); depth++)
            {
                searchDirectories.Add(current!);
                if (IsWorkspaceRoot(current!))
                {
                    break;
                }
                current = Path.GetDirectoryName(current);
            }
        }

        var manifests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in searchDirectories)
        {
            IEnumerable<string> projectDirectories;
            try
            {
                projectDirectories = new[] { directory }
                    .Concat(Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                    .ToList();
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is DirectoryNotFoundException)
            {
                continue;
            }
            foreach (var projectDirectory in projectDirectories)
            {
                var manifestRoots = new[]
                {
                    Path.Combine(projectDirectory, "bin"),
                    Path.Combine(projectDirectory, "obj", "FrontEndCSharp")
                };
                foreach (var manifestRoot in manifestRoots.Where(Directory.Exists))
                {
                    try
                    {
                        foreach (var manifest in Directory.EnumerateFiles(
                            manifestRoot,
                            "*.frontendcsharp.debug.json",
                            SearchOption.AllDirectories))
                        {
                            manifests.Add(manifest);
                        }
                    }
                    catch (Exception exception) when (
                        exception is IOException
                        || exception is UnauthorizedAccessException
                        || exception is DirectoryNotFoundException)
                    {
                        continue;
                    }
                }
            }
        }
        return manifests.OrderByDescending(File.GetLastWriteTimeUtc).ToList();
    }

    private static bool IsWorkspaceRoot(string directory)
    {
        try
        {
            if (Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).Any())
            {
                return true;
            }
            return Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
                .Any(child => Directory.EnumerateFiles(
                    child,
                    "*.csproj",
                    SearchOption.TopDirectoryOnly).Any());
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static IReadOnlyList<string> DiscoverCandidateRoots(string launchJson)
    {
        JObject launch;
        try
        {
            launch = JObject.Parse(launchJson);
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }

        return launch.DescendantsAndSelf()
            .OfType<JProperty>()
            .Where(property => property.Value.Type == JTokenType.String
                && (string.Equals(property.Name, "webRoot", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Name, "rootPath", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Name, "cwd", StringComparison.OrdinalIgnoreCase)))
            .Select(property => property.Value.Value<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsBrowserChildLaunch(string launchJson)
    {
        try
        {
            return IsBrowserChildLaunch(JObject.Parse(launchJson));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsBrowserChildLaunch(JObject launch) => launch
        .DescendantsAndSelf()
        .OfType<JProperty>()
        .Any(property =>
            string.Equals(property.Name, "__pendingTargetId", StringComparison.Ordinal)
            && property.Value.Type == JTokenType.String
            && !string.IsNullOrWhiteSpace(property.Value.Value<string>()));

    private static int? FindExecuteInstructionLine(string runtimeSourcePath) =>
        FindExecuteInstructionLineInSource(File.ReadAllText(runtimeSourcePath));

    private static int? FindExecuteInstructionLineInSource(string runtimeSource)
    {
        var lines = runtimeSource.Replace("\r\n", "\n").Split('\n');
        var declaration = Array.FindIndex(lines, line =>
            line.IndexOf("function executeInstruction(opts)", StringComparison.Ordinal) >= 0);
        if (declaration < 0)
        {
            return null;
        }
        for (var index = declaration + 1; index < lines.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                return index + 1;
            }
        }
        return null;
    }

    private static bool TryReadBundledRuntime(
        string sourceMapPath,
        out string runtimeSourcePath,
        out string runtimeSource,
        out string buildIdentitySource)
    {
        runtimeSourcePath = string.Empty;
        runtimeSource = string.Empty;
        buildIdentitySource = string.Empty;
        if (!File.Exists(sourceMapPath))
        {
            return false;
        }

        try
        {
            var sourceMap = JObject.Parse(File.ReadAllText(sourceMapPath));
            if (sourceMap["sources"] is not JArray sources
                || sourceMap["sourcesContent"] is not JArray sourcesContent
                || sources.Count != sourcesContent.Count)
            {
                return false;
            }

            for (var index = 0; index < sources.Count; index++)
            {
                var sourceName = sources[index]?.Value<string>();
                var sourceContent = sourcesContent[index]?.Value<string>();
                if (string.IsNullOrWhiteSpace(sourceName) || sourceContent == null)
                {
                    continue;
                }

                var normalizedSourceName = sourceName!.Replace('\\', '/');
                if (normalizedSourceName.EndsWith(
                    "/jit-engine/_executeInstruction.js",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryResolveSourceMapPath(sourceName, out runtimeSourcePath))
                    {
                        return false;
                    }
                    runtimeSource = sourceContent;
                }
                else if (normalizedSourceName.EndsWith(
                    "/debug-build.js",
                    StringComparison.OrdinalIgnoreCase))
                {
                    buildIdentitySource = sourceContent;
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is JsonException)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(runtimeSourcePath)
            && !string.IsNullOrWhiteSpace(runtimeSource)
            && !string.IsNullOrWhiteSpace(buildIdentitySource);
    }

    private static bool TryResolveSourceMapPath(string sourceName, out string path)
    {
        path = string.Empty;
        if (Uri.TryCreate(sourceName, UriKind.Absolute, out var sourceUri) && sourceUri.IsFile)
        {
            path = sourceUri.LocalPath;
            return true;
        }

        try
        {
            path = Path.GetFullPath(sourceName);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is NotSupportedException
            || exception is PathTooLongException)
        {
            return false;
        }
    }

    private static IEnumerable<string> SourceMapCandidates(string root)
    {
        foreach (var indexPath in DiscoverIndexBundles(root, SearchOption.TopDirectoryOnly)
            .Concat(DiscoverIndexBundles(
                Path.Combine(root, "js"),
                SearchOption.AllDirectories))
            .Concat(DiscoverIndexBundles(
                Path.Combine(root, "wwwroot", "js"),
                SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var sourceMapPath = indexPath + ".map";
            if (File.Exists(sourceMapPath))
            {
                yield return sourceMapPath;
            }
        }
    }

    private static IReadOnlyList<string> DiscoverIndexBundles(
        string directory,
        SearchOption searchOption)
    {
        try
        {
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "index.js", searchOption).ToList()
                : Array.Empty<string>();
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool ContainsDebuggerRendezvous(string sourceMapPath)
    {
        try
        {
            return File.Exists(sourceMapPath)
                && File.ReadAllText(sourceMapPath).IndexOf(
                    "FrontEndCSharp debugger did not attach within ",
                    StringComparison.Ordinal) >= 0;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IEnumerable<string> RuntimeSourceCandidates(string root)
    {
        yield return Path.Combine(root, "jit-engine", "_executeInstruction.js");
        yield return Path.Combine(root, "js", "jit-engine", "_executeInstruction.js");
        yield return Path.Combine(root, "wwwroot", "js", "jit-engine", "_executeInstruction.js");
    }
}
