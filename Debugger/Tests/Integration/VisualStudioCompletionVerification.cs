using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace FrontEndCSharp.Debugger.Integration;

[SupportedOSPlatform("windows")]
internal static class VisualStudioCompletionVerification
{
    public static void Run(IReadOnlyList<string> arguments)
    {
        var options = CompletionOptions.Parse(arguments);
        if (options.TailwindAdvancedOnly && options.UsesTailwindCompletionFixture)
        {
            BuildTailwindCompletionFixture(options.SolutionPath);
        }
        Directory.CreateDirectory(options.OutputDirectory);
        var diagnosticPath = Path.Combine(
            options.OutputDirectory,
            "fecsx-completion-commit-manager.log");
        File.Delete(diagnosticPath);
        Environment.SetEnvironmentVariable(
            "FRONTENDCSHARP_FECSX_COMPLETION_LOG",
            diagnosticPath);

        var existing = VisualStudioAutomation.GetHiveProcessIds(options.RootSuffix);
        if (options.CloseExisting)
        {
            VisualStudioAutomation.CloseProcesses(existing);
            existing = [];
        }
        if (existing.Count > 1)
        {
            throw new InvalidOperationException(
                $"More than one '{options.RootSuffix}' Visual Studio instance is open. "
                + "Use --close-existing.");
        }

        Process process;
        int? launchedProcessId = null;
        if (existing.Count == 1)
        {
            process = Process.GetProcessById(existing[0]);
        }
        else
        {
            process = Launch(options);
            launchedProcessId = process.Id;
        }

        var completed = false;
        try
        {
            var deadline = DateTime.UtcNow + options.Timeout;
            dynamic dte = VisualStudioAutomation.WaitForDte(
                process,
                deadline,
                options.AcceptProjectSecurityWarnings);
            VisualStudioAutomation.WaitForSolution(
                dte,
                process,
                deadline,
                options.AcceptProjectSecurityWarnings);
            dte.ItemOperations.OpenFile(options.DocumentPath);
            VisualStudioAutomation.WaitForDocument(
                dte,
                options.DocumentPath,
                DateTime.UtcNow + TimeSpan.FromSeconds(30));

            var session = new CompletionSession(
                options,
                process,
                dte,
                diagnosticPath);
            try
            {
                session.Verify();
                completed = true;
            }
            finally
            {
                session.RestoreDocument();
            }
        }
        finally
        {
            if (launchedProcessId.HasValue && (!options.KeepOpen || !completed))
            {
                VisualStudioAutomation.CloseProcesses([launchedProcessId.Value]);
                Console.WriteLine(
                    "Closed the Visual Studio process launched for completion capture.");
            }
        }
    }

