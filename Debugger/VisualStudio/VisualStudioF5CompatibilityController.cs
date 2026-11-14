using EnvDTE;
using EnvDTE80;
using FrontEndCSharp.Debugging;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FrontEndCSharp.VisualStudio;

internal sealed class VisualStudioF5CompatibilityController : IOleCommandTarget, IDisposable
{
    private const uint StartDebugTargetCommandId = 257;
    private static readonly Guid DebugTargetCommandSet = new("6e87cfad-6c05-4adf-9cd7-3b7943875b7c");

    private readonly DTE2 dte;
    private readonly VisualStudioDebuggerOutput output;
    private readonly IVsRegisterPriorityCommandTarget commandTargets;
    private readonly uint commandTargetCookie;
    private readonly BuildEvents buildEvents;
    private VisualStudioBrowserSelectionService? browserSelectionService;
    private string? browserSelectionToRestore;
    private bool standardF5BuildArmed;

    internal VisualStudioF5CompatibilityController(
        DTE2 dte,
        VisualStudioDebuggerOutput output,
        IVsRegisterPriorityCommandTarget commandTargets)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        this.dte = dte;
        this.output = output;
        this.commandTargets = commandTargets;
        buildEvents = dte.Events.BuildEvents;
        buildEvents.OnBuildDone += OnBuildDone;
        ErrorHandler.ThrowOnFailure(commandTargets.RegisterPriorityCommandTarget(
            0,
            this,
            out commandTargetCookie));
        BrowserCompatibilityTrace.Write(
            "priority-command-target-registered",
            "cookie=" + commandTargetCookie.ToString(CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        commandTargets.UnregisterPriorityCommandTarget(commandTargetCookie);
        buildEvents.OnBuildDone -= OnBuildDone;
        RestoreBrowserSelection();
    }

    internal void RestoreBrowserSelection()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        Environment.SetEnvironmentVariable(
            VisualStudioBrowserSelectionService.BrowserDebugOverrideEnvironmentVariable,
            null,
            EnvironmentVariableTarget.Process);
        var selection = browserSelectionToRestore;
        browserSelectionToRestore = null;
        if (selection !=  null && browserSelectionService !=  null)
        {
            browserSelectionService.TrySelectBrowser(selection, out _);
        }
    }

    int IOleCommandTarget.QueryStatus(
        ref Guid commandGroup,
        uint commandCount,
        OLECMD[] commands,
        IntPtr commandText) =>
        (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;

    int IOleCommandTarget.Exec(
        ref Guid commandGroup,
        uint commandId,
        uint commandExecutionOptions,
        IntPtr input,
        IntPtr outputPointer)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var isStartCommand = commandGroup == VSConstants.GUID_VSStandardCommandSet97
                && commandId == (uint)VSConstants.VSStd97CmdID.Start
            || commandGroup == VSConstants.VSStd2K
                && commandId == (uint)VSConstants.VSStd2KCmdID.PROJSTARTDEBUG
            || commandGroup == DebugTargetCommandSet
                && commandId == StartDebugTargetCommandId;
        if (isStartCommand)
        {
            BrowserCompatibilityTrace.Write(
                "start-command-observed",
                commandGroup.ToString("D") + ":" + commandId.ToString(CultureInfo.InvariantCulture));
            ArmStandardF5Build();
            PrepareBraveCompatibility();
        }

        return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
    }

