namespace FrontEndCSharp.Debugger.Integration;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "verify-vsix")
            {
                VsixArchiveVerifier.Verify(args[1]);
                return 0;
            }
            if (args.Length == 2 && args[0] == "verify-vsix-build-modes")
            {
                VsixBuildModeVerifier.Verify(args[1]);
                return 0;
            }
            if (args.Length == 3 && args[0] == "verify-build-package")
            {
                BuildPackageVerifier.Verify(args[1], args[2]);
                return 0;
            }
            if (args.Length == 3
                && args[0] == "verify-nuget-package-metadata"
                && int.TryParse(args[2], out var expectedPackageCount))
            {
                NuGetPackageMetadataVerifier.Verify(args[1], expectedPackageCount);
                return 0;
            }
            if (args.Length == 3
                && args[0] == "verify-nuget-org-availability"
                && int.TryParse(args[2], out var expectedAvailablePackageCount))
            {
                NuGetPackageMetadataVerifier.VerifyNuGetOrgAvailability(
                    args[1],
                    expectedAvailablePackageCount);
                return 0;
            }
            if (args.Length == 3
                && args[0] == "verify-nuget-org-publication"
                && int.TryParse(args[2], out var expectedPublishedPackageCount))
            {
                NuGetPackageMetadataVerifier.VerifyNuGetOrgPublication(
                    args[1],
                    expectedPublishedPackageCount);
                return 0;
            }
            if (args.Length == 5 && args[0] == "verify-project-template")
            {
                ProjectTemplateVerifier.Verify(args[1], args[2], args[3], args[4]);
                return 0;
            }
            if (args.Length is 1 or 2 && args[0] == "debugger-adapter-failure")
            {
                DebuggerAdapterFailure.Run(args.Length == 2 ? args[1] : null);
                return 0;
            }
            if (args.Length == 1 && args[0] == "edit-and-continue-smoke")
            {
                EditAndContinueSmoke.Run();
                return 0;
            }
            if (args.Length == 2 && args[0] == "debugger-performance")
            {
                DebuggerPerformance.Run(args[1]);
                return 0;
            }
            if (args.Length == 2 && args[0] == "debug-adapter-async-smoke")
            {
                DebugAdapterAsyncSmoke.Run(args[1]);
                return 0;
            }
            if (args.Length == 2 && args[0] == "debug-adapter-edit-and-continue-smoke")
            {
                DebugAdapterEditAndContinueSmoke.Run(args[1]);
                return 0;
            }
            if (args.Length >= 2 && args[0] == "debug-adapter-smoke")
            {
                DebugAdapterSmoke.Run(args[1], args.Skip(2).ToArray());
                return 0;
            }
            if (args.Length >= 3 && args[0] == "measure-f5-startup")
            {
                F5StartupMeasurement.Run(args.Skip(1).ToArray());
                return 0;
            }
            if (args.Length is 2 or 3 && args[0] == "install-vsix")
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException(
                        "Visual Studio VSIX installation requires Windows.");
                }
                VisualStudioVsixInstaller.Install(
                    args[1],
                    args.Length == 3 ? args[2] : "Exp");
                return 0;
            }
            if (args.Length >= 3 && args[0] == "capture-editor-window")
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException(
                        "Visual Studio automation requires Windows.");
                }
                VisualStudioEditorCapture.Capture(args.Skip(1).ToArray());
                return 0;
            }
            if (args.Length >= 4 && args[0] == "verify-semantic-classification")
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException(
                        "Visual Studio automation requires Windows.");
                }
                VisualStudioEditorCapture.VerifySemanticClassification(
                    args.Skip(1).ToArray());
                return 0;
            }
            if (args.Length >= 3 && args[0] == "verify-textmate-scopes")
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException(
                        "Visual Studio TextMate verification requires Windows.");
                }
                VisualStudioEditorCapture.VerifyTextMateScopes(args.Skip(1).ToArray());
                return 0;
            }
            if (args.Length >= 2 && args[0] == "verify-solution-rename")
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException(
                        "Visual Studio automation requires Windows.");
                }
                VisualStudioSolutionRename.Run(args.Skip(1).ToArray());
                return 0;
            }
            if (args.Length >= 4 && args[0] == "verify-completion")
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException(
                        "Visual Studio automation requires Windows.");
                }
                VisualStudioCompletionVerification.Run(args.Skip(1).ToArray());
                return 0;
            }
            if (args.Length >= 5 && args[0] == "verify-visual-studio-f5")
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException(
                        "Visual Studio automation requires Windows.");
                }
                VisualStudioF5Verification.Run(args.Skip(1).ToArray());
                return 0;
            }
            if (args.Length >= 1 && args[0] == "verify-visual-studio-project-templates")
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException(
                        "Visual Studio project-template acceptance requires Windows.");
                }
                VisualStudioProjectTemplateVerification.Run(args.Skip(1).ToArray());
                return 0;
            }

            Console.Error.WriteLine(
                "Usage: FrontEndCSharp.Debugger.Integration verify-vsix <path>\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-vsix-build-modes <repository-root>\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-build-package <path> <repository-root>\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-nuget-package-metadata <directory> <expected-package-count>\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-nuget-org-availability <directory> <expected-package-count>\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-nuget-org-publication <directory> <expected-package-count>\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-project-template <simple|fecsx> <path> <build-package-path> <repository-root>\n" +
                "   or: FrontEndCSharp.Debugger.Integration debugger-adapter-failure [output-path]\n" +
                "   or: FrontEndCSharp.Debugger.Integration edit-and-continue-smoke\n" +
                "   or: FrontEndCSharp.Debugger.Integration debugger-performance <chromium-browser-path>\n" +
                "   or: FrontEndCSharp.Debugger.Integration debug-adapter-async-smoke <chromium-browser-path>\n" +
                "   or: FrontEndCSharp.Debugger.Integration debug-adapter-edit-and-continue-smoke <chromium-browser-path>\n" +
                "   or: FrontEndCSharp.Debugger.Integration debug-adapter-smoke <chromium-browser-path> [options]\n" +
                "   or: FrontEndCSharp.Debugger.Integration measure-f5-startup <trace-path> <cold|warm> [options]\n" +
                "   or: FrontEndCSharp.Debugger.Integration install-vsix <debugger|fecsx> [root-suffix]\n" +
                "   or: FrontEndCSharp.Debugger.Integration capture-editor-window <document> <output> [options]\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-semantic-classification <document> <solution> <output> [options]\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-textmate-scopes <document> <output> [options]\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-solution-rename <output-directory> [options]\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-completion <document> <solution> <output-directory> [options]\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-visual-studio-f5 <solution> <document> <line> <output-directory> [options]\n" +
                "   or: FrontEndCSharp.Debugger.Integration verify-visual-studio-project-templates [options]");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                Environment.GetEnvironmentVariable("FRONTENDCSHARP_VERBOSE_ERRORS") == "1"
                    ? exception.ToString()
                    : "Error: " + exception.Message);
            return 1;
        }
    }

}
