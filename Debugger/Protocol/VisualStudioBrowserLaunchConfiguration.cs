using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

public static class VisualStudioBrowserLaunchConfiguration
{
    private const string BraveDebuggerBootstrapUrl =
        "data:,frontendcsharp-debugger-bootstrap";

    private static readonly string[] SupportedChromiumExecutables =
    {
        "brave.exe",
        "chrome.exe",
        "msedge.exe"
    };

    public static bool TryResolveSelectedChromiumExecutable(
        string selectedItem,
        IEnumerable<string> candidateExecutablePaths,
        out string? executablePath,
        out string reason)
    {
        executablePath = null;
        if (string.IsNullOrWhiteSpace(selectedItem))
        {
            reason = "live-browser-selection-unavailable";
            return false;
        }

        var normalizedSelection = selectedItem.Trim().Trim('"');
        var candidates = candidateExecutablePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim().Trim('"'))
            .Prepend(normalizedSelection)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!Path.IsPathRooted(candidate)
                || !IsSupportedChromiumExecutable(candidate)
                || !File.Exists(candidate))
            {
                continue;
            }

            if (string.Equals(candidate, normalizedSelection, StringComparison.OrdinalIgnoreCase)
                || SelectionMatchesExecutable(normalizedSelection, candidate))
            {
                executablePath = Path.GetFullPath(candidate);
                reason = "live-browser-selection-resolved";
                return true;
            }
        }

        reason = "live-browser-selection-not-supported:" + normalizedSelection;
        return false;
    }

    public static bool TryReadSelectedChromiumExecutable(
        string visualStudioAppDataDirectory,
        out string? executablePath,
        out string reason)
    {
        executablePath = null;
        if (string.IsNullOrWhiteSpace(visualStudioAppDataDirectory))
        {
            reason = "visual-studio-app-data-unavailable";
            return false;
        }

        var browserCachePath = Path.Combine(visualStudioAppDataDirectory, "browsers.xml");
        if (!File.Exists(browserCachePath))
        {
            reason = "browser-cache-not-found";
            return false;
        }

        try
        {
            var browser = XDocument.Load(browserCachePath, LoadOptions.None)
                .Descendants("Browser")
                .FirstOrDefault(element =>
                    string.Equals(
                        element.Element("IsDefault")?.Value,
                        "true",
                        StringComparison.OrdinalIgnoreCase));
            var configuredPath = browser?.Element("Path")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                reason = "selected-browser-path-unavailable";
                return false;
            }

            configuredPath = configuredPath!.Trim('"');
            if (!Path.IsPathRooted(configuredPath)
                || !IsSupportedChromiumExecutable(configuredPath))
            {
                reason = "selected-browser-is-not-supported-chromium";
                return false;
            }
            if (!File.Exists(configuredPath))
            {
                reason = "selected-browser-executable-not-found";
                return false;
            }

            executablePath = Path.GetFullPath(configuredPath);
            reason = "selected-browser-found";
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is System.Xml.XmlException)
        {
            reason = "browser-cache-read-failed:" + exception.GetType().Name;
            return false;
        }
    }

    public static IReadOnlyList<string> GetInstalledChromiumExecutableCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new[]
        {
            Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(programFilesX86, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe")
        };
    }

    public static string? FindBrowserSelectionForExecutable(
        IEnumerable<string> browserSelections,
        string executablePath)
    {
        if (browserSelections == null || string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        return browserSelections.FirstOrDefault(selection =>
            !string.IsNullOrWhiteSpace(selection)
            && SelectionMatchesExecutable(selection, executablePath));
    }

    public static bool TryApplySelectedChromiumExecutable(
        string launchJson,
        string executablePath,
        out string updatedLaunchJson,
        out string reason)
    {
        updatedLaunchJson = launchJson;
        if (string.IsNullOrWhiteSpace(launchJson)
            || string.IsNullOrWhiteSpace(executablePath))
        {
            reason = "launch-or-browser-path-unavailable";
            return false;
        }

        try
        {
            var launch = JObject.Parse(launchJson);
            if (launch["__jsDebugChildServer"] !=  null
                || launch["__pendingTargetId"] !=  null
                || launch["sessionId"] !=  null)
            {
                reason = "browser-child-session";
                return false;
            }

            var request = launch.Value<string>("request");
            if (!string.IsNullOrWhiteSpace(request)
                && !string.Equals(request, "launch", StringComparison.OrdinalIgnoreCase))
            {
                reason = "not-browser-launch";
                return false;
            }

            var adapterType = launch.Value<string>("type");
            if (!string.Equals(adapterType, "pwa-chrome", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(adapterType, "chrome", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(adapterType, "pwa-msedge", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(adapterType, "edge", StringComparison.OrdinalIgnoreCase))
            {
                reason = "not-chromium-debug-adapter";
                return false;
            }

            var executableName = Path.GetFileName(executablePath);
            if (!SupportedChromiumExecutables.Contains(
                executableName,
                StringComparer.OrdinalIgnoreCase))
            {
                reason = "selected-browser-is-not-supported-chromium";
                return false;
            }

            launch["type"] = string.Equals(
                executableName,
                "msedge.exe",
                StringComparison.OrdinalIgnoreCase)
                ? "pwa-msedge"
                : "pwa-chrome";
            launch["runtimeExecutable"] = Path.GetFullPath(executablePath);
            if (string.Equals(
                    executableName,
                    "brave.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                var runtimeArgs = launch["runtimeArgs"] as JArray ?? new JArray();
                if (!runtimeArgs.Values<string>().Any(argument =>
                        !string.IsNullOrWhiteSpace(argument)
                        && !argument!.StartsWith("-", StringComparison.Ordinal)))
                {
                    runtimeArgs.Add(BraveDebuggerBootstrapUrl);
                }
                launch["runtimeArgs"] = runtimeArgs;
                if (runtimeArgs.Values<string>().Any(argument =>
                        string.Equals(
                            argument,
                            BraveDebuggerBootstrapUrl,
                            StringComparison.Ordinal)))
                {
                    launch["urlFilter"] = BraveDebuggerBootstrapUrl;
                }
            }
            updatedLaunchJson = launch.ToString(Newtonsoft.Json.Formatting.None);
            reason = "selected-browser-applied";
            return true;
        }
        catch (Exception exception) when (
            exception is Newtonsoft.Json.JsonException
            || exception is ArgumentException
            || exception is NotSupportedException)
        {
            reason = "browser-launch-transform-failed:" + exception.GetType().Name;
            return false;
        }
    }

    public static bool UsesBraveDebuggerBootstrap(string launchJson)
    {
        if (string.IsNullOrWhiteSpace(launchJson))
        {
            return false;
        }

        try
        {
            var launch = JObject.Parse(launchJson);
            return string.Equals(
                    Path.GetFileName(launch.Value<string>("runtimeExecutable")),
                    "brave.exe",
                    StringComparison.OrdinalIgnoreCase)
                && launch["runtimeArgs"] is JArray runtimeArgs
                && runtimeArgs.Values<string>().Any(argument =>
                    string.Equals(
                        argument,
                        BraveDebuggerBootstrapUrl,
                        StringComparison.Ordinal));
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return false;
        }
    }

    private static bool IsSupportedChromiumExecutable(string path) =>
        SupportedChromiumExecutables.Contains(
            Path.GetFileName(path),
            StringComparer.OrdinalIgnoreCase);

    private static bool SelectionMatchesExecutable(string selection, string executablePath)
    {
        var executableName = Path.GetFileName(executablePath);
        if (string.Equals(executableName, "brave.exe", StringComparison.OrdinalIgnoreCase))
        {
            return selection.IndexOf("brave", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        if (string.Equals(executableName, "chrome.exe", StringComparison.OrdinalIgnoreCase))
        {
            return selection.IndexOf("chrome", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        if (string.Equals(executableName, "msedge.exe", StringComparison.OrdinalIgnoreCase))
        {
            return selection.IndexOf("edge", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        return false;
    }
}
