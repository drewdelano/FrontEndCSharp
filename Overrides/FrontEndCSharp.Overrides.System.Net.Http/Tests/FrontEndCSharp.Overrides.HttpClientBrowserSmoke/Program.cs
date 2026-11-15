using System.Diagnostics;
using System.Text;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project Overrides/FrontEndCSharp.Overrides.System.Net.Http/Tests/FrontEndCSharp.Overrides.HttpClientBrowserSmoke/FrontEndCSharp.Overrides.HttpClientBrowserSmoke.csproj -- <chromium-browser-path>");
    return 2;
}

var browserPath = Path.GetFullPath(args[0]);
if (!File.Exists(browserPath))
{
    Console.Error.WriteLine($"Chromium executable not found: {browserPath}");
    return 2;
}

var stopwatch = Stopwatch.StartNew();
var repositoryRoot = RepositoryPaths.FindRoot(Environment.CurrentDirectory);
var fixtureRoot = Path.Combine(
    repositoryRoot,
    "Overrides",
    "FrontEndCSharp.Overrides.System.Net.Http",
    "Tests",
    "FrontEndCSharp.Overrides.HttpClientBrowserFixture");
var fixtureProject = Path.Combine(fixtureRoot, "FrontEndCSharp.Overrides.HttpClientBrowserFixture.csproj");
var fixtureAssembly = Path.Combine(
    fixtureRoot,
    "bin",
    "Debug",
    "net8.0",
    "HttpClientBrowserFixture.dll");
var compilerPath = Path.Combine(
    repositoryRoot,
    "Compiler",
    "Core",
    "bin",
    "Debug",
    "net8.0",
    "Compiler.dll");
var generatedOutput = TemporaryDirectory.Create("FrontEndCSharp-HttpClient-Smoke-");
var browserProfile = TemporaryDirectory.Create("FrontEndCSharp-HttpClient-Browser-");

using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try
{
    await BoundedProcess.RunAsync(
        "dotnet",
        [compilerPath, "--no-bundle", fixtureProject, generatedOutput, fixtureAssembly],
        repositoryRoot,
        "FrontEndCSharp compilation",
        deadline.Token);

    await using var server = new HttpClientBrowserServer(generatedOutput);
    await File.WriteAllTextAsync(
        Path.Combine(generatedOutput, "index.html"),
        BuildHostPage(server),
        Encoding.UTF8,
        deadline.Token);

    var browserResult = await ChromiumRunner.RunAsync(
        browserPath,
        repositoryRoot,
        browserProfile,
        server.Origin,
        deadline.Token);
    HttpClientSmokeAssertions.Verify(browserResult);

    stopwatch.Stop();
    if (stopwatch.Elapsed >= TimeSpan.FromSeconds(10))
    {
        throw new TimeoutException(
            $"The HTTP browser smoke completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds; "
                + "tests must complete in under 10 seconds.");
    }

    var durationClass = stopwatch.Elapsed >= TimeSpan.FromSeconds(5) ? "slow" : "normal";
    Console.WriteLine(
        $"Browser HttpClient integration passed in Chromium in {stopwatch.Elapsed.TotalSeconds:F2} seconds "
            + $"[{durationClass}; 5-second slow bar; 10-second maximum].");
    return 0;
}
catch (OperationCanceledException exception) when (deadline.IsCancellationRequested)
{
    throw new TimeoutException(
        "The HTTP browser smoke exceeded its absolute 10-second execution limit.",
        exception);
}
finally
{
    TemporaryDirectory.Delete(generatedOutput);
    TemporaryDirectory.Delete(browserProfile);
}

static string BuildHostPage(HttpClientBrowserServer server)
{
    return "<!doctype html><html><head><meta charset=\"utf-8\"></head>"
        + $"<body data-http-endpoint=\"{server.Origin}http-probe\""
        + $" data-http-echo-endpoint=\"{server.Origin}http-echo\""
        + $" data-http-base-endpoint=\"{server.Origin}\">"
        + "<script>addEventListener('error',event=>document.body.dataset.fecsError="
        + "event.error?.stack??event.message);addEventListener('unhandledrejection',event=>"
        + "document.body.dataset.fecsError=event.reason?.stack??String(event.reason));</script>"
        + "<script type=\"module\" src=\"/index.js\"></script></body></html>\n";
}

internal static class RepositoryPaths
{
    public static string FindRoot(string start)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(start));
             directory != null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln"))
                && File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the FrontEndCSharp repository above '{start}'.");
    }
}

internal static class TemporaryDirectory
{
    public static string Create(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void Delete(string path)
    {
        for (var attempt = 0; attempt < 10 && Directory.Exists(path); attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }

        if (Directory.Exists(path))
        {
            Console.Error.WriteLine($"Warning: could not remove temporary directory '{path}'.");
        }
    }
}
