using FrontEndCSharp.Fecsx.Editor;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxTailwindProjectAvailabilityTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "fecsx-tailwind-availability");
    private static readonly string BrowserProject = Path.Combine(Root, "Browser", "Browser.csproj");
    private static readonly string HostProject = Path.Combine(Root, "Host", "Host.csproj");

    [Test]
    public void EnablesCompletionWhenTheOwningProjectHasTailwindBuildIntegration()
    {
        var projects = new[]
        {
            new FecsxTailwindProjectConfiguration(BrowserProject, true, true, null)
        };

        Assert.True(FecsxTailwindProjectAvailability.IsAvailable(BrowserProject, projects));
    }

    [Test]
    public void EnablesCompletionWhenAnEnabledTailwindHostTargetsTheOwningProject()
    {
        var projects = new[]
        {
            new FecsxTailwindProjectConfiguration(
                HostProject,
                true,
                true,
                @"..\Browser\Browser.csproj")
        };

        Assert.True(FecsxTailwindProjectAvailability.IsAvailable(BrowserProject, projects));
    }

    [Test]
    public void ResolvesTheHostCatalogForAReferencedBrowserProject()
    {
        var projects = new[]
        {
            new FecsxTailwindProjectConfiguration(
                HostProject,
                true,
                true,
                @"..\Browser\Browser.csproj",
                @"obj\Debug\net8.0\fecscn\tailwind\editor-catalog.tsv.gz")
        };

        Assert.Equal(
            Path.Combine(
                Path.GetDirectoryName(HostProject)!,
                "obj",
                "Debug",
                "net8.0",
                "fecscn",
                "tailwind",
                "editor-catalog.tsv.gz"),
            FecsxTailwindProjectAvailability.GetEditorCatalogPath(BrowserProject, projects));
    }

    [Test]
    public void DisablesCompletionWhenTailwindBuildIntegrationIsDisabled()
    {
        var projects = new[]
        {
            new FecsxTailwindProjectConfiguration(HostProject, true, false, BrowserProject)
        };

        Assert.False(FecsxTailwindProjectAvailability.IsAvailable(BrowserProject, projects));
    }

    [Test]
    public void DisablesCompletionForMissingOrUnrelatedTailwindBuildIntegration()
    {
        var projects = new[]
        {
            new FecsxTailwindProjectConfiguration(
                HostProject,
                true,
                true,
                Path.Combine(Root, "Other", "Other.csproj")),
            new FecsxTailwindProjectConfiguration(BrowserProject, false, true, null)
        };

        Assert.False(FecsxTailwindProjectAvailability.IsAvailable(BrowserProject, projects));
        Assert.False(FecsxTailwindProjectAvailability.IsAvailable(
            BrowserProject,
            Array.Empty<FecsxTailwindProjectConfiguration>()));
    }
}
