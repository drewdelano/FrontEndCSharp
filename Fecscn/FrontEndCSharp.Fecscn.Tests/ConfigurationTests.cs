using FrontEndCSharp.Fecscn.Configuration;
using FrontEndCSharp.Fecscn.Registry;

namespace FrontEndCSharp.Fecscn.Tests;

public sealed class ConfigurationTests
{
    [Test]
    public void DefaultConfigurationRoundTrips()
    {
        var configuration = FecscnProjectConfig.CreateDefault(
            "https://registry.fecscn.dev/r");

        var json = FecscnJson.SerializeProjectConfig(configuration);
        var parsed = FecscnJson.ParseProjectConfig(json);

        Assert.Equal(configuration, parsed);
        Assert.Equal("Components/UI", parsed.Paths.Components);
        Assert.Equal("Styles/fecscn.css", parsed.Paths.CssInput);
        Assert.Equal("wwwroot/css/fecscn.css", parsed.Paths.CssOutput);
    }

    [Test]
    public void ConfigurationRejectsUnsupportedProfileAndRelativeRegistry()
    {
        var unsupported = FecscnProjectConfig.CreateDefault(
            "https://registry.fecscn.dev/r") with
        {
            Profile = FecscnProfile.Default with { IconLibrary = "tabler" }
        };
        var relative = FecscnProjectConfig.CreateDefault("registry");

        Assert.Contains(
            "profile",
            Assert.Throws<FecscnConfigurationException>(() => unsupported.Validate()).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "absolute HTTPS",
            Assert.Throws<FecscnConfigurationException>(() => relative.Validate()).Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
