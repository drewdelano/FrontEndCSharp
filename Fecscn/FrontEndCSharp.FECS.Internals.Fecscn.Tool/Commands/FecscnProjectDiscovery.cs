using System.Xml.Linq;

namespace FrontEndCSharp.Fecscn.Commands;

public sealed record FecscnProject(string HostProjectPath, string BrowserProjectPath)
{
    public string HostDirectory => Path.GetDirectoryName(HostProjectPath)!;
    public string BrowserDirectory => Path.GetDirectoryName(BrowserProjectPath)!;
}

public static class FecscnProjectDiscovery
{
    public static FecscnProject Discover(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var target = Path.GetFullPath(path);
        if (!File.Exists(target) && !Directory.Exists(target))
        {
            throw new FecscnCommandException($"The project path does not exist: '{target}'.");
        }

        if (File.Exists(target))
        {
            if (!target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                throw new FecscnCommandException(
                    $"The explicit project must be a .csproj file: '{target}'.");
            }
            var document = LoadProject(target);
            if (IsHost(document))
            {
                return PairFromHost(target, document);
            }
            if (IsBrowser(document))
            {
                var searchRoot = Directory.GetParent(Path.GetDirectoryName(target)!)?.FullName
                    ?? Path.GetDirectoryName(target)!;
                var matches = DiscoverPairs(searchRoot)
                    .Where(pair => PathsEqual(pair.BrowserProjectPath, target))
                    .ToArray();
                return ExactlyOne(matches, target);
            }
            throw Unsupported(target);
        }

        var directProjects = Directory.EnumerateFiles(target, "*.csproj", SearchOption.TopDirectoryOnly).ToArray();
        var searchDirectory = directProjects.Any(project => IsBrowser(LoadProject(project)))
            ? Directory.GetParent(target)?.FullName ?? target
            : target;
        var pairs = DiscoverPairs(searchDirectory)
            .Where(pair => directProjects.Length == 0
                           || directProjects.Any(project => PathsEqual(project, pair.HostProjectPath)
                                                            || PathsEqual(project, pair.BrowserProjectPath)))
            .ToArray();
        return ExactlyOne(pairs, target);
    }

    private static IEnumerable<FecscnProject> DiscoverPairs(string directory)
    {
        foreach (var projectPath in Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
                     .Where(path => !HasBuildDirectory(path))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var document = LoadProject(projectPath);
            if (!IsHost(document))
            {
                continue;
            }
            FecscnProject pair;
            try
            {
                pair = PairFromHost(projectPath, document);
            }
            catch (FecscnCommandException)
            {
                continue;
            }
            yield return pair;
        }
    }

    private static FecscnProject PairFromHost(string hostPath, XDocument host)
    {
        var value = host.Descendants("FrontEndCSharpProject").Select(element => element.Value.Trim()).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Unsupported(hostPath);
        }
        var hostDirectory = Path.GetDirectoryName(hostPath)!;
        value = value.Replace("$(MSBuildProjectDirectory)", hostDirectory, StringComparison.OrdinalIgnoreCase);
        if (value.Contains("$(", StringComparison.Ordinal))
        {
            throw new FecscnCommandException(
                $"The FrontEndCSharpProject value in '{hostPath}' contains unsupported MSBuild expressions.");
        }
        var browserPath = Path.GetFullPath(value, hostDirectory);
        if (!File.Exists(browserPath) || !IsBrowser(LoadProject(browserPath)))
        {
            throw new FecscnCommandException(
                $"The host project '{hostPath}' does not reference a supported FECSX React browser project.");
        }
        return new(Path.GetFullPath(hostPath), browserPath);
    }

    private static FecscnProject ExactlyOne(IReadOnlyCollection<FecscnProject> pairs, string target) =>
        pairs.Count switch
        {
            1 => pairs.Single(),
            0 => throw new FecscnCommandException(
                $"No FrontEndCSharp host and FECSX React browser project pair was found beneath '{target}'."),
            _ => throw new FecscnCommandException(
                $"Multiple FrontEndCSharp project pairs were found beneath '{target}'. Pass --project with one host or browser .csproj path.")
        };

    private static bool IsHost(XDocument document) =>
        document.Descendants("FrontEndCSharpProject").Any();

    private static bool IsBrowser(XDocument document)
    {
        var packages = document.Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(value => value is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return packages.Contains("FrontEndCSharp.FECS.Internals.Fecsx.Build")
               && packages.Contains("FrontEndCSharp.Overrides.Interop.JS.React");
    }

    private static XDocument LoadProject(string path)
    {
        try
        {
            return XDocument.Load(path, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
        {
            throw new FecscnCommandException($"Could not read project '{path}': {exception.Message}");
        }
    }

    private static bool HasBuildDirectory(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                            || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static FecscnCommandException Unsupported(string path) => new(
        $"Project '{path}' is not a supported FrontEndCSharp host or FECSX React browser project.");
}
