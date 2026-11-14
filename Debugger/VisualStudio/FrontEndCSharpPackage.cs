using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FrontEndCSharp.VisualStudio;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration(
    "FrontEndCSharp Debugger",
    "Debug browser-executed FrontEndCSharp programs with Visual Studio's built-in debugger UI.",
    "0.1.19")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(PackageGuidString)]
public sealed class FrontEndCSharpPackage : AsyncPackage
{
    public const string PackageGuidString = "cfcb81f4-801e-4f6b-a582-3dcd42ca0218";
    private DebugWithFrontEndCSharpCommand? debuggerCommand;

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        BrowserCompatibilityTrace.Write(
            "package-initialize-start",
            "pid=" + Process.GetCurrentProcess().Id);
        debuggerCommand = await DebugWithFrontEndCSharpCommand.InitializeAsync(this, cancellationToken);
        BrowserCompatibilityTrace.Write(
            debuggerCommand == null ? "package-initialize-incomplete" : "package-initialize-complete");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && debuggerCommand !=  null)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                debuggerCommand.Dispose();
            });
            debuggerCommand = null;
        }

        base.Dispose(disposing);
    }
}
