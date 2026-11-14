using FrontEndCSharp.Debugging;
using Newtonsoft.Json.Linq;

namespace Debugger.Tests;

public sealed class VisualStudioBrowserLaunchConfigurationTests
{
    [Test]
    public void ReadsVisualStudiosSelectedBraveExecutable()
    {
        var directory = Directory.CreateTempSubdirectory("frontendcsharp-browser-");
        try
        {
            var browser = Path.Combine(directory.FullName, "brave.exe");
            File.WriteAllText(browser, string.Empty);
            File.WriteAllText(
                Path.Combine(directory.FullName, "browsers.xml"),
                $"""
                <?xml version="1.0"?>
                <BrowserInfo><Browser><Name>Brave</Name><Path>"{browser}"</Path><IsDefault>True</IsDefault></Browser></BrowserInfo>
                """);

            Assert.True(
                VisualStudioBrowserLaunchConfiguration.TryReadSelectedChromiumExecutable(
                    directory.FullName,
                    out var selected,
                    out var reason),
                reason);
            Assert.Equal(Path.GetFullPath(browser), selected);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public void ReadsBrowserSelectionAgainAfterVisualStudioChangesIt()
    {
        var directory = Directory.CreateTempSubdirectory("frontendcsharp-browser-");
        try
        {
            var chrome = Path.Combine(directory.FullName, "chrome.exe");
            var brave = Path.Combine(directory.FullName, "brave.exe");
            File.WriteAllText(chrome, string.Empty);
            File.WriteAllText(brave, string.Empty);
            var cache = Path.Combine(directory.FullName, "browsers.xml");
            WriteBrowserCache(cache, "Chrome", chrome);

            Assert.True(
                VisualStudioBrowserLaunchConfiguration.TryReadSelectedChromiumExecutable(
                    directory.FullName,
                    out var first,
                    out var firstReason),
                firstReason);
            Assert.Equal(Path.GetFullPath(chrome), first);

            WriteBrowserCache(cache, "Brave", brave);

            Assert.True(
                VisualStudioBrowserLaunchConfiguration.TryReadSelectedChromiumExecutable(
                    directory.FullName,
                    out var second,
                    out var secondReason),
                secondReason);
            Assert.Equal(Path.GetFullPath(brave), second);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public void ResolvesLiveBraveSelectionWithoutWaitingForBrowserCacheFlush()
    {
        var directory = Directory.CreateTempSubdirectory("frontendcsharp-browser-");
        try
        {
            var brave = Path.Combine(directory.FullName, "brave.exe");
            File.WriteAllText(brave, string.Empty);

            Assert.True(
                VisualStudioBrowserLaunchConfiguration.TryResolveSelectedChromiumExecutable(
                    "Brave",
                    new[] { brave },
                    out var selected,
                    out var reason),
                reason);
            Assert.Equal(Path.GetFullPath(brave), selected);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public void ResolvesLiveBrowserSelectionWhenVisualStudioReturnsItsExecutablePath()
    {
        var directory = Directory.CreateTempSubdirectory("frontendcsharp-browser-");
        try
        {
            var brave = Path.Combine(directory.FullName, "brave.exe");
            File.WriteAllText(brave, string.Empty);

            Assert.True(
                VisualStudioBrowserLaunchConfiguration.TryResolveSelectedChromiumExecutable(
                    brave,
                    Array.Empty<string>(),
                    out var selected,
                    out var reason),
                reason);
            Assert.Equal(Path.GetFullPath(brave), selected);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public void FindsToolbarSelectionMatchingOverriddenExecutable()
    {
        var selections = new[] { "Google Chrome", "Brave", "Microsoft Edge" };

        Assert.Equal(
            "Brave",
            VisualStudioBrowserLaunchConfiguration.FindBrowserSelectionForExecutable(
                selections,
                Path.Combine(Path.GetTempPath(), "brave.exe")));
    }

    [Test]
    public void AppliesSelectedBraveToRootChromeLaunch()
    {
        var brave = Path.Combine(Path.GetTempPath(), "brave.exe");
        var launchJson = """
            {"type":"pwa-chrome","request":"launch","runtimeExecutable":"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe","url":"https://localhost:7120"}
            """;

        Assert.True(
            VisualStudioBrowserLaunchConfiguration.TryApplySelectedChromiumExecutable(
                launchJson,
                brave,
                out var updated,
                out var reason),
            reason);
        var launch = JObject.Parse(updated);
        Assert.Equal("pwa-chrome", launch.Value<string>("type"));
        Assert.Equal(Path.GetFullPath(brave), launch.Value<string>("runtimeExecutable"));
        Assert.Equal(
            new[] { "data:,frontendcsharp-debugger-bootstrap" },
            launch["runtimeArgs"]!.Values<string>());
        Assert.Equal(
            "data:,frontendcsharp-debugger-bootstrap",
            launch.Value<string>("urlFilter"));
        Assert.True(
            VisualStudioBrowserLaunchConfiguration.UsesBraveDebuggerBootstrap(updated));
    }

    [Test]
    public void PreservesExistingBravePositionalBootstrapArgument()
    {
        var brave = Path.Combine(Path.GetTempPath(), "brave.exe");
        var launchJson = """
            {"type":"pwa-chrome","request":"launch","runtimeArgs":["--incognito","https://example.test/bootstrap"],"url":"https://localhost:7120"}
            """;

        Assert.True(
            VisualStudioBrowserLaunchConfiguration.TryApplySelectedChromiumExecutable(
                launchJson,
                brave,
                out var updated,
                out var reason),
            reason);

        Assert.Equal(
            new[] { "--incognito", "https://example.test/bootstrap" },
            JObject.Parse(updated)["runtimeArgs"]!.Values<string>());
        Assert.Null(JObject.Parse(updated)["urlFilter"]);
        Assert.False(
            VisualStudioBrowserLaunchConfiguration.UsesBraveDebuggerBootstrap(updated));
    }

    [Test]
    public void DoesNotAddBraveBootstrapArgumentToOtherChromiumBrowsers()
    {
        var edge = Path.Combine(Path.GetTempPath(), "msedge.exe");
        var launchJson = """
            {"type":"pwa-msedge","request":"launch","runtimeArgs":["--inprivate"],"url":"https://localhost:7120"}
            """;

        Assert.True(
            VisualStudioBrowserLaunchConfiguration.TryApplySelectedChromiumExecutable(
                launchJson,
                edge,
                out var updated,
                out var reason),
            reason);

        Assert.Equal(
            new[] { "--inprivate" },
            JObject.Parse(updated)["runtimeArgs"]!.Values<string>());
    }

    [Test]
    public void DoesNotChangeChildOrAttachSessions()
    {
        var brave = Path.Combine(Path.GetTempPath(), "brave.exe");

        Assert.False(
            VisualStudioBrowserLaunchConfiguration.TryApplySelectedChromiumExecutable(
                "{\"type\":\"pwa-chrome\",\"request\":\"launch\",\"__jsDebugChildServer\":1234}",
                brave,
                out _,
                out _));
        Assert.False(
            VisualStudioBrowserLaunchConfiguration.TryApplySelectedChromiumExecutable(
                "{\"type\":\"pwa-chrome\",\"request\":\"launch\",\"__pendingTargetId\":\"target-1\"}",
                brave,
                out _,
                out _));
        Assert.False(
            VisualStudioBrowserLaunchConfiguration.TryApplySelectedChromiumExecutable(
                "{\"type\":\"pwa-chrome\",\"request\":\"attach\"}",
                brave,
                out _,
                out _));
    }

    private static void WriteBrowserCache(string path, string name, string browserPath) =>
        File.WriteAllText(
            path,
            $"""
            <?xml version="1.0"?>
            <BrowserInfo><Browser><Name>{name}</Name><Path>"{browserPath}"</Path><IsDefault>True</IsDefault></Browser></BrowserInfo>
            """);
}