    private static void BuildTailwindCompletionFixture(string projectPath)
    {
        var result = BoundedProcess.Run(
            "dotnet",
            Path.GetDirectoryName(projectPath)!,
            TimeSpan.FromMinutes(3),
            "build",
            projectPath,
            "--nologo");
        Console.WriteLine(
            "Built the Tailwind completion fixture and its project-specific editor catalog.");
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            Console.WriteLine(result.StandardOutput.Trim());
        }
    }

    private static Process Launch(CompletionOptions options)
    {
        var start = new ProcessStartInfo
        {
            FileName = VisualStudioAutomation.FindDevenv(),
            UseShellExecute = false,
        };
        start.ArgumentList.Add("/RootSuffix");
        start.ArgumentList.Add(options.RootSuffix);
        start.ArgumentList.Add(options.SolutionPath);
        return Process.Start(start)
            ?? throw new InvalidOperationException("Visual Studio did not start.");
    }

    private sealed class CompletionSession
    {
        private readonly CompletionOptions _options;
        private readonly Process _process;
        private readonly dynamic _dte;
        private readonly string _completionDiagnosticPath;
        private string[] _documentLines;
        private string? _pendingOriginalDocumentText;
        private int _pendingUndoCount;

        public CompletionSession(
            CompletionOptions options,
            Process process,
            object dte,
            string completionDiagnosticPath)
        {
            _options = options;
            _process = process;
            _dte = dte;
            _completionDiagnosticPath = completionDiagnosticPath;
            _documentLines = File.ReadAllLines(options.DocumentPath);
        }

        public void Verify()
        {
            if (_options.NavigationOnly)
            {
                VerifyNavigation();
                return;
            }
            if (_options.TailwindRetargetOnly)
            {
                VerifyTailwindSessionRetargeting();
                return;
            }
            if (_options.TailwindAdvancedOnly)
            {
                VerifyTailwindAdvancedSyntax();
                return;
            }

            foreach (var scenario in CompletionScenario.StandardScenarios)
            {
                if (_options.TailwindOnly
                    && !scenario.Name.StartsWith("tailwind", StringComparison.Ordinal))
                {
                    continue;
                }
                VerifyPopup(scenario);
            }
            VerifyTailwindSessionRetargeting();
            VerifyTailwindAdvancedSyntax();
            if (_options.TailwindOnly)
            {
                return;
            }
            VerifyInvalidMarkupCommit();
            VerifyImportCommit();
            VerifyNavigation();
            VerifyNamedComponentCompletion();
            if (!_dte.ActiveDocument.Saved)
            {
                throw new InvalidOperationException(
                    $"'{_dte.ActiveDocument.FullName}' remained modified after "
                    + "completion and navigation automation.");
            }
        }

        public void RestoreDocument()
        {
            try
            {
                VisualStudioAutomation.SendEscape();
                UndoPendingChanges(waitForOriginal: false);
            }
            catch (COMException)
            {
            }
        }

        private void VerifyPopup(CompletionScenario scenario)
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            if (scenario.TextToReplace != null)
            {
                ReplaceLineText(
                    selection,
                    _documentLines,
                    scenario.Line,
                    scenario.TextToReplace,
                    scenario.Replacement!);
                _pendingUndoCount = 1;
            }
            else
            {
                var offset = GetLineOffsetAfter(
                    _documentLines,
                    scenario.Line,
                    scenario.CaretAfter!);
                selection.MoveToLineAndOffset(scenario.Line, offset, false);
            }

            ShowCompletion();
            var imagePath = Path.Combine(
                _options.OutputDirectory,
                $"fecsx-completion-{scenario.Name}-{scenario.Expected}.png");
            var completion = WaitForCompletion(scenario.Expected, imagePath);
            if (!completion.Name.StartsWith(
                scenario.Expected,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The '{scenario.Name}' completion item had unexpected text "
                    + $"'{completion.Name}'.");
            }
            if (scenario.ExpectedDescriptionText != null)
            {
                VisualStudioAutomation.Select(completion);
                WaitForVisibleText(scenario.ExpectedDescriptionText, imagePath);
            }
            CaptureEvidence(imagePath, scenario.Name);
            Console.WriteLine(
                $"Verified '{scenario.Expected}' in the '{scenario.Name}' popup "
                + $"and captured '{imagePath}'.");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
        }

        private void VerifyTailwindSessionRetargeting()
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", string.Empty);
            _pendingUndoCount = 1;
            VisualStudioAutomation.BringToForeground(_process);
            RetryTransientCom(
                () => _dte.ActiveDocument.Activate(),
                "focus the FECSX editor for Tailwind session retargeting");
            Thread.Sleep(250);
            VisualStudioAutomation.SendText("p-");
            WaitForBuffer(
                text => GetLineText(text, 6).Contains(
                    "className=\"p- energy-section",
                    StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            var imagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-retarget-block.png");
            WaitForCompletion("p-0", imagePath);
            VisualStudioAutomation.SendBackspace();
            VisualStudioAutomation.SendBackspace();
            VisualStudioAutomation.SendText("blo");
            WaitForBuffer(
                text => GetLineText(text, 6).Contains(
                    "className=\"blo energy-section",
                    StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            var completion = WaitForCompletion("block", imagePath);
            VisualStudioAutomation.Select(completion);
            WaitForVisibleText("display: block", imagePath);
            CaptureEvidence(imagePath, "tailwind-retarget");
            Console.WriteLine(
                "Verified that one Tailwind completion session retargets from "
                + $"'p-' to 'blo' and captured '{imagePath}'.");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
        }

        private void VerifyTailwindAdvancedSyntax()
        {
            if (_options.UsesTailwindCompletionFixture)
            {
                VerifyTailwindStateVariant(
                    "tablet",
                    "tabl",
                    "tablet:",
                    "@media (width >= 50rem)");
                VerifyTailwindStateVariant(
                    "dark",
                    "dar",
                    "dark:",
                    "&:where(.dark, .dark *)");
                VerifyTailwindProjectUtility();
            }
            VerifyTailwindStateVariant(
                "hover",
                "hov",
                "hover:",
                "@media (hover: hover) { &:hover }");
            VerifyTailwindStateVariant(
                "focus",
                "foc",
                "focus:",
                "&:focus");
            VerifyTailwindUtilityFamily();
            VerifyTailwindSemanticFamilies();
            VerifyTailwindResponsiveMediaVariant();
            VerifyTailwindAdvancedScenario(
                "arbitrary-example",
                "drop-shadow-",
                "[",
                "drop-shadow-[0_35px_35px_rgba(0,0,0,0.25)]",
                "Resulting CSS value: 0 35px 35px rgba(0,0,0,0.25)");
            VerifyTailwindAdvancedScenario(
                "modifier",
                "drop-shadow-xl",
                "/",
                "drop-shadow-xl/25",
                "25%");
            VerifyTailwindAdvancedScenario(
                "arbitrary",
                "drop-shadow-",
                "[0_35px_35px_rgba(0,0,0,0.25)",
                "drop-shadow-[0_35px_35px_rgba(0,0,0,0.25)]",
                "filter: drop-shadow(0 35px 35px rgba(0,0,0,0.25));");
            VerifyTailwindAdvancedScenario(
                "custom-property",
                "drop-shadow-",
                "(--my-drop-shadow",
                "drop-shadow-(--my-drop-shadow)",
                "filter: drop-shadow(var(--my-drop-shadow));");
            VerifyTailwindParameterScenario(
                "offset-y",
                "drop-shadow-[0_|35px_35px_rgba(0,0,0,0.25)]",
                "Active parameter: offset-y");
            VerifyTailwindParameterScenario(
                "blur",
                "drop-shadow-[0_35px_|35px_rgba(0,0,0,0.25)]",
                "Active parameter: blur");
        }

        private void VerifyTailwindUtilityFamily()
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", "m");
            _pendingUndoCount = 1;

            var familyImagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-family-margin.png");
            ShowCompletion();
            var family = WaitForCompletion("m-", familyImagePath);
            VisualStudioAutomation.Select(family);
            WaitForVisibleText("Adds whitespace outside the box", familyImagePath);
            WaitForVisibleText("m-14", familyImagePath);
            WaitForVisibleText("margin: 56px", familyImagePath);
            WaitForVisibleText("m-[var(--padding)]", familyImagePath);
            CaptureEvidence(familyImagePath, "tailwind-family-margin");
            VisualStudioAutomation.CommitSelectedCompletion();
            _pendingUndoCount = 2;

            WaitForBuffer(
                text => GetLineText(text, 6).Contains(
                    "className=\"m- energy-section",
                    StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));
            var valuesImagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-family-margin-values.png");
            WaitForCompletion("m-0", valuesImagePath);
            WaitForCompletion("m-8", valuesImagePath);
            CaptureEvidence(valuesImagePath, "tailwind-family-margin-values");
            Console.WriteLine(
                "Verified the grouped 'm-' Tailwind family, its pixel-valued tooltip, "
                + $"and its real expanded source set; captured '{familyImagePath}' and '{valuesImagePath}'.");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
        }

        private void VerifyTailwindSemanticFamilies()
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", "grow");
            _pendingUndoCount = 1;

            var growImagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-family-grow.png");
            ShowCompletion();
            WaitForCompletion("grow", growImagePath);
            WaitForCompletion("grow-", growImagePath);
            CaptureEvidence(growImagePath, "tailwind-family-grow");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();

            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", "bg-a");
            _pendingUndoCount = 1;
            var backgroundImagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-family-background.png");
            ShowCompletion();
            WaitForCompletion("bg-auto", backgroundImagePath);
            var amber = WaitForCompletion("bg-amber-", backgroundImagePath);
            VisualStudioAutomation.Select(amber);
            WaitForVisibleText("Sets the element's background color", backgroundImagePath);
            WaitForVisibleText("bg-amber-50", backgroundImagePath);
            CaptureEvidence(backgroundImagePath, "tailwind-family-background");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();

            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", "bg-cl");
            _pendingUndoCount = 1;
            var backgroundClipImagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-family-background-clip.png");
            ShowCompletion();
            var clip = WaitForCompletion("bg-clip-", backgroundClipImagePath);
            VisualStudioAutomation.Select(clip);
            WaitForVisibleText("background-clip", backgroundClipImagePath);
            WaitForVisibleText("bg-clip-text", backgroundClipImagePath);
            CaptureEvidence(backgroundClipImagePath, "tailwind-family-background-clip");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();

            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", "ring");
            _pendingUndoCount = 1;
            var ringImagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-family-ring.png");
            ShowCompletion();
            var ring = WaitForCompletion("ring-", ringImagePath);
            VisualStudioAutomation.Select(ring);
            WaitForVisibleText("ring-1 → ring width: 1px", ringImagePath);
            WaitForVisibleText("ring-[13px] → ring width: 13px", ringImagePath);
            CaptureEvidence(ringImagePath, "tailwind-family-ring");
            Console.WriteLine(
                "Verified exact/default Tailwind utilities beside their value families and "
                + "distinct property-based family values; captured "
                + $"'{growImagePath}', '{backgroundImagePath}', '{backgroundClipImagePath}', "
                + $"and '{ringImagePath}'.");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
        }

        private void VerifyTailwindProjectUtility()
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", "drop-shadow-car");
            _pendingUndoCount = 1;

            var imagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-project-drop-shadow-card.png");
            ShowCompletion();
            var completion = WaitForCompletion("drop-shadow-card", imagePath);
            VisualStudioAutomation.Select(completion);
            WaitForVisibleText("12px 24px", imagePath);
            CaptureEvidence(imagePath, "tailwind-project-drop-shadow-card");
            Console.WriteLine(
                "Verified the project-defined 'drop-shadow-card' Tailwind utility "
                + $"and captured '{imagePath}'.");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
        }

        private void VerifyTailwindStateVariant(
            string name,
            string authored,
            string expectedCompletion,
            string expectedCondition)
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", authored);
            _pendingUndoCount = 1;

            var imagePath = Path.Combine(
                _options.OutputDirectory,
                $"fecsx-completion-tailwind-variant-{name}.png");
            ShowCompletion();
            var variant = WaitForCompletion(expectedCompletion, imagePath);
            VisualStudioAutomation.Select(variant);
            WaitForVisibleText(expectedCondition, imagePath);
            CaptureEvidence(imagePath, $"tailwind-variant-{name}");
            Console.WriteLine(
                $"Verified the '{expectedCompletion}' Tailwind variant and captured '{imagePath}'.");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
        }

        private void VerifyTailwindResponsiveMediaVariant()
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", "md");
            _pendingUndoCount = 1;

            var variantImagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-media-md.png");
            ShowCompletion();
            var variant = WaitForCompletion("md:", variantImagePath);
            VisualStudioAutomation.Select(variant);
            WaitForVisibleText("@media (width >= 48rem)", variantImagePath);
            CaptureEvidence(variantImagePath, "tailwind-media-md");
            VisualStudioAutomation.CommitSelectedCompletion();
            _pendingUndoCount = 2;

            WaitForBuffer(
                text => GetLineText(text, 6).Contains(
                    "className=\"md: energy-section",
                    StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            var utilityImagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-tailwind-media-md-block.png");
            WaitForCompletion("md:h-", utilityImagePath);
            VisualStudioAutomation.SendText("blo");
            WaitForBuffer(
                text => GetLineText(text, 6).Contains(
                    "className=\"md:blo energy-section",
                    StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));
            var utility = WaitForCompletion("md:block", utilityImagePath);
            VisualStudioAutomation.Select(utility);
            WaitForVisibleText("display: block", utilityImagePath);
            CaptureEvidence(utilityImagePath, "tailwind-media-md-block");
            Console.WriteLine(
                "Verified the 'md:' responsive media completion and the refreshed "
                + $"'md:block' utility source set; captured '{variantImagePath}' "
                + $"and '{utilityImagePath}'.");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
        }

        private void VerifyTailwindParameterScenario(
            string name,
            string markedToken,
            string expectedDescription)
        {
            var marker = markedToken.IndexOf('|');
            if (marker < 0)
            {
                throw new ArgumentException("The Tailwind token must contain a caret marker.", nameof(markedToken));
            }

            var token = markedToken.Remove(marker, 1);
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", token);
            _pendingUndoCount = 1;
            var tokenStart = _documentLines[5].IndexOf("focus-side-section", StringComparison.Ordinal) + 1;
            selection.MoveToLineAndOffset(6, tokenStart + marker, false);

            var imagePath = Path.Combine(
                _options.OutputDirectory,
                $"fecsx-completion-tailwind-parameter-{name}.png");
            ShowCompletion();
            var completion = WaitForCompletion(token, imagePath);
            VisualStudioAutomation.Select(completion);
            WaitForVisibleText(expectedDescription, imagePath);
            CaptureEvidence(imagePath, "tailwind-parameter-" + name);
            Console.WriteLine(
                $"Verified caret-sensitive Tailwind '{name}' help and captured "
                + $"'{imagePath}'.");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
        }

        private void VerifyTailwindAdvancedScenario(
            string name,
            string initialText,
            string dynamicText,
            string expected,
            string expectedDescription)
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, _documentLines, 6, "focus-side-section", initialText);
            _pendingUndoCount = 1;

            var imagePath = Path.Combine(
                _options.OutputDirectory,
                $"fecsx-completion-tailwind-{name}.png");
            ShowCompletion();
            WaitForCompletion("drop-shadow-xl", imagePath);
            VisualStudioAutomation.SendText(dynamicText);
            var authored = initialText + dynamicText;
            var text = WaitForBuffer(
                text => GetLineText(text, 6).Contains(
                    $"className=\"{authored} energy-section",
                    StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));
            if (!GetLineText(text, 6).Contains(
                $"className=\"{authored} energy-section",
                StringComparison.Ordinal))
            {
                throw new TimeoutException(
                    $"Typing the advanced Tailwind '{name}' suffix did not update "
                    + "the FECSX buffer within five seconds.");
            }

            var completion = WaitForCompletion(expected, imagePath);
            VisualStudioAutomation.Select(completion);
            WaitForVisibleText(expectedDescription, imagePath);
            CaptureEvidence(imagePath, "tailwind-" + name);
            Console.WriteLine(
                $"Verified advanced Tailwind '{expected}' completion and captured "
                + $"'{imagePath}'.");
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
        }

        private void VerifyInvalidMarkupCommit()
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, _documentLines, 6, "section", "Task");
            _pendingUndoCount = 1;
            ShowCompletion();
            var imagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-component-TaskCard-committed-invalid.png");
            var completion = WaitForCompletion("TaskCard", imagePath);
            VisualStudioAutomation.Select(completion);
            Thread.Sleep(250);
            VisualStudioAutomation.BringToForeground(_process);
            VisualStudioAutomation.CommitSelectedCompletion();
            _pendingUndoCount = 2;

            var currentText = WaitForBuffer(
                text => Regex.IsMatch(
                    text,
                    "<TaskCard\\s+className="),
                TimeSpan.FromSeconds(10));
            VisualStudioAutomation.SaveWindowScreenshot(_process, imagePath);
            if (!Regex.IsMatch(
                currentText,
                "<TaskCard\\s+className="))
            {
                throw new InvalidDataException(
                    "Committing 'TaskCard' did not replace the temporary markup token. "
                    + $"Diagnostic: '{imagePath}'.");
            }

            var undoTimer = Stopwatch.StartNew();
            _dte.ExecuteCommand("Edit.Undo");
            _pendingUndoCount--;
            _dte.ExecuteCommand("Edit.Undo");
            _pendingUndoCount--;
            undoTimer.Stop();
            if (undoTimer.Elapsed > TimeSpan.FromSeconds(5))
            {
                throw new TimeoutException(
                    "Undo after invalid-markup completion took "
                    + $"{undoTimer.Elapsed.TotalSeconds:0.00} seconds.");
            }
            if (!_dte.ActiveDocument.Saved)
            {
                throw new InvalidOperationException(
                    "Undo after invalid-markup completion did not restore the saved "
                    + "FECSX document.");
            }
            Console.WriteLine(
                "Verified committing markup completion in a temporarily invalid "
                + "FECSX document and undoing it in "
                + $"{undoTimer.Elapsed.TotalSeconds:0.00} seconds.");
        }

        private void VerifyImportCommit()
        {
            dynamic selection = _dte.ActiveDocument.Selection;
            const string original = "props.Store.EnergyLevel";
            const string replacement = "new ObservableCollectio<int>().Count";
            ReplaceLineText(selection, _documentLines, 13, original, replacement);
            _pendingUndoCount = 1;
            var replacementStart = _documentLines[12].IndexOf(
                original,
                StringComparison.Ordinal) + 1;
            selection.MoveToLineAndOffset(
                13,
                replacementStart + "new ObservableCollectio".Length,
                false);
            ShowCompletion();
            var imagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-import-ObservableCollection.png");
            var completion = WaitForCompletion("ObservableCollection", imagePath);
            VisualStudioAutomation.SaveWindowScreenshot(_process, imagePath);
            VisualStudioAutomation.Select(completion);
            Thread.Sleep(250);
            VisualStudioAutomation.BringToForeground(_process);
            VisualStudioAutomation.CommitSelectedCompletion();
            _pendingUndoCount = 2;
            Thread.Sleep(500);
            VisualStudioAutomation.SaveWindowScreenshot(
                _process,
                Path.Combine(
                    _options.OutputDirectory,
                    "fecsx-completion-import-after-gesture.png"));

            var currentText = WaitForBuffer(
                text => text.Contains(
                        "using System.Collections.ObjectModel;",
                        StringComparison.Ordinal)
                    && text.Contains(
                        "new ObservableCollection<int>().Count",
                        StringComparison.Ordinal),
                TimeSpan.FromSeconds(10));
            var diagnostic = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-import-after-commit.txt");
            File.WriteAllText(diagnostic, currentText);
            if (!currentText.Contains(
                "using System.Collections.ObjectModel;",
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Committing 'ObservableCollection' did not add "
                    + "'using System.Collections.ObjectModel;' to the FECSX preamble. "
                    + $"Buffer: '{diagnostic}'. Commit manager: "
                    + $"'{_completionDiagnosticPath}'.");
            }
            if (!currentText.Contains(
                "new ObservableCollection<int>().Count",
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Committing 'ObservableCollection' did not replace the authored token.");
            }
            Console.WriteLine(
                "Verified committing 'ObservableCollection' adds "
                + "'using System.Collections.ObjectModel;' and replaces the FECSX token.");
            UndoPendingChanges();
        }

        private void VerifyNavigation()
        {
            var storePath = Path.Combine(
                Directory.GetParent(
                    Directory.GetParent(_options.DocumentPath)!.FullName)!.FullName,
                "OrbitStore.cs");
            OpenDocument(_options.DocumentPath);
            _documentLines = File.ReadAllLines(_options.DocumentPath);
            var energyOffset = _documentLines[12].IndexOf(
                "EnergyLevel",
                StringComparison.Ordinal) + 2;
            if (energyOffset < 2)
            {
                throw new InvalidDataException(
                    "The FECSX navigation fixture no longer contains EnergyLevel on line 13.");
            }
            RetryTransientCom(
                () =>
                {
                    _dte.ActiveDocument.Selection.MoveToLineAndOffset(
                        13,
                        energyOffset,
                        false);
                    _dte.ExecuteCommand("Edit.GoToDefinition");
                },
                "navigate to EnergyLevel");
            WaitForLocation(storePath, 8, "OrbitStore.cs:8");

            var appDocument = Path.Combine(
                Path.GetDirectoryName(_options.DocumentPath)!,
                "App.fecsx");
            OpenDocument(appDocument);
            var appLines = File.ReadAllLines(appDocument);
            var appPropsOffset = appLines[9].IndexOf(
                "AppProps",
                StringComparison.Ordinal) + 2;
            if (appPropsOffset < 2)
            {
                throw new InvalidDataException(
                    "The FECSX navigation fixture no longer contains AppProps on line 10.");
            }
            RetryTransientCom(
                () =>
                {
                    _dte.ActiveDocument.Selection.MoveToLineAndOffset(
                        10,
                        appPropsOffset,
                        false);
                    _dte.ExecuteCommand("Edit.GoToDefinition");
                },
                "navigate to AppProps");
            WaitForLocation(appDocument, 4, "App.fecsx:4");

            OpenDocument(appDocument);
            var focusCardOffset = appLines[22].IndexOf(
                "FocusCard",
                StringComparison.Ordinal) + 2;
            if (focusCardOffset < 2)
            {
                throw new InvalidDataException(
                    "The FECSX navigation fixture no longer contains FocusCard on line 23.");
            }
            RetryTransientCom(
                () =>
                {
                    _dte.ActiveDocument.Selection.MoveToLineAndOffset(
                        23,
                        focusCardOffset,
                        false);
                    _dte.ExecuteCommand("Edit.GoToDefinition");
                },
                "navigate to the FocusCard component");
            var focusCardDocument = Path.Combine(
                Path.GetDirectoryName(_options.DocumentPath)!,
                "FocusCard.fecsx");
            WaitForLocation(focusCardDocument, 6, "FocusCard.fecsx:6");

            OpenDocument(appDocument);
            var footerOffset = appLines[39].IndexOf(
                "App.Footer",
                StringComparison.Ordinal) + 5;
            if (footerOffset < 5)
            {
                throw new InvalidDataException(
                    "The FECSX navigation fixture no longer contains App.Footer on line 40.");
            }
            RetryTransientCom(
                () =>
                {
                    _dte.ActiveDocument.Selection.MoveToLineAndOffset(
                        40,
                        footerOffset,
                        false);
                    _dte.ExecuteCommand("Edit.GoToDefinition");
                },
                "navigate to the named App.Footer component");
            WaitForLocation(appDocument, 44, "App.fecsx:44");
        }

        private void VerifyNamedComponentCompletion()
        {
            var appDocument = Path.Combine(
                Path.GetDirectoryName(_options.DocumentPath)!,
                "App.fecsx");
            OpenDocument(appDocument);
            var appLines = File.ReadAllLines(appDocument);
            dynamic selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, appLines, 35, "App.Footer", "App.");
            _pendingUndoCount = 1;
            ShowCompletion();
            var imagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-named-component-App.Footer.png");
            WaitForCompletion("App.Footer", imagePath);
            VisualStudioAutomation.SaveWindowScreenshot(_process, imagePath);
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
            Console.WriteLine(
                "Verified nested FECSX component completion exposes 'App.Footer'.");

            appLines = File.ReadAllLines(appDocument);
            selection = _dte.ActiveDocument.Selection;
            ReplaceLineText(selection, appLines, 35, "Store", "St");
            _pendingUndoCount = 1;
            ShowCompletion();
            imagePath = Path.Combine(
                _options.OutputDirectory,
                "fecsx-completion-named-component-prop-Store.png");
            WaitForCompletion("Store", imagePath);
            VisualStudioAutomation.SaveWindowScreenshot(_process, imagePath);
            VisualStudioAutomation.SendEscape();
            UndoPendingChanges();
            Console.WriteLine(
                "Verified nested FECSX component metadata exposes the 'Store' prop.");
        }

        private void OpenDocument(string path)
        {
            RetryTransientCom(
                () => _dte.ItemOperations.OpenFile(path),
                $"open '{path}'");
            VisualStudioAutomation.WaitForDocument(
                _dte,
                path,
                DateTime.UtcNow + TimeSpan.FromSeconds(30));
        }

        private static void RetryTransientCom(Action action, string description)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            Exception? lastException = null;
            do
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception exception) when (
                    VisualStudioAutomation.IsTransientComFailure(exception))
                {
                    lastException = exception;
                    Thread.Sleep(250);
                }
            }
            while (DateTime.UtcNow < deadline);

            throw new TimeoutException(
                $"Visual Studio remained busy while attempting to {description}.",
                lastException);
        }

        private void ShowCompletion()
        {
            VisualStudioAutomation.SendEscape();
            Thread.Sleep(100);
            VisualStudioAutomation.BringToForeground(_process);
            RetryTransientCom(
                () =>
                {
                    _dte.ActiveDocument.Activate();
                    _dte.ExecuteCommand("Edit.ListMembers");
                },
                "open the completion list");
        }

        private VisualStudioAutomationElement WaitForCompletion(
            string expected,
            string diagnosticImagePath)
        {
            var deadline = DateTime.UtcNow + _options.CompletionTimeout;
            var nextRefresh = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                foreach (var completion in GetVisibleMenuItems())
                {
                    if (MatchesCompletionItem(completion, expected))
                    {
                        return completion;
                    }
                }
                if (DateTime.UtcNow >= nextRefresh)
                {
                    ShowCompletion();
                    nextRefresh = DateTime.UtcNow + TimeSpan.FromSeconds(2);
                }
                Thread.Sleep(250);
            }
            VisualStudioAutomation.SaveWindowScreenshot(
                _process,
                diagnosticImagePath);
            var visible = string.Join(
                ", ",
                GetVisibleMenuItems().Select(GetElementDescription));
            throw new TimeoutException(
                $"Completion did not show '{expected}'. Visible items: {visible}. "
                + $"Diagnostic: '{diagnosticImagePath}'.");
        }

        private static bool MatchesCompletionItem(
            VisualStudioAutomationElement element,
            string expected)
        {
            string name;
            try
            {
                name = element.Name;
            }
            catch (COMException)
            {
                return false;
            }

            var automationId = string.Empty;
            try
            {
                automationId = element.AutomationId;
            }
            catch (COMException)
            {
            }
            return VisualStudioCompletionItemIdentity.Matches(
                expected,
                automationId,
                name);
        }

        private IReadOnlyList<VisualStudioAutomationElement> GetVisibleMenuItems()
        {
            var result = new List<VisualStudioAutomationElement>();
            foreach (var element in VisualStudioAutomation.FindElements(
                _process,
                VisualStudioControlType.MenuItem))
            {
                try
                {
                    if (!element.IsOffscreen)
                    {
                        result.Add(element);
                    }
                }
                catch (COMException)
                {
                }
            }
            return result;
        }

        private void WaitForVisibleText(string expected, string diagnosticImagePath)
        {
            var deadline = DateTime.UtcNow + _options.CompletionTimeout;
            do
            {
                foreach (var element in VisualStudioAutomation.FindElements(
                    _process,
                    VisualStudioControlType.Text))
                {
                    try
                    {
                        if (!element.IsOffscreen
                            && element.Name.Contains(expected, StringComparison.Ordinal))
                        {
                            return;
                        }
                    }
                    catch (COMException)
                    {
                    }
                }
                Thread.Sleep(250);
            }
            while (DateTime.UtcNow < deadline);

            VisualStudioAutomation.SaveWindowScreenshot(_process, diagnosticImagePath);
            throw new TimeoutException(
                $"Completion description did not show '{expected}'. Diagnostic: '{diagnosticImagePath}'.");
        }

        private static string GetElementDescription(VisualStudioAutomationElement element)
        {
            try
            {
                return element.Name;
            }
            catch (COMException)
            {
                return "<unavailable>";
            }
        }

        private void CaptureEvidence(string imagePath, string scenarioName)
        {
            VisualStudioAutomation.SaveWindowScreenshot(_process, imagePath);
            var length = new FileInfo(imagePath).Length;
            if (length < 10_000)
            {
                throw new InvalidDataException(
                    $"The '{scenarioName}' completion screenshot is unexpectedly "
                    + $"small ({length} bytes).");
            }
        }

        private string WaitForBuffer(
            Func<string, bool> predicate,
            TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            string text;
            do
            {
                Thread.Sleep(250);
                text = ReadActiveDocumentText();
            }
            while (!predicate(text) && DateTime.UtcNow < deadline);
            return text;
        }

        private string ReadActiveDocumentText()
        {
            dynamic textDocument = _dte.ActiveDocument.Object("TextDocument");
            return textDocument.StartPoint
                .CreateEditPoint()
                .GetText(textDocument.EndPoint);
        }

        private void WaitForLocation(
            string expectedPath,
            int expectedLine,
            string description)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            string? activePath = null;
            var activeLine = 0;
            do
            {
                Thread.Sleep(250);
                try
                {
                    activePath = _dte.ActiveDocument.FullName;
                    activeLine = _dte.ActiveDocument.Selection.ActivePoint.Line;
                }
                catch (COMException)
                {
                    activePath = null;
                    activeLine = 0;
                }
            }
            while ((!string.Equals(
                    activePath,
                    expectedPath,
                    StringComparison.OrdinalIgnoreCase)
                || activeLine != expectedLine)
                && DateTime.UtcNow < deadline);
            if (!string.Equals(
                    activePath,
                    expectedPath,
                    StringComparison.OrdinalIgnoreCase)
                || activeLine != expectedLine)
            {
                throw new InvalidDataException(
                    $"Go To Definition did not navigate to {description}; active "
                    + $"location was '{activePath}:{activeLine}'.");
            }
            Console.WriteLine(
                $"Verified Go To Definition navigated to authored '{description}'.");
        }

        private void UndoPendingChanges(bool waitForOriginal = true)
        {
            var originalText = _pendingOriginalDocumentText;
            while (_pendingUndoCount > 0)
            {
                _dte.ExecuteCommand("Edit.Undo");
                _pendingUndoCount--;
            }
            if (!waitForOriginal)
            {
                _pendingOriginalDocumentText = null;
                return;
            }
            if (originalText == null)
            {
                _pendingOriginalDocumentText = null;
                return;
            }

            // Physical keyboard probes can be split into more undo transactions
            // than the programmatic edit that began the scenario. Continue only
            // until the exact captured buffer is restored, with a strict bound.
            for (var attempt = 0;
                attempt < 10
                    && !string.Equals(
                        ReadActiveDocumentText(),
                        originalText,
                        StringComparison.Ordinal);
                attempt++)
            {
                _dte.ExecuteCommand("Edit.Undo");
                Thread.Sleep(100);
            }
            _pendingOriginalDocumentText = null;

            var text = WaitForBuffer(
                current => string.Equals(
                    current,
                    originalText,
                    StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));
            if (!string.Equals(text, originalText, StringComparison.Ordinal))
            {
                throw new TimeoutException(
                    "Undo did not restore the original FECSX buffer within five seconds.");
            }
        }

        private static int GetLineOffsetAfter(
            IReadOnlyList<string> lines,
            int line,
            string prefix)
        {
            var index = lines[line - 1].IndexOf(prefix, StringComparison.Ordinal);
            return index >= 0
                ? index + prefix.Length + 1
                : throw new InvalidDataException(
                    $"Line {line} does not contain '{prefix}'.");
        }

        private void ReplaceLineText(
            dynamic selection,
            IReadOnlyList<string> lines,
            int line,
            string oldText,
            string newText)
        {
            if (_pendingUndoCount != 0)
            {
                throw new InvalidOperationException(
                    "A new completion probe cannot begin while another edit is pending.");
            }
            _pendingOriginalDocumentText = ReadActiveDocumentText();
            var index = lines[line - 1].IndexOf(oldText, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new InvalidDataException(
                    $"Line {line} does not contain '{oldText}'.");
            }
            var start = index + 1;
            var expectedLine = lines[line - 1]
                .Remove(index, oldText.Length)
                .Insert(index, newText);
            selection.MoveToLineAndOffset(line, start, false);
            selection.MoveToLineAndOffset(line, start + oldText.Length, true);
            selection.Insert(newText);
            var text = WaitForBuffer(
                current => string.Equals(
                    GetLineText(current, line),
                    expectedLine,
                    StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));
            var updatedLine = GetLineText(text, line);
            if (!string.Equals(updatedLine, expectedLine, StringComparison.Ordinal))
            {
                throw new TimeoutException(
                    $"Line {line} did not contain the completion probe '{newText}' "
                    + $"within five seconds. Current line: '{updatedLine}'.");
            }
        }

        private static string GetLineText(string text, int line)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            return line > 0 && line <= lines.Length ? lines[line - 1] : string.Empty;
        }
    }

    private sealed class CompletionScenario
    {
        public required string Name { get; init; }
        public required string Expected { get; init; }
        public required int Line { get; init; }
        public string? CaretAfter { get; init; }
        public string? TextToReplace { get; init; }
        public string? Replacement { get; init; }
        public string? ExpectedDescriptionText { get; init; }

        public static IReadOnlyList<CompletionScenario> StandardScenarios { get; } =
        [
            new()
            {
                Name = "namespace",
                Expected = "ReactDemo",
                Line = 3,
                CaretAfter = "FrontEndCSharp.",
            },
            new()
            {
                Name = "member",
                Expected = "EnergyLevel",
                Line = 13,
                CaretAfter = "props.Store.",
            },
            new()
            {
                Name = "component",
                Expected = "TaskCard",
                Line = 6,
                TextToReplace = "section",
                Replacement = "Task",
            },
            new()
            {
                Name = "tailwind",
                Expected = "drop-shadow-xl",
                ExpectedDescriptionText = "Tailwind CSS v4.3.5",
                Line = 6,
                TextToReplace = "focus-side-section",
                Replacement = "drop-shadow-",
            },
            new()
            {
                Name = "keyword",
                Expected = "default",
                Line = 13,
                TextToReplace = "props.Store.EnergyLevel",
                Replacement = "def",
            },
        ];
    }

    private sealed class CompletionOptions
    {
        public required string DocumentPath { get; init; }
        public required string SolutionPath { get; init; }
        public required string OutputDirectory { get; init; }
        public string RootSuffix { get; set; } = "Exp";
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(180);
        public TimeSpan CompletionTimeout { get; set; } = TimeSpan.FromSeconds(20);
        public bool AcceptProjectSecurityWarnings { get; set; }
        public bool KeepOpen { get; set; }
        public bool CloseExisting { get; set; }
        public bool NavigationOnly { get; set; }
        public bool TailwindOnly { get; set; }
        public bool TailwindRetargetOnly { get; set; }
        public bool TailwindAdvancedOnly { get; set; }
        public bool UsesTailwindCompletionFixture => string.Equals(
            Path.GetFileNameWithoutExtension(SolutionPath),
            "TailwindCompletionFixture",
            StringComparison.OrdinalIgnoreCase);

        public static CompletionOptions Parse(IReadOnlyList<string> arguments)
        {
            if (arguments.Count < 3)
            {
                throw new ArgumentException(
                    "verify-completion requires <document> <solution> "
                    + "<output-directory> [options].");
            }
            var options = new CompletionOptions
            {
                DocumentPath = Path.GetFullPath(arguments[0]),
                SolutionPath = Path.GetFullPath(arguments[1]),
                OutputDirectory = Path.GetFullPath(arguments[2]),
            };
            if (!File.Exists(options.DocumentPath))
            {
                throw new FileNotFoundException(
                    "The editor document was not found.",
                    options.DocumentPath);
            }
            if (!File.Exists(options.SolutionPath))
            {
                throw new FileNotFoundException(
                    "The solution was not found.",
                    options.SolutionPath);
            }
            for (var index = 3; index < arguments.Count; index++)
            {
                var name = arguments[index];
                if (name == "--accept-project-security-warnings")
                {
                    options.AcceptProjectSecurityWarnings = true;
                    continue;
                }
                if (name == "--keep-open")
                {
                    options.KeepOpen = true;
                    continue;
                }
                if (name == "--close-existing")
                {
                    options.CloseExisting = true;
                    continue;
                }
                if (name == "--navigation-only")
                {
                    options.NavigationOnly = true;
                    continue;
                }
                if (name == "--tailwind-only")
                {
                    options.TailwindOnly = true;
                    continue;
                }
                if (name == "--tailwind-retarget-only")
                {
                    options.TailwindRetargetOnly = true;
                    continue;
                }
                if (name == "--tailwind-advanced-only")
                {
                    options.TailwindAdvancedOnly = true;
                    continue;
                }
                if (index + 1 >= arguments.Count)
                {
                    throw new ArgumentException($"Option '{name}' requires a value.");
                }
                var value = arguments[++index];
                if (name == "--root-suffix")
                {
                    options.RootSuffix = value;
                }
                else if (name == "--timeout")
                {
                    options.Timeout = TimeSpan.FromSeconds(int.Parse(value));
                }
                else if (name == "--completion-timeout")
                {
                    options.CompletionTimeout = TimeSpan.FromSeconds(int.Parse(value));
                }
                else
                {
                    throw new ArgumentException($"Unknown option '{name}'.");
                }
            }
            if (options.Timeout < TimeSpan.FromSeconds(30)
                || options.Timeout > TimeSpan.FromSeconds(300))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arguments),
                    "--timeout must be between 30 and 300 seconds.");
            }
            if (options.CompletionTimeout < TimeSpan.FromSeconds(5)
                || options.CompletionTimeout > TimeSpan.FromSeconds(60))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arguments),
                    "--completion-timeout must be between 5 and 60 seconds.");
            }
            if (new[]
                {
                    options.NavigationOnly,
                    options.TailwindOnly,
                    options.TailwindRetargetOnly,
                    options.TailwindAdvancedOnly,
                }.Count(enabled => enabled) > 1)
            {
                throw new ArgumentException(
                    "--navigation-only, --tailwind-only, and "
                    + "the focused Tailwind-only modes cannot be combined.");
            }
            return options;
        }
    }
}
