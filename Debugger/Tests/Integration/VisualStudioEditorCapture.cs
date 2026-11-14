using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace FrontEndCSharp.Debugger.Integration;

[SupportedOSPlatform("windows")]
internal static class VisualStudioEditorCapture
{
    public static void Capture(IReadOnlyList<string> arguments) =>
        Capture(EditorCaptureOptions.Parse(arguments));

    public static void VerifySemanticClassification(IReadOnlyList<string> arguments)
    {
        var options = SemanticCaptureOptions.Parse(arguments);
        var outputDirectory = Path.GetDirectoryName(options.OutputPath)!;
        Directory.CreateDirectory(outputDirectory);
        var classificationLog = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(options.OutputPath) + ".classification.log");
        if (File.Exists(classificationLog))
        {
            File.Delete(classificationLog);
        }
        Environment.SetEnvironmentVariable(
            "FRONTENDCSHARP_FECSX_CLASSIFICATION_LOG",
            classificationLog);
        var lookaheads = string.Concat(options.ExpectedTypeNames.Select(
            name => $@"(?=[^\r\n]*\b{Regex.Escape(name)}\b)"));
        try
        {
            Capture(new EditorCaptureOptions
            {
                DocumentPath = options.DocumentPath,
                OutputPath = options.OutputPath,
                SolutionPath = options.SolutionPath,
                RootSuffix = options.RootSuffix,
                Timeout = options.Timeout,
                RenderDelay = TimeSpan.FromSeconds(2),
                ReadinessLogPath = classificationLog,
                ReadinessPattern = "Classification complete: semantic=True" + lookaheads,
                EditLine = options.EditLine,
                CloseExisting = options.CloseExisting,
                AcceptProjectSecurityWarnings = options.AcceptProjectSecurityWarnings,
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "FRONTENDCSHARP_FECSX_CLASSIFICATION_LOG",
                null);
        }
        var log = File.ReadAllText(classificationLog);
        if (!Regex.IsMatch(
            log,
            "Classification complete: semantic=True.*project=.+",
            RegexOptions.Multiline))
        {
            throw new InvalidDataException(
                "The capture completed without project-aware semantic classification.");
        }
        var diagnosticMatches = Regex.Matches(
            log,
            @"Classification complete: semantic=True[^\r\n]*diagnostics=(?<ids>[^\r\n]*), types=",
            RegexOptions.Multiline);
        if (diagnosticMatches.Count == 0)
        {
            throw new InvalidDataException(
                "The semantic classifier did not report projected-document diagnostics.");
        }
        var diagnosticIds = diagnosticMatches
            .Cast<Match>()
            .SelectMany(match => match.Groups["ids"].Value.Split(
                [','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var diagnosticId in options.ForbiddenDiagnosticIds)
        {
            if (diagnosticIds.Contains(diagnosticId))
            {
                throw new InvalidDataException(
                    $"The live FECSX projection reported forbidden diagnostic '{diagnosticId}'.");
            }
        }
        foreach (var typeName in options.ExpectedTypeNames)
        {
            if (!Regex.IsMatch(
                log,
                $@"types=[^\r\n]*\b{Regex.Escape(typeName)}\b"))
            {
                throw new InvalidDataException(
                    $"The semantic classifier did not render expected type '{typeName}'.");
            }
        }
        Console.WriteLine(
            $"Verified project-aware FECSX type classification in '{options.OutputPath}'.");
    }

    public static void VerifyTextMateScopes(IReadOnlyList<string> arguments)
    {
        var options = TextMateCaptureOptions.Parse(arguments);
        var outputDirectory = Path.GetDirectoryName(options.OutputPath)!;
        Directory.CreateDirectory(outputDirectory);
        var scopeLog = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(options.OutputPath) + ".textmate.log");
        if (File.Exists(scopeLog))
        {
            File.Delete(scopeLog);
        }

        Environment.SetEnvironmentVariable(
            "FRONTENDCSHARP_FECSX_TEXTMATE_LOG",
            scopeLog);
        try
        {
            Capture(new EditorCaptureOptions
            {
                DocumentPath = options.DocumentPath,
                OutputPath = options.OutputPath,
                RootSuffix = options.RootSuffix,
                Timeout = options.Timeout,
                RenderDelay = TimeSpan.Zero,
                ReadinessLogPath = scopeLog,
                ReadinessPattern = "TextMateScopeCaptureComplete",
                CloseExisting = options.CloseExisting,
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "FRONTENDCSHARP_FECSX_TEXTMATE_LOG",
                null);
        }

        VerifyTextMateScopeLog(options.DocumentPath, scopeLog);
        Console.WriteLine(
            $"Verified actual Visual Studio TextMate scopes for '{options.DocumentPath}'.");
    }

    private static void VerifyTextMateScopeLog(string documentPath, string scopeLog)
    {
        var source = File.ReadAllText(documentPath);
        var records = File.ReadLines(scopeLog)
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length == 4 && parts[0] == "TextMateScope")
            .Select(parts => new TextMateScopeRecord(
                int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                parts[3]))
            .ToArray();
        var classifications = File.ReadLines(scopeLog)
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length == 4 && parts[0] == "Classification")
            .Select(parts => new EditorClassificationRecord(
                int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                parts[3]))
            .ToArray();
        if (records.Length == 0)
        {
            throw new InvalidDataException("Visual Studio reported no TextMate scope tags.");
        }
        if (classifications.Length == 0)
        {
            throw new InvalidDataException("Visual Studio reported no editor classification tags.");
        }

        const string attributeScope = "entity.other.attribute-name.fecsx";
        var fileName = Path.GetFileName(documentPath);
        if (fileName.Equals("WeatherPanel.fecsx", StringComparison.OrdinalIgnoreCase))
        {
            RequireEveryTokenScope(source, records, "className", attributeScope);
            RequireEveryTokenLacksScope(source, records, "props", "string.");
            RequireEquivalentClassifications(
                source,
                classifications,
                FindTokenAfter(source, "props", "$\"panel side-panel"),
                FindTokenAfter(source, "props", "<strong>"));
            RequireEquivalentClassifications(
                source,
                classifications,
                FindTokenAfter(source, "WeatherFailed", "$\"panel side-panel"),
                FindTokenAfter(source, "WeatherFailed", "<strong>"));
        }
        else if (fileName.Equals("EnergyPanel.fecsx", StringComparison.OrdinalIgnoreCase))
        {
            RequireEveryTokenScope(source, records, "className", attributeScope);
            RequireEveryTokenScope(source, records, "type", attributeScope);
            RequireEveryTokenScope(source, records, "onClick", attributeScope);
            RequireEveryTokenLacksScope(source, records, "props", "string.");
            RequireEquivalentClassifications(
                source,
                classifications,
                FindTokenAfter(source, "props", "className={$\"energy-button"),
                FindTokenAfter(source, "props", "onClick={_ =>"));
        }
        else
        {
            throw new ArgumentException(
                "The FECSX TextMate scope verifier supports WeatherPanel.fecsx and EnergyPanel.fecsx.");
        }
    }

