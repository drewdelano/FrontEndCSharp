namespace FrontEndCSharp.Fecscn.Configuration;

using FrontEndCSharp.Fecscn.Registry;

public sealed record FecscnProjectPaths(
    string Components,
    string CssInput,
    string CssOutput);

public sealed record FecscnProjectConfig(
    int Version,
    string Registry,
    FecscnProfile Profile,
    FecscnProjectPaths Paths)
{
    public const int SupportedVersion = 1;

    public static FecscnProjectConfig CreateDefault(string registry) =>
        new(
            SupportedVersion,
            registry,
            FecscnProfile.Default,
            new("Components/UI", "Styles/fecscn.css", "wwwroot/css/fecscn.css"));

    public void Validate()
    {
        if (Version != SupportedVersion)
        {
            throw new FecscnConfigurationException(
                $"fecscn project-config version '{Version}' is unsupported; "
                + $"expected '{SupportedVersion}'.");
        }
        if (Profile != FecscnProfile.Default)
        {
            throw new FecscnConfigurationException(
                "The fecscn project profile is unsupported. The initial release requires "
                + "Base UI, Nova, Lucide, Tailwind CSS v4, CSS variables, and non-RSC output.");
        }
        if (!Uri.TryCreate(Registry, UriKind.Absolute, out var registryUri)
            || (registryUri.Scheme != Uri.UriSchemeHttps
                && !(registryUri.Scheme == Uri.UriSchemeHttp && registryUri.IsLoopback)))
        {
            throw new FecscnConfigurationException(
                "The fecscn registry must be an absolute HTTPS URL; HTTP is allowed only for localhost development.");
        }

        ValidateRelativePath(Paths.Components, nameof(Paths.Components));
        ValidateRelativePath(Paths.CssInput, nameof(Paths.CssInput));
        ValidateRelativePath(Paths.CssOutput, nameof(Paths.CssOutput));
        if (!Paths.CssInput.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || !Paths.CssOutput.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            throw new FecscnConfigurationException(
                "The fecscn CSS input and output paths must use the .css extension.");
        }
    }

    private static void ValidateRelativePath(string path, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Contains('\\')
            || path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new FecscnConfigurationException(
                $"fecscn project-config path '{propertyName}' must be a canonical relative path.");
        }
    }
}

public sealed class FecscnConfigurationException : Exception
{
    public FecscnConfigurationException(string message)
        : base(message)
    {
    }
}