    private void PrepareBraveCompatibility()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        BrowserCompatibilityTrace.Write("prepare-brave-entered");
        if (dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
        {
            BrowserCompatibilityTrace.Write("prepare-brave-skipped", "debugger-not-in-design-mode");
            return;
        }

        var serviceReason = "live-browser-selection-service-unavailable";
        browserSelectionService ??= VisualStudioBrowserSelectionService.TryCreate(out serviceReason);
        if (browserSelectionService == null)
        {
            BrowserCompatibilityTrace.Write("prepare-brave-failed", serviceReason);
            output.WriteLine("Brave JavaScript-debugger compatibility was not applied: " + serviceReason);
            return;
        }

        if (!browserSelectionService.TryGetBrowserInformation(
                out var selectedBrowser,
                out var browsers,
                out var selectionReason))
        {
            BrowserCompatibilityTrace.Write("prepare-brave-failed", selectionReason);
            output.WriteLine("Brave JavaScript-debugger compatibility was not applied: " + selectionReason);
            return;
        }
        if (selectedBrowser == null
            || string.IsNullOrWhiteSpace(selectedBrowser)
            || selectedBrowser.IndexOf("brave", StringComparison.OrdinalIgnoreCase) < 0)
        {
            BrowserCompatibilityTrace.Write("prepare-brave-skipped", "selected-browser-is-not-brave");
            return;
        }

        var chromeSelection = browsers.FirstOrDefault(browser =>
            browser.IndexOf("chrome", StringComparison.OrdinalIgnoreCase) >= 0
            && browser.IndexOf("brave", StringComparison.OrdinalIgnoreCase) < 0);
        if (chromeSelection == null
            || !VisualStudioBrowserLaunchConfiguration.TryResolveSelectedChromiumExecutable(
                selectedBrowser!,
                VisualStudioBrowserLaunchConfiguration.GetInstalledChromiumExecutableCandidates(),
                out var braveExecutable,
                out _)
            || braveExecutable == null)
        {
            BrowserCompatibilityTrace.Write("prepare-brave-failed", "chrome-or-brave-executable-unavailable");
            return;
        }

        Environment.SetEnvironmentVariable(
            VisualStudioBrowserSelectionService.BrowserDebugOverrideEnvironmentVariable,
            braveExecutable,
            EnvironmentVariableTarget.Process);
        browserSelectionToRestore = selectedBrowser;
        if (!browserSelectionService.TrySelectBrowser(chromeSelection, out var reason))
        {
            Environment.SetEnvironmentVariable(
                VisualStudioBrowserSelectionService.BrowserDebugOverrideEnvironmentVariable,
                null,
                EnvironmentVariableTarget.Process);
            browserSelectionToRestore = null;
            BrowserCompatibilityTrace.Write("prepare-brave-failed", reason);
            output.WriteLine("Brave JavaScript-debugger compatibility was not applied: " + reason);
            return;
        }

        BrowserCompatibilityTrace.Write("prepare-brave-applied");
        output.WriteLine("Using Visual Studio's Chrome-compatible JavaScript launch path for Brave.");
    }

    private void ArmStandardF5Build()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            var project = GetStartupProject();
            var projectPath = Path.GetFullPath(project.FullName);
            if (!string.Equals(Path.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            var projectText = File.ReadAllText(projectPath);
            if (projectText.IndexOf("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) < 0
                || projectText.IndexOf("<FrontEndCSharpProject>", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(dte.Solution.FullName));
            if (string.IsNullOrWhiteSpace(solutionDirectory))
            {
                return;
            }
            var armPath = Path.Combine(solutionDirectory, ".vs", "FrontEndCSharp", "debugger.arm");
            Directory.CreateDirectory(Path.GetDirectoryName(armPath)!);
            File.WriteAllText(
                armPath,
                Guid.NewGuid().ToString("N") + Environment.NewLine,
                Encoding.ASCII);
            standardF5BuildArmed = true;
            BrowserCompatibilityTrace.Write("standard-f5-build-armed", armPath);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            || exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException)
        {
            BrowserCompatibilityTrace.Write("standard-f5-build-arm-failed", exception.Message);
        }
    }

    private void OnBuildDone(vsBuildScope scope, vsBuildAction action)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (!standardF5BuildArmed)
        {
            return;
        }
        standardF5BuildArmed = false;
        BrowserCompatibilityTrace.Write(
            "standard-f5-build-complete",
            scope.ToString() + ":" + action.ToString());
    }

    private Project GetStartupProject()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (dte.Solution.SolutionBuild.StartupProjects is not Array startupProjects
            || startupProjects.Cast<object>().OfType<string>().ToArray() is not { Length: 1 } startupProjectNames)
        {
            throw new InvalidOperationException("Visual Studio does not have exactly one startup project.");
        }

        var startupProjectName = startupProjectNames[0];
        foreach (Project project in dte.Solution.Projects)
        {
            var match = FindProject(project, startupProjectName);
            if (match !=  null)
            {
                return match;
            }
        }
        throw new InvalidOperationException(
            $"Visual Studio's startup project '{startupProjectName}' could not be resolved.");
    }

    private static Project? FindProject(Project project, string uniqueName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (string.Equals(project.UniqueName, uniqueName, StringComparison.OrdinalIgnoreCase))
        {
            return project;
        }
        if (project.ProjectItems == null)
        {
            return null;
        }
        foreach (ProjectItem item in project.ProjectItems)
        {
            if (item.SubProject is Project subProject
                && FindProject(subProject, uniqueName) is Project match)
            {
                return match;
            }
        }
        return null;
    }
}
