using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using FrontEndCSharp.Debugging;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace FrontEndCSharp.VisualStudio;

/// <summary>
/// Transparent JavaScript debugger proxy launcher. It delegates all launch configuration and
/// process creation to Visual Studio's installed JavaScript launcher, then
/// interposes only on the returned DAP streams.
/// </summary>
[ComVisible(true)]
[Guid("FC152D13-6E45-44C6-B80C-25A153B931F8")]
public sealed class JavaScriptDebuggerProxyLauncher : IAdapterLauncher, IDebugAdapterHostComponent
{
    private const string MicrosoftAssemblyName =
        "Microsoft.VisualStudio.JavaScript.Diagnostics.JSDebugger";
    private const string MicrosoftLauncherTypeName =
        "Microsoft.VisualStudio.WebClient.Diagnostics.AdapterLauncher";
    private static readonly object BreakpointCompositionGate = new object();
    private static readonly HashSet<string> BreakpointCompositionRoots =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool blankPageCleanupEnabled;
    private IAdapterLauncher? launcher;
    private IDebugAdapterHostComponent? component;
    private string? visualStudioAppDataDirectory;
    private VisualStudioBrowserSelectionService? browserSelectionService;

    public void Initialize(IDebugAdapterHostContext context)
    {
        BrowserCompatibilityTrace.Write("proxy-initialize-entered");
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        if (launcher !=  null)
        {
            throw new InvalidOperationException("The JavaScript debugger proxy launcher is already initialized.");
        }

        var instance = CreateInstalledLauncher();
        launcher = instance as IAdapterLauncher
            ?? throw new InvalidOperationException(
                "The installed JavaScript AdapterLauncher does not implement IAdapterLauncher.");
        component = instance as IDebugAdapterHostComponent
            ?? throw new InvalidOperationException(
                "The installed JavaScript AdapterLauncher does not implement IDebugAdapterHostComponent.");
        component.Initialize(context);
        BrowserCompatibilityTrace.Write("proxy-initialize-delegate-complete");
        ThreadHelper.ThrowIfNotOnUIThread();
        visualStudioAppDataDirectory = TryReadVisualStudioAppDataDirectory(
            out var browserSelectionReason);
        Trace.WriteLine(
            "FrontEndCSharp debugger proxy browser selection: " + browserSelectionReason);
        browserSelectionService = VisualStudioBrowserSelectionService.TryCreate(
            out browserSelectionReason);
        BrowserCompatibilityTrace.Write("proxy-initialize-complete", browserSelectionReason);
        Trace.WriteLine(
            "FrontEndCSharp debugger proxy live browser selection: " + browserSelectionReason);
    }

