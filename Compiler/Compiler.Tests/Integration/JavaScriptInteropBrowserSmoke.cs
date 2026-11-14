namespace FrontEndCSharp.Compiler.Integration;

internal static class JavaScriptInteropBrowserSmoke
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);

    public static void Run(string browserPath, bool noBuild)
    {
        browserPath = Path.GetFullPath(browserPath);
        if (!File.Exists(browserPath))
        {
            throw new FileNotFoundException("The Chromium browser does not exist.", browserPath);
        }
        var repositoryRoot = RepositoryLayout.Root;
        var fixtureRoot = Path.Combine(
            repositoryRoot,
            "Compiler",
            "Compiler.Tests",
            "Fixtures",
            "JavaScriptInteropFixture");
        var fixtureProject = Path.Combine(fixtureRoot, "JavaScriptInteropFixture.csproj");
        var fixtureAssembly = Path.Combine(
            fixtureRoot,
            "bin",
            "Debug",
            "net8.0",
            "JavaScriptInteropFixture.dll");
        var compilerProject = Path.Combine(
            repositoryRoot,
            "Compiler",
            "Core",
            "Compiler.csproj");
        var compilerPath = Path.Combine(
            repositoryRoot,
            "Compiler",
            "Core",
            "bin",
            "Debug",
            "net8.0",
            "Compiler.dll");
        var generatedOutput = CreateTemporaryDirectory(
            "FrontEndCSharp-JavaScriptInterop-Smoke-");
        var browserProfile = CreateTemporaryDirectory(
            "FrontEndCSharp-JavaScriptInterop-Browser-");
        try
        {
            if (!noBuild)
            {
                RunDotNet(repositoryRoot, "build", compilerProject, "--configuration", "Debug");
                RunDotNet(repositoryRoot, "build", fixtureProject, "--configuration", "Debug");
            }
            RunDotNet(
                repositoryRoot,
                compilerPath,
                fixtureProject,
                generatedOutput,
                fixtureAssembly);
            File.WriteAllText(
                Path.Combine(generatedOutput, "index.html"),
                "<!doctype html><html><head><meta charset=\"utf-8\"></head><body>"
                + "<script type=\"module\" src=\"/index.js\"></script></body></html>"
                + Environment.NewLine);

            using var server = new StaticFileServer(generatedOutput);
            var browser = BoundedProcess.Run(
                browserPath,
                repositoryRoot,
                TimeSpan.FromSeconds(30),
                "--headless=new",
                "--disable-gpu",
                "--no-default-browser-check",
                "--no-first-run",
                "--dump-dom",
                "--virtual-time-budget=2000",
                "--user-data-dir=" + browserProfile,
                server.Url);
            var dom = browser.StandardOutput;
            var diagnostics = "\nChromium diagnostics:\n" + browser.StandardError
                + "\nFailed HTTP requests:\n" + string.Join("\n", server.FailedRequests);
            Require(dom, "data-fecs-status=\"ready\"", "Interop fixture did not report success.", diagnostics);
            Require(dom, "Managed React", "Managed React output was absent.", diagnostics);
            Require(dom, "Managed Motion", "Managed Motion output was absent.", diagnostics);
            Require(dom, "FrontEndCSharp interop", "Browser output was absent.", diagnostics);
            Console.WriteLine("Verified managed browser, React, MobX, and Motion interop in Chromium.");
        }
        finally
        {
            Directory.Delete(generatedOutput, recursive: true);
            Directory.Delete(browserProfile, recursive: true);
        }
    }

    private static void Require(string text, string marker, string message, string diagnostics)
    {
        if (!text.Contains(marker, StringComparison.Ordinal))
        {
            throw new InvalidDataException(message + "\nDOM:\n" + text + diagnostics);
        }
    }

    private static string CreateTemporaryDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void RunDotNet(string workingDirectory, params string[] arguments) =>
        BoundedProcess.Run("dotnet", workingDirectory, BuildTimeout, arguments);
}
