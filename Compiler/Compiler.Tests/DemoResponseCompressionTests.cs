namespace Compiler.Tests;

public sealed class DemoResponseCompressionTests
{
    [Test]
    public void DemoPrefersBrotliAndCompressesPublishedBinaryAssets()
    {
        var programPath = Path.Combine(
            FindRepositoryRoot(),
            "Demo",
            "FrontEndCSharp.Demo",
            "Program.cs");
        var program = File.ReadAllText(programPath);

        Assert.Contains("AddResponseCompression", program);
        Assert.Contains("BrotliCompressionProvider", program);
        Assert.Contains("GzipCompressionProvider", program);
        Assert.Contains("application/octet-stream", program);
        Assert.True(
            program.IndexOf("BrotliCompressionProvider", StringComparison.Ordinal) <
            program.IndexOf("GzipCompressionProvider", StringComparison.Ordinal));
        Assert.True(
            program.IndexOf("UseResponseCompression", StringComparison.Ordinal) <
            program.IndexOf("UseStaticFiles", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory !=  null && !File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the FrontEndCSharp repository root.");
    }
}