    public void UpdateLaunchOptions(IAdapterLaunchInfo launchInfo)
    {
        if (launchInfo == null)
        {
            throw new ArgumentNullException(nameof(launchInfo));
        }

        var isChildLaunch = DapBreakpointCompositionFactory.IsBrowserChildLaunch(
            launchInfo.LaunchJson);
        if (!isChildLaunch)
        {
            lock (BreakpointCompositionGate)
            {
                blankPageCleanupEnabled = false;
            }
        }
        BrowserCompatibilityTrace.Write(
            "proxy-update-entered",
            isChildLaunch ? "child" : "root");
        GetLauncher().UpdateLaunchOptions(launchInfo);
        BrowserCompatibilityTrace.Write(
            "proxy-update-delegate-complete",
            isChildLaunch ? "child" : "root");
        string? selectedBrowserExecutable = null;
        var browserSelectionReason = "browser-selection-unavailable";
        var compatibilityOverride = Environment.GetEnvironmentVariable(
            VisualStudioBrowserSelectionService.BrowserDebugOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(compatibilityOverride))
        {
            VisualStudioBrowserLaunchConfiguration.TryResolveSelectedChromiumExecutable(
                compatibilityOverride,
                Array.Empty<string>(),
                out selectedBrowserExecutable,
                out browserSelectionReason);
        }
        else if (browserSelectionService?.TryGetBrowserInformation(
                out var liveSelectedItem,
                out _,
                out browserSelectionReason) == true
            && liveSelectedItem !=  null)
        {
            Trace.WriteLine(
                "FrontEndCSharp debugger proxy live browser selection: " + browserSelectionReason);
            VisualStudioBrowserLaunchConfiguration.TryResolveSelectedChromiumExecutable(
                liveSelectedItem,
                VisualStudioBrowserLaunchConfiguration.GetInstalledChromiumExecutableCandidates(),
                out selectedBrowserExecutable,
                out browserSelectionReason);
        }

        if (selectedBrowserExecutable == null
            && visualStudioAppDataDirectory !=  null)
        {
            VisualStudioBrowserLaunchConfiguration.TryReadSelectedChromiumExecutable(
                visualStudioAppDataDirectory,
                out selectedBrowserExecutable,
                out browserSelectionReason);
        }

        if (selectedBrowserExecutable == null)
        {
            BrowserCompatibilityTrace.Write("proxy-update-skipped", browserSelectionReason);
            Trace.WriteLine(
                "FrontEndCSharp debugger proxy browser selection: " + browserSelectionReason);
            return;
        }
        Trace.WriteLine(
            "FrontEndCSharp debugger proxy browser selection: " + browserSelectionReason);

        if (VisualStudioBrowserLaunchConfiguration.TryApplySelectedChromiumExecutable(
            launchInfo.LaunchJson,
            selectedBrowserExecutable,
            out var updatedLaunchJson,
            out var reason))
        {
            launchInfo.LaunchJson = updatedLaunchJson;
            lock (BreakpointCompositionGate)
            {
                blankPageCleanupEnabled =
                    VisualStudioBrowserLaunchConfiguration.UsesBraveDebuggerBootstrap(
                        updatedLaunchJson);
            }
            BrowserCompatibilityTrace.Write(
                "proxy-update-browser-applied",
                Path.GetFileName(selectedBrowserExecutable));
            if (!string.IsNullOrWhiteSpace(compatibilityOverride))
            {
                if (browserSelectionService?.TryGetBrowserInformation(
                        out _,
                        out var browserSelections,
                        out _) == true)
                {
                    var selectionToRestore =
                        VisualStudioBrowserLaunchConfiguration.FindBrowserSelectionForExecutable(
                            browserSelections,
                            compatibilityOverride);
                    if (selectionToRestore !=  null)
                    {
                        var restored = browserSelectionService.TrySelectBrowser(
                            selectionToRestore,
                            out var restoreReason);
                        BrowserCompatibilityTrace.Write(
                            restored
                                ? "proxy-update-selection-restored"
                                : "proxy-update-selection-restore-failed",
                            restoreReason);
                    }
                }
                Environment.SetEnvironmentVariable(
                    VisualStudioBrowserSelectionService.BrowserDebugOverrideEnvironmentVariable,
                    null,
                    EnvironmentVariableTarget.Process);
            }
        }
        BrowserCompatibilityTrace.Write("proxy-update-complete", reason);
        Trace.WriteLine("FrontEndCSharp debugger proxy browser launch: " + reason);
    }

    private static string? TryReadVisualStudioAppDataDirectory(out string reason)
    {
        try
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Package.GetGlobalService(typeof(SVsShell)) is not IVsShell shell
                || shell.GetProperty(
                    (int)__VSSPROPID.VSSPROPID_AppDataDir,
                    out var appDataDirectory) < 0
                || !(appDataDirectory is string path))
            {
                reason = "visual-studio-app-data-unavailable";
                return null;
            }