    private static void RequireEveryTokenScope(
        string source,
        IReadOnlyList<TextMateScopeRecord> records,
        string token,
        string requiredScope)
    {
        var positions = new List<int>();
        for (var position = 0;
             (position = source.IndexOf(token, position, StringComparison.Ordinal)) >= 0;
             position += token.Length)
        {
            positions.Add(position);
        }
        if (positions.Count == 0)
        {
            throw new InvalidDataException($"The fixture contains no '{token}' token.");
        }

        foreach (var position in positions)
        {
            var scopes = records
                .Where(record => record.Start <= position && position < record.Start + record.Length)
                .Select(record => record.ScopeName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (!scopes.Any(scope => scope.Contains(requiredScope, StringComparison.Ordinal)))
            {
                var line = source.Take(position).Count(character => character == '\n') + 1;
                throw new InvalidDataException(
                    $"'{token}' on line {line} lacks TextMate scope '{requiredScope}'. "
                    + $"Actual scopes: {string.Join(" | ", scopes.DefaultIfEmpty("<none>"))}");
            }
        }
    }

    private static void RequireEveryTokenLacksScope(
        string source,
        IReadOnlyList<TextMateScopeRecord> records,
        string token,
        string forbiddenScopePrefix)
    {
        var positions = new List<int>();
        for (var position = 0;
             (position = source.IndexOf(token, position, StringComparison.Ordinal)) >= 0;
             position += token.Length)
        {
            positions.Add(position);
        }
        if (positions.Count == 0)
        {
            throw new InvalidDataException($"The fixture contains no '{token}' token.");
        }

        foreach (var position in positions)
        {
            var forbiddenScopes = records
                .Where(record => record.Start <= position && position < record.Start + record.Length)
                .Select(record => record.ScopeName)
                .Where(scope => scope.StartsWith(forbiddenScopePrefix, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (forbiddenScopes.Length != 0)
            {
                var line = source.Take(position).Count(character => character == '\n') + 1;
                throw new InvalidDataException(
                    $"'{token}' on line {line} is incorrectly covered by TextMate string scope(s): "
                    + string.Join(" | ", forbiddenScopes));
            }
        }
    }

    private static int FindTokenAfter(string source, string token, string anchor)
    {
        var anchorPosition = source.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorPosition < 0)
        {
            throw new InvalidDataException($"The fixture contains no '{anchor}' anchor.");
        }
        var position = source.IndexOf(token, anchorPosition + anchor.Length, StringComparison.Ordinal);
        return position >= 0
            ? position
            : throw new InvalidDataException($"The fixture contains no '{token}' after '{anchor}'.");
    }

    private static void RequireEquivalentClassifications(
        string source,
        IReadOnlyList<EditorClassificationRecord> records,
        int actualPosition,
        int baselinePosition)
    {
        string[] At(int position) => records
            .Where(record => record.Start <= position && position < record.Start + record.Length)
            .Select(record => record.Classification)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(classification => classification, StringComparer.Ordinal)
            .ToArray();

        var actual = At(actualPosition);
        var baseline = At(baselinePosition);
        if (actual.Length == 0 || baseline.Length == 0)
        {
            throw new InvalidDataException(
                "Visual Studio returned no classification at an interpolation comparison point.");
        }
        if (!actual.SequenceEqual(baseline, StringComparer.Ordinal))
        {
            var actualLine = source.Take(actualPosition).Count(character => character == '\n') + 1;
            var baselineLine = source.Take(baselinePosition).Count(character => character == '\n') + 1;
            throw new InvalidDataException(
                $"Editor classifications differ between interpolation line {actualLine} "
                + $"({string.Join(" | ", actual)}) and baseline line {baselineLine} "
                + $"({string.Join(" | ", baseline)}).");
        }
    }

    private static void Capture(EditorCaptureOptions options)
    {
        var document = Path.GetFullPath(options.DocumentPath);
        var output = Path.GetFullPath(options.OutputPath);
        if (!File.Exists(document))
        {
            throw new FileNotFoundException("The editor document was not found.", document);
        }
        var solution = options.SolutionPath == null
            ? null
            : Path.GetFullPath(options.SolutionPath);
        if (solution != null && !File.Exists(solution))
        {
            throw new FileNotFoundException("The solution was not found.", solution);
        }
        var devenv = FindDevenv();
        var fileName = Path.GetFileName(document);
        if (options.CloseExisting)
        {
            CloseProcesses(GetHiveProcessIds(options.RootSuffix));
        }
        var deadline = DateTime.UtcNow + options.Timeout;
        Process? launchedProcess = null;
        dynamic? dte = null;
        Bitmap? bitmap = null;
        var completed = false;
        try
        {
            var editor = FindEditorProcess(options.RootSuffix, fileName, null);
            if (editor == null)
            {
                launchedProcess = Launch(devenv, options.RootSuffix, document, solution);
                if (solution != null)
                {
                    dte = VisualStudioAutomation.WaitForDte(
                        launchedProcess,
                        deadline,
                        options.AcceptProjectSecurityWarnings);
                    VisualStudioAutomation.WaitForSolution(
                        dte,
                        launchedProcess,
                        deadline,
                        options.AcceptProjectSecurityWarnings);
                    dte.ItemOperations.OpenFile(document);
                    VisualStudioAutomation.WaitForDocument(
                        dte,
                        document,
                        deadline);
                }
            }
            editor = WaitForEditor(
                options.RootSuffix,
                fileName,
                launchedProcess?.Id,
                deadline);
            if (options.EditLine is int editLine)
            {
                if (dte == null)
                {
                    dte = VisualStudioAutomation.WaitForDte(
                        editor,
                        deadline,
                        options.AcceptProjectSecurityWarnings);
                    if (solution != null)
                    {
                        VisualStudioAutomation.WaitForSolution(
                            dte,
                            editor,
                            deadline,
                            options.AcceptProjectSecurityWarnings);
                    }
                    dte.ItemOperations.OpenFile(document);
                    VisualStudioAutomation.WaitForDocument(dte, document, deadline);
                }

                WaitForReadiness(options, deadline);
                if (!string.IsNullOrWhiteSpace(options.ReadinessLogPath)
                    && File.Exists(options.ReadinessLogPath))
                {
                    File.Delete(options.ReadinessLogPath);
                }
                dte.ActiveDocument.Selection.MoveToLineAndOffset(editLine, 1, false);
                dte.ActiveDocument.Selection.Insert(Environment.NewLine);
            }
            Thread.Sleep(options.RenderDelay);
            WaitForReadiness(options, deadline);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var diagnostic = Path.Combine(
                Path.GetDirectoryName(output)!,
                Path.GetFileNameWithoutExtension(output) + ".diagnostic.png");
            var editorVisible = false;
            do
            {
                bitmap?.Dispose();
                bitmap = null;
                editor = FindEditorProcess(
                    options.RootSuffix,
                    fileName,
                    launchedProcess?.Id);
                if (editor == null)
                {
                    Thread.Sleep(250);
                    continue;
                }
                if (IsIconic(editor.MainWindowHandle))
                {
                    throw new InvalidOperationException(
                        $"The Visual Studio window for '{fileName}' is minimized.");
                }
                if (!GetWindowRect(editor.MainWindowHandle, out var rectangle))
                {
                    Thread.Sleep(250);
                    continue;
                }
                var width = rectangle.Right - rectangle.Left;
                var height = rectangle.Bottom - rectangle.Top;
                if (width < 400 || height < 300)
                {
                    Thread.Sleep(250);
                    continue;
                }
                bitmap = CaptureWindow(editor.MainWindowHandle, width, height);
                editorVisible = HasVisibleEditorContent(bitmap);
                if (!editorVisible)
                {
                    Thread.Sleep(1000);
                }
            }
            while (!editorVisible && DateTime.UtcNow < deadline);
            if (!editorVisible || bitmap == null)
            {
                bitmap?.Save(diagnostic, ImageFormat.Png);
                throw new TimeoutException(
                    $"The editor for '{fileName}' did not render visible content. "
                    + $"The final frame is '{diagnostic}'.");
            }
            bitmap.Save(output, ImageFormat.Png);
            if (new FileInfo(output).Length < 10_000)
            {
                throw new InvalidDataException("The captured image is unexpectedly small.");
            }
            Console.WriteLine(
                $"Captured '{fileName}' from Visual Studio {options.RootSuffix} to '{output}'.");
            completed = true;
        }
        finally
        {
            bitmap?.Dispose();
            if (options.EditLine.HasValue && dte != null)
            {
                try
                {
                    dte!.ExecuteCommand("Edit.Undo");
                }
                catch (COMException)
                {
                }
            }
            if ((!options.KeepOpen || !completed) && launchedProcess != null)
            {
                CloseProcesses([launchedProcess.Id]);
            }
        }
    }

    private static Process Launch(
        string devenv,
        string rootSuffix,
        string document,
        string? solution)
    {
        var start = new ProcessStartInfo
        {
            FileName = devenv,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("/RootSuffix");
        start.ArgumentList.Add(rootSuffix);
        if (solution == null)
        {
            start.ArgumentList.Add(document);
        }
        else
        {
            start.ArgumentList.Add(solution);
        }
        return Process.Start(start)
            ?? throw new InvalidOperationException("Visual Studio did not start.");
    }

    private static Process WaitForEditor(
        string rootSuffix,
        string fileName,
        int? launchedProcessId,
        DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            var process = FindEditorProcess(rootSuffix, fileName, launchedProcessId);
            if (process != null)
            {
                return process;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException($"Visual Studio did not open '{fileName}'.");
    }

    private static Process? FindEditorProcess(
        string rootSuffix,
        string fileName,
        int? launchedProcessId)
    {
        if (launchedProcessId.HasValue)
        {
            var launched = TryGetProcess(launchedProcessId.Value);
            if (launched != null && launched.MainWindowHandle != IntPtr.Zero)
            {
                return launched;
            }
        }
        return GetHiveProcessIds(rootSuffix)
            .Select(TryGetProcess)
            .Where(process => process != null
                && process.MainWindowHandle != IntPtr.Zero
                && process.MainWindowTitle.StartsWith(
                    fileName,
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(process => process!.StartTime)
            .FirstOrDefault();
    }

    private static IReadOnlyList<int> GetHiveProcessIds(string rootSuffix)
    {
        var result = new List<int>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'devenv.exe'");
        foreach (var item in searcher.Get().Cast<ManagementObject>())
        {
            var commandLine = item["CommandLine"] as string;
            if (commandLine != null
                && Regex.IsMatch(
                    commandLine,
                    @"(?i)/RootSuffix\s+" + Regex.Escape(rootSuffix) + @"(?:\s|$)"))
            {
                result.Add(Convert.ToInt32(item["ProcessId"], System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        return result;
    }

    private static Process? TryGetProcess(int processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void CloseProcesses(IEnumerable<int> processIds)
    {
        var processes = processIds
            .Distinct()
            .Select(TryGetProcess)
            .Where(process => process != null)
            .Cast<Process>()
            .ToArray();
        foreach (var process in processes)
        {
            process.CloseMainWindow();
        }
        Thread.Sleep(TimeSpan.FromSeconds(3));
        foreach (var process in processes)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            process.WaitForExit();
            process.Dispose();
        }
    }

    private static void WaitForReadiness(EditorCaptureOptions options, DateTime deadline)
    {
        if (string.IsNullOrWhiteSpace(options.ReadinessPattern))
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(options.ReadinessLogPath))
        {
            throw new ArgumentException(
                "--readiness-log is required with --readiness-pattern.");
        }
        var expression = new Regex(options.ReadinessPattern);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(options.ReadinessLogPath)
                && expression.IsMatch(File.ReadAllText(options.ReadinessLogPath)))
            {
                return;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            $"The editor did not satisfy readiness pattern '{options.ReadinessPattern}'.");
    }

    private static string FindDevenv()
    {
        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer",
            "vswhere.exe");
        var installation = BoundedProcess.Run(
            vswhere,
            RepositoryLayout.Root,
            TimeSpan.FromSeconds(30),
            "-latest",
            "-products",
            "*",
            "-property",
            "installationPath").StandardOutput.Trim();
        var devenv = Path.Combine(installation, "Common7", "IDE", "devenv.exe");
        return File.Exists(devenv)
            ? devenv
            : throw new FileNotFoundException("devenv.exe was not found.", devenv);
    }

    private static Bitmap CaptureWindow(IntPtr handle, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var target = graphics.GetHdc();
        try
        {
            if (!PrintWindow(handle, target, 2))
            {
                bitmap.Dispose();
                throw new InvalidOperationException("Windows could not capture Visual Studio.");
            }
        }
        finally
        {
            graphics.ReleaseHdc(target);
        }
        return bitmap;
    }

    private static bool HasVisibleEditorContent(Bitmap bitmap)
    {
        var right = Math.Min(bitmap.Width - 1, (int)(bitmap.Width * 0.70));
        var bottom = Math.Min(bitmap.Height - 1, (int)(bitmap.Height * 0.65));
        var visibleSamples = 0;
        for (var y = 100; y <= bottom; y += 3)
        {
            for (var x = 12; x <= right; x += 3)
            {
                var pixel = bitmap.GetPixel(x, y);
                var maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                if ((maximum >= 90 || maximum - minimum >= 30)
                    && ++visibleSamples >= 150)
                {
                    return true;
                }
            }
        }
        return false;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr window, IntPtr target, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private sealed class EditorCaptureOptions
    {
        public required string DocumentPath { get; init; }
        public required string OutputPath { get; init; }
        public string? SolutionPath { get; init; }
        public string RootSuffix { get; init; } = "Exp";
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
        public TimeSpan RenderDelay { get; init; } = TimeSpan.FromSeconds(2);
        public string? ReadinessLogPath { get; init; }
        public string? ReadinessPattern { get; init; }
        public int? EditLine { get; init; }
        public bool KeepOpen { get; init; }
        public bool CloseExisting { get; init; }
        public bool AcceptProjectSecurityWarnings { get; init; }

        public static EditorCaptureOptions Parse(IReadOnlyList<string> arguments)
        {
            if (arguments.Count < 2)
            {
                throw new ArgumentException(
                    "capture-editor-window requires <document> <output> [options].");
            }
            var values = ParseOptions(arguments.Skip(2).ToArray());
            return new EditorCaptureOptions
            {
                DocumentPath = arguments[0],
                OutputPath = arguments[1],
                SolutionPath = values.Value("--solution"),
                RootSuffix = values.Value("--root-suffix") ?? "Exp",
                Timeout = TimeSpan.FromSeconds(values.Integer("--timeout", 30)),
                RenderDelay = TimeSpan.FromSeconds(values.Integer("--render-delay", 2)),
                ReadinessLogPath = values.Value("--readiness-log"),
                ReadinessPattern = values.Value("--readiness-pattern"),
                KeepOpen = values.Flag("--keep-open"),
                CloseExisting = values.Flag("--close-existing"),
                AcceptProjectSecurityWarnings = values.Flag("--accept-project-security-warnings"),
            };
        }
    }

    private sealed class SemanticCaptureOptions
    {
        public required string DocumentPath { get; init; }
        public required string SolutionPath { get; init; }
        public required string OutputPath { get; init; }
        public required IReadOnlyList<string> ExpectedTypeNames { get; init; }
        public required IReadOnlyList<string> ForbiddenDiagnosticIds { get; init; }
        public string RootSuffix { get; init; } = "Exp";
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(120);
        public bool CloseExisting { get; init; }
        public bool AcceptProjectSecurityWarnings { get; init; }
        public int? EditLine { get; init; }

        public static SemanticCaptureOptions Parse(IReadOnlyList<string> arguments)
        {
            if (arguments.Count < 3)
            {
                throw new ArgumentException(
                    "verify-semantic-classification requires <document> <solution> <output> [options].");
            }
            var values = ParseOptions(arguments.Skip(3).ToArray());
            var expected = values.Values("--expected-type");
            var forbidden = values.Values("--forbidden-diagnostic");
            var editLine = values.Value("--edit-line") is { } editLineText
                ? int.Parse(editLineText)
                : (int?)null;
            if (editLine is <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arguments),
                    "--edit-line must be greater than zero.");
            }
            return new SemanticCaptureOptions
            {
                DocumentPath = Path.GetFullPath(arguments[0]),
                SolutionPath = Path.GetFullPath(arguments[1]),
                OutputPath = Path.GetFullPath(arguments[2]),
                ExpectedTypeNames = expected.Count == 0
                    ? ["ReactNode", "OrbitStore"]
                    : expected,
                ForbiddenDiagnosticIds = forbidden.Count == 0
                    ? ["CS0229", "CS0119"]
                    : forbidden,
                RootSuffix = values.Value("--root-suffix") ?? "Exp",
                Timeout = TimeSpan.FromSeconds(values.Integer("--timeout", 120)),
                CloseExisting = values.Flag("--close-existing"),
                AcceptProjectSecurityWarnings = values.Flag("--accept-project-security-warnings"),
                EditLine = editLine,
            };
        }
    }

    private sealed class TextMateCaptureOptions
    {
        public required string DocumentPath { get; init; }
        public required string OutputPath { get; init; }
        public string RootSuffix { get; init; } = "Exp";
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
        public bool CloseExisting { get; init; }

        public static TextMateCaptureOptions Parse(IReadOnlyList<string> arguments)
        {
            if (arguments.Count < 2)
            {
                throw new ArgumentException(
                    "verify-textmate-scopes requires <document> <output> [options].");
            }
            var values = ParseOptions(arguments.Skip(2).ToArray());
            return new TextMateCaptureOptions
            {
                DocumentPath = Path.GetFullPath(arguments[0]),
                OutputPath = Path.GetFullPath(arguments[1]),
                RootSuffix = values.Value("--root-suffix") ?? "Exp",
                Timeout = TimeSpan.FromSeconds(values.Integer("--timeout", 60)),
                CloseExisting = values.Flag("--close-existing"),
            };
        }
    }

    private sealed record TextMateScopeRecord(int Start, int Length, string ScopeName);
    private sealed record EditorClassificationRecord(int Start, int Length, string Classification);

    private static ParsedOptions ParseOptions(IReadOnlyList<string> arguments)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index++)
        {
            var option = arguments[index];
            if (option is "--keep-open" or "--close-existing" or "--accept-project-security-warnings")
            {
                flags.Add(option);
                continue;
            }
            if (index + 1 >= arguments.Count)
            {
                throw new ArgumentException($"Option '{option}' requires a value.");
            }
            if (!values.TryGetValue(option, out var items))
            {
                items = [];
                values.Add(option, items);
            }
            items.Add(arguments[++index]);
        }
        return new ParsedOptions(values, flags);
    }

    private sealed class ParsedOptions(
        IReadOnlyDictionary<string, List<string>> values,
        IReadOnlySet<string> flags)
    {
        public bool Flag(string name) => flags.Contains(name);
        public string? Value(string name) =>
            values.TryGetValue(name, out var items) ? items[^1] : null;
        public IReadOnlyList<string> Values(string name) =>
            values.TryGetValue(name, out var items) ? items : [];
        public int Integer(string name, int defaultValue) =>
            Value(name) is { } text
            && int.TryParse(text, out var result)
                ? result
                : defaultValue;
    }
}
