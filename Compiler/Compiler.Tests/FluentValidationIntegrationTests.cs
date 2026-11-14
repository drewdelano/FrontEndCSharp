namespace Compiler.Tests;

public sealed class FluentValidationIntegrationTests : IntegrationTestBase
{
    [Test]
    public void UnbundledRuntimeAssetsRemainInsideOwnerQualifiedDirectories()
    {
        var outputDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "FluentValidationBrowserOutput");
        var runtimeAssetsDirectory = Path.Combine(outputDirectory, "runtime-assets");

        Assert.True(Directory.Exists(runtimeAssetsDirectory));
        Assert.Empty(Directory.EnumerateFiles(
            runtimeAssetsDirectory,
            "*",
            SearchOption.TopDirectoryOnly));
        var owners = Directory.EnumerateDirectories(runtimeAssetsDirectory).ToArray();
        Assert.NotEmpty(owners);
        Assert.All(owners, owner =>
        {
            Assert.EndsWith(".fecs", Path.GetFileName(owner), StringComparison.Ordinal);
            Assert.NotEmpty(Directory.EnumerateFiles(owner, "*.js", SearchOption.TopDirectoryOnly));
        });
        Assert.False(File.Exists(Path.Combine(outputDirectory, ".fecs-runtime-assets")));
    }

    [Test]
    public void PinnedPackageRunsManagedValidationSurface()
    {
        var outputDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "FluentValidationBrowserOutput");
        var (standardOutput, standardError) = TestProcess.Run(
            "node",
            outputDirectory,
            TimeSpan.FromSeconds(10),
            Path.Combine(outputDirectory, "index.js"));
        Assert.True(
            string.IsNullOrWhiteSpace(standardError),
            $"FluentValidation browser fixture failed:{Environment.NewLine}{standardError}");
        var output = standardOutput.Trim().Replace("\r\n", "\n");

        Assert.Equal(
            "True\n"
            + "Code\nNotEmptyValidator\n"
            + "Code\nstarts-x\n"
            + "Customer.Name\nNotEmptyValidator\n"
            + "Lines[0].Sku\nNotEmptyValidator\n"
            + "True\nform\nTrue\n"
            + "True\n"
            + "Mode\n"
            + "True\nAsyncValue\nAsyncPredicateValidator\n"
            + "async-sync-failure\n"
            + "cancelled",
            output);
    }
}