            reason = "visual-studio-app-data-found";
            return path;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            || exception is COMException)
        {
            reason = "visual-studio-browser-selection-failed:" + exception.GetType().Name;
            return null;
        }
    }

    public ITargetHostProcess LaunchAdapter(
        IAdapterLaunchInfo launchInfo,
        ITargetHostInterop targetInterop)
    {
        if (launchInfo == null)
        {
            throw new ArgumentNullException(nameof(launchInfo));
        }
        if (targetInterop == null)
        {
            throw new ArgumentNullException(nameof(targetInterop));
        }

        var isChildLaunch = DapBreakpointCompositionFactory.IsBrowserChildLaunch(
            launchInfo.LaunchJson);
        BrowserCompatibilityTrace.Write(
            "proxy-launch-entered",
            isChildLaunch ? "child" : "root");
        var process = GetLauncher().LaunchAdapter(launchInfo, targetInterop)
            ?? throw new InvalidOperationException(
                "The installed JavaScript AdapterLauncher returned no adapter process.");
        BrowserCompatibilityTrace.Write(
            "proxy-launch-delegate-complete",
            isChildLaunch ? "child" : "root");
        string[] compositionRoots;
        bool closeBlankPageTargets;
        lock (BreakpointCompositionGate)
        {
            if (!DapBreakpointCompositionFactory.IsBrowserChildLaunch(launchInfo.LaunchJson))
            {
                BreakpointCompositionRoots.Clear();
            }
            foreach (var root in DapBreakpointCompositionFactory.DiscoverCandidateRoots(
                launchInfo.LaunchJson))
            {
                BreakpointCompositionRoots.Add(root);
            }
            compositionRoots = BreakpointCompositionRoots.ToArray();
            closeBlankPageTargets = blankPageCleanupEnabled;
        }
        DapBreakpointCompositionTransform? breakpointTransform;
        string compositionReason;
        if (isChildLaunch
            && !DapBreakpointCompositionFactory.WaitForDebuggerBuildCompletion(
                compositionRoots,
                TimeSpan.FromSeconds(8)))
        {
            breakpointTransform = null;
            compositionReason = "debugger-build-still-armed";
            BrowserCompatibilityTrace.Write(
                "proxy-transform",
                compositionReason);
        }
        else
        {
            if (isChildLaunch)
            {
                BrowserCompatibilityTrace.Write(
                    "proxy-transform",
                    "debugger-build-artifacts-ready");
            }
            DapBreakpointCompositionFactory.TryCreate(
                launchInfo.LaunchJson,
                out breakpointTransform,
                out compositionReason,
                status =>
                {
                    Trace.WriteLine(
                        "FrontEndCSharp debugger proxy breakpoint transform: " + status);
                    BrowserCompatibilityTrace.Write("proxy-transform", status);
                },
                compositionRoots);
        }
        Trace.WriteLine(
            "FrontEndCSharp debugger proxy breakpoint composition: " + compositionReason);
        BrowserCompatibilityTrace.Write(
            "proxy-launch-complete",
            compositionReason);
        var browserTargetCleanup = isChildLaunch && closeBlankPageTargets
            ? new DapBrowserTargetCleanupTransform(status =>
                BrowserCompatibilityTrace.Write("proxy-blank-target", status))
            : null;
        if (breakpointTransform == null && browserTargetCleanup == null)
        {
            return process;
        }
        return new DapProxyTargetHostProcess(
            process,
            breakpointTransform,
            browserTargetCleanup,
            isChildLaunch ? "child" : "root");
    }

    private IAdapterLauncher GetLauncher() => launcher
        ?? throw new InvalidOperationException(
            "Initialize must be called before using the JavaScript debugger proxy launcher.");

    private static object CreateInstalledLauncher()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => string.Equals(
                assembly.GetName().Name,
                MicrosoftAssemblyName,
                StringComparison.Ordinal))
            .ToArray();
        if (loaded.Length > 1)
        {
            throw new InvalidOperationException(
                "More than one installed JavaScript debugger assembly is loaded.");
        }

        var assembly = loaded.Length == 1 ? loaded[0] : LoadInstalledAssembly();
        var type = assembly.GetType(MicrosoftLauncherTypeName, throwOnError: true)
            ?? throw new TypeLoadException(MicrosoftLauncherTypeName);
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                "The installed JavaScript AdapterLauncher could not be created.");
    }

    private static Assembly LoadInstalledAssembly()
    {
        var ideDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        var assemblyPath = Path.Combine(
            ideDirectory,
            "CommonExtensions",
            "Microsoft",
            "JSDiagnostics",
            "Debugger",
            MicrosoftAssemblyName + ".dll");
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                "Visual Studio's installed JavaScript debugger assembly was not found beneath the IDE directory.",
                assemblyPath);
        }

        var name = AssemblyName.GetAssemblyName(assemblyPath);
        if (!string.Equals(name.Name, MicrosoftAssemblyName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The installed JavaScript debugger assembly has an unexpected identity.");
        }
        return Assembly.LoadFrom(assemblyPath);
    }

    private sealed class DapProxyTargetHostProcess : ITargetHostProcess
    {
        private readonly ITargetHostProcess inner;
        private readonly Stream standardInput;
        private readonly Stream standardOutput;
        private readonly Guid launchId = Guid.NewGuid();
        private readonly string launchRole;

        public DapProxyTargetHostProcess(
            ITargetHostProcess inner,
            DapBreakpointCompositionTransform? breakpointTransform,
            DapBrowserTargetCleanupTransform? browserTargetCleanup,
            string launchRole)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.launchRole = launchRole ?? throw new ArgumentNullException(nameof(launchRole));
            var proxyInput = new DapTransformWriteStream(
                inner.StandardInput,
                payload =>
                {
                    var transformed = breakpointTransform?.TransformClientToAdapter(payload) ?? payload;
                    DapHarnessTrace.Write(
                        launchId,
                        this.launchRole,
                        "client-to-adapter",
                        transformed);
                    return transformed;
                },
                leaveOpen: true);
            standardInput = proxyInput;
            standardOutput = new DapTransformReadStream(
                inner.StandardOutput,
                payload =>
                {
                    var dispatch = breakpointTransform?.TransformAdapterToClientDuplex(payload)
                        ?? new DapTransformDispatch(new[] { payload }, Array.Empty<string>());
                    if (browserTargetCleanup !=  null)
                    {
                        var clientPayloads = new List<string>();
                        var adapterPayloads = new List<string>(dispatch.AdapterPayloads);
                        foreach (var clientPayload in dispatch.ClientPayloads)
                        {
                            var cleanupDispatch =
                                browserTargetCleanup.TransformAdapterToClientDuplex(clientPayload);
                            clientPayloads.AddRange(cleanupDispatch.ClientPayloads);
                            adapterPayloads.AddRange(cleanupDispatch.AdapterPayloads);
                        }
                        dispatch = new DapTransformDispatch(clientPayloads, adapterPayloads);
                    }
                    foreach (var adapterPayload in dispatch.AdapterPayloads)
                    {
                        proxyInput.InjectPayload(adapterPayload);
                    }
                    foreach (var clientPayload in dispatch.ClientPayloads)
                    {
                        DapHarnessTrace.Write(
                            launchId,
                            this.launchRole,
                            "adapter-to-client",
                            clientPayload);
                    }
                    return dispatch.ClientPayloads;
                },
                leaveOpen: true);
        }

        public IntPtr Handle => inner.Handle;
        public Stream StandardInput => standardInput;
        public Stream StandardOutput => standardOutput;
        public bool HasExited => inner.HasExited;

        public event EventHandler Exited
        {
            add => inner.Exited += value;
            remove => inner.Exited -= value;
        }

        public event DataReceivedEventHandler ErrorDataReceived
        {
            add => inner.ErrorDataReceived += value;
            remove => inner.ErrorDataReceived -= value;
        }

        public void Terminate() => inner.Terminate();
    }
}
