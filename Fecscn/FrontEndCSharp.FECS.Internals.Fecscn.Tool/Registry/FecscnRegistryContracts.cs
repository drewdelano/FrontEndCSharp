namespace FrontEndCSharp.Fecscn.Registry;

public sealed record FecscnProfile(
    string Base,
    string Style,
    string IconLibrary,
    int TailwindMajorVersion,
    bool CssVariables,
    bool ReactServerComponents)
{
    public static FecscnProfile Default { get; } = new(
        "base",
        "nova",
        "lucide",
        4,
        CssVariables: true,
        ReactServerComponents: false);
}

public sealed record NpmDependency(string Name, string Version);

public sealed record NuGetDependency(string Name, string Version);

public sealed record FecscnUpstreamSource(
    string Address,
    string Sha256,
    string Collection);

public sealed record FecscnRegistryFile(
    string Source,
    string Target,
    string Kind,
    string Sha256);

public sealed record FecscnRegistryItem(
    string Name,
    string Type,
    FecscnUpstreamSource Upstream,
    IReadOnlyList<NpmDependency> Dependencies,
    IReadOnlyList<string> RegistryDependencies,
    IReadOnlyList<FecscnRegistryFile> Files,
    IReadOnlyList<NuGetDependency>? Packages = null);

public sealed record PublishedRegistryFile(
    string Path,
    string Target,
    string Type,
    string Sha256,
    string? Content,
    string? ContentBase64);

public sealed record PublishedRegistryItem(
    int Version,
    string Name,
    string Type,
    FecscnUpstreamSource Upstream,
    IReadOnlyList<NpmDependency> Dependencies,
    IReadOnlyList<string> RegistryDependencies,
    IReadOnlyList<PublishedRegistryFile> Files,
    IReadOnlyList<NuGetDependency>? Packages = null);

public sealed record FecscnRegistryDocument(
    int Version,
    FecscnProfile Profile,
    IReadOnlyList<FecscnRegistryItem> Items);

public sealed record FecscnRegistryBuildResult(
    string IndexJson,
    IReadOnlyDictionary<string, string> Items);

public sealed class FecscnRegistryException : Exception
{
    public FecscnRegistryException(string message)
        : base(message)
    {
    }
}
