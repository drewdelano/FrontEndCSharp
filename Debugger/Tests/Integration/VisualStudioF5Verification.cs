using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;

namespace FrontEndCSharp.Debugger.Integration;

[SupportedOSPlatform("windows")]
internal static class VisualStudioF5Verification
{
    private const string DapTraceEnvironmentVariable =
        "FRONTENDCSHARP_DEBUGGER_HARNESS_TRACE";

    public static void Run(IReadOnlyList<string> arguments)
    {
        var options = F5Options.Parse(arguments);
        Directory.CreateDirectory(options.OutputDirectory);
        var existing = VisualStudioAutomation.GetHiveProcessIds(options.RootSuffix);
        if (options.CloseExisting)
        {
            VisualStudioAutomation.CloseProcesses(existing);
            existing = [];
        }
        if (existing.Count != 0)
        {
            throw new InvalidOperationException(
                $"A '{options.RootSuffix}' Visual Studio instance is already open. "
                + "Use --close-existing only when discarding that instance is intended.");
        }

        var previousTracePath = Environment.GetEnvironmentVariable(
            DapTraceEnvironmentVariable);
        Environment.SetEnvironmentVariable(
            DapTraceEnvironmentVariable,
            Path.Combine(options.OutputDirectory, "composed-dap.jsonl"));
        using var process = Launch(options);
        dynamic? dte = null;
        var breakpointState = new List<BreakpointState>();
        object? startupProjects = null;
        var scenarioBreakpointCreated = false;
        try
        {
            var deadline = DateTime.UtcNow + options.Timeout;
            dte = VisualStudioAutomation.WaitForDte(
                process,
                deadline,
                options.AcceptProjectSecurityWarnings);
            VisualStudioAutomation.WaitForSolution(
                dte,
                process,
                deadline,
                options.AcceptProjectSecurityWarnings);
            WaitForBuildReady(dte, deadline);
            startupProjects = dte.Solution.SolutionBuild.StartupProjects;
            if (options.StartupProjectPath != null)
            {
                var uniqueName = FindProjectUniqueName(
                    dte,
                    options.StartupProjectPath);
                dte.Solution.SolutionBuild.StartupProjects = new[] { uniqueName };
            }

            breakpointState.AddRange(CaptureAndDisableBreakpoints(dte));
            dte.ItemOperations.OpenFile(options.DocumentPath);
            VisualStudioAutomation.WaitForDocument(
                dte,
                options.DocumentPath,
                DateTime.UtcNow + TimeSpan.FromSeconds(30));
            dynamic selection = dte.ActiveDocument.Selection;
            selection.MoveToLineAndOffset(options.Line, 1, false);
            dynamic scenarioBreakpoint = FindBreakpoint(
                dte,
                options.DocumentPath,
                options.Line);
            if (scenarioBreakpoint == null)
            {
                dte.ExecuteCommand("Debug.ToggleBreakpoint");
                scenarioBreakpointCreated = true;
                scenarioBreakpoint = WaitForBreakpoint(
                    dte,
                    options.DocumentPath,
                    options.Line,
                    DateTime.UtcNow + TimeSpan.FromSeconds(10));
            }
            scenarioBreakpoint.Enabled = true;

            var stopwatch = Stopwatch.StartNew();
            dte.ExecuteCommand("Debug.Start");
            if (options.NavigationUrl != null)
            {
                NavigateDebuggerBrowser(options.NavigationUrl, deadline);
            }
            var stop = WaitForManagedStop(dte, options, deadline);
            stopwatch.Stop();
            var imagePath = Path.Combine(
                options.OutputDirectory,
                "visual-studio-managed-breakpoint.png");
            VisualStudioAutomation.SaveWindowScreenshot(process, imagePath);
            if (new FileInfo(imagePath).Length < 10_000)
            {
                throw new InvalidDataException(
                    "The managed-breakpoint screenshot is unexpectedly small.");
            }
            var result = new F5Result
            {
                SchemaVersion = 1,
                SolutionPath = options.SolutionPath,
                DocumentPath = stop.DocumentPath,
                Line = stop.Line,
                FunctionName = stop.FunctionName,
                RootSuffix = options.RootSuffix,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                ScreenshotPath = imagePath,
            };
            File.WriteAllText(
                Path.Combine(options.OutputDirectory, "result.json"),
                JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(
                $"Verified Visual Studio F5 stopped at '{stop.DocumentPath}:{stop.Line}' "
                + $"after {stopwatch.Elapsed.TotalSeconds:0.00} seconds.");
        }
        finally
        {
            if (dte != null)
            {
                TryStopDebugging(dte);
                RestoreBreakpoints(dte, breakpointState, scenarioBreakpointCreated, options);
                if (startupProjects != null)
                {
                    try
                    {
                        dte.Solution.SolutionBuild.StartupProjects = startupProjects;
                    }
                    catch (COMException)
                    {
                    }
                }
            }
            VisualStudioAutomation.CloseProcesses([process.Id]);
            Environment.SetEnvironmentVariable(
                DapTraceEnvironmentVariable,
                previousTracePath);
        }
    }

    private static void NavigateDebuggerBrowser(Uri destination, DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            foreach (var process in GetPipeAttachedBrowserProcesses())
            {
                var addressBar = VisualStudioAutomation.FindElement(
                    process,
                    "Address and search bar",
                    VisualStudioControlType.Edit);
                if (addressBar == null)
                {
                    continue;
                }
                VisualStudioAutomation.BringToForeground(process);
                VisualStudioAutomation.SetValue(
                    addressBar,
                    destination.AbsoluteUri);
                VisualStudioAutomation.SendEnter();
                return;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            $"A debugger-attached browser did not expose its address bar for '{destination}'.");
    }

    private static IReadOnlyList<Process> GetPipeAttachedBrowserProcesses()
    {
        var result = new List<Process>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, CommandLine FROM Win32_Process "
            + "WHERE Name = 'brave.exe' OR Name = 'chrome.exe' OR Name = 'msedge.exe'");
        foreach (var item in searcher.Get().Cast<ManagementObject>())
        {
            var commandLine = item["CommandLine"] as string;
            if (commandLine == null
                || !commandLine.Contains(
                    "--remote-debugging-pipe",
                    StringComparison.OrdinalIgnoreCase)
                || commandLine.Contains("--type=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            try
            {
                result.Add(Process.GetProcessById(Convert.ToInt32(
                    item["ProcessId"],
                    System.Globalization.CultureInfo.InvariantCulture)));
            }
            catch (ArgumentException)
            {
            }
        }
        return result.OrderByDescending(process => process.StartTime).ToArray();
    }

    private static Process Launch(F5Options options)
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

    private static void WaitForBuildReady(dynamic dte, DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((int)dte.Solution.SolutionBuild.BuildState == 1)
                {
                    return;
                }
            }
            catch (COMException)
            {
            }
            catch (Exception exception) when (
                VisualStudioAutomation.IsTransientComFailure(exception))
            {
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException("Visual Studio did not reach an idle build state.");
    }

    private static IReadOnlyList<BreakpointState> CaptureAndDisableBreakpoints(
        dynamic dte)
    {
        var result = new List<BreakpointState>();
        foreach (dynamic breakpoint in dte.Debugger.Breakpoints)
        {
            var state = new BreakpointState
            {
                File = (string)breakpoint.File,
                Line = (int)breakpoint.FileLine,
                Enabled = (bool)breakpoint.Enabled,
            };
            result.Add(state);
            breakpoint.Enabled = false;
        }
        return result;
    }

    private static dynamic? FindBreakpoint(
        dynamic dte,
        string documentPath,
        int line)
    {
        foreach (dynamic breakpoint in dte.Debugger.Breakpoints)
        {
            if ((int)breakpoint.FileLine == line
                && string.Equals(
                    (string)breakpoint.File,
                    documentPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return breakpoint;
            }
        }
        return null;
    }

    private static dynamic WaitForBreakpoint(
        dynamic dte,
        string documentPath,
        int line,
        DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            var breakpoint = FindBreakpoint(dte, documentPath, line);
            if (breakpoint != null)
            {
                return breakpoint;
            }
            Thread.Sleep(100);
        }
        throw new TimeoutException(
            $"Visual Studio did not create a breakpoint at '{documentPath}:{line}'.");
    }

    private static ManagedStop WaitForManagedStop(
        dynamic dte,
        F5Options options,
        DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((int)dte.Debugger.CurrentMode == 2)
                {
                    dynamic frame = dte.Debugger.CurrentStackFrame;
                    var path = (string)dte.ActiveDocument.FullName;
                    var line = (int)dte.ActiveDocument.Selection.ActivePoint.Line;
                    if (string.Equals(
                            path,
                            options.DocumentPath,
                            StringComparison.OrdinalIgnoreCase)
                        && line == options.Line)
                    {
                        return new ManagedStop
                        {
                            DocumentPath = path,
                            Line = line,
                            FunctionName = (string)frame.FunctionName,
                        };
                    }
                }
            }
            catch (COMException)
            {
            }
            catch (Exception exception) when (
                VisualStudioAutomation.IsTransientComFailure(exception))
            {
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            $"Visual Studio did not stop at '{options.DocumentPath}:{options.Line}'.");
    }

    private static string FindProjectUniqueName(dynamic dte, string projectPath)
    {
        foreach (dynamic project in dte.Solution.Projects)
        {
            var match = FindProjectUniqueNameInProject(project, projectPath);
            if (match != null)
            {
                return match;
            }
        }
        throw new InvalidDataException(
            $"Startup project '{projectPath}' was not found in the loaded solution.");
    }

    private static string? FindProjectUniqueNameInProject(
        dynamic project,
        string projectPath)
    {
        try
        {
            var fullName = (string)project.FullName;
            if (string.Equals(
                Path.GetFullPath(fullName),
                projectPath,
                StringComparison.OrdinalIgnoreCase))
            {
                return (string)project.UniqueName;
            }
        }
        catch (Exception exception) when (
            exception is COMException or ArgumentException)
        {
        }
        try
        {
            foreach (dynamic item in project.ProjectItems)
            {
                dynamic subProject = item.SubProject;
                if (subProject == null)
                {
                    continue;
                }
                var match = FindProjectUniqueNameInProject(subProject, projectPath);
                if (match != null)
                {
                    return match;
                }
            }
        }
        catch (COMException)
        {
        }
        return null;
    }

    private static void RestoreBreakpoints(
        dynamic dte,
        IReadOnlyList<BreakpointState> states,
        bool scenarioBreakpointCreated,
        F5Options options)
    {
        try
        {
            if (scenarioBreakpointCreated)
            {
                dynamic? breakpoint = FindBreakpoint(
                    dte,
                    options.DocumentPath,
                    options.Line);
                breakpoint?.Delete();
            }
            foreach (var state in states)
            {
                dynamic? breakpoint = FindBreakpoint(dte, state.File, state.Line);
                if (breakpoint != null)
                {
                    breakpoint.Enabled = state.Enabled;
                }
            }
        }
        catch (COMException)
        {
        }
    }

    private static void TryStopDebugging(dynamic dte)
    {
        try
        {
            if ((int)dte.Debugger.CurrentMode != 1)
            {
                dte.ExecuteCommand("Debug.StopDebugging");
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
                while ((int)dte.Debugger.CurrentMode != 1 && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(250);
                }
            }
        }
        catch (COMException)
        {
        }
    }

    private sealed class BreakpointState
    {
        public required string File { get; init; }
        public required int Line { get; init; }
        public required bool Enabled { get; init; }
    }

    private sealed class ManagedStop
    {
        public required string DocumentPath { get; init; }
        public required int Line { get; init; }
        public required string FunctionName { get; init; }
    }

    private sealed class F5Result
    {
        public required int SchemaVersion { get; init; }
        public required string SolutionPath { get; init; }
        public required string DocumentPath { get; init; }
        public required int Line { get; init; }
        public required string FunctionName { get; init; }
        public required string RootSuffix { get; init; }
        public required double ElapsedMilliseconds { get; init; }
        public required string ScreenshotPath { get; init; }
    }

    private sealed class F5Options
    {
        public required string SolutionPath { get; init; }
        public required string DocumentPath { get; init; }
        public required int Line { get; init; }
        public required string OutputDirectory { get; init; }
        public string? StartupProjectPath { get; set; }
        public Uri? NavigationUrl { get; set; }
        public string RootSuffix { get; set; } = "Exp";
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(180);
        public bool AcceptProjectSecurityWarnings { get; set; }
        public bool CloseExisting { get; set; }

        public static F5Options Parse(IReadOnlyList<string> arguments)
        {
            if (arguments.Count < 4 || !int.TryParse(arguments[2], out var line))
            {
                throw new ArgumentException(
                    "verify-visual-studio-f5 requires <solution> <document> <line> "
                    + "<output-directory> [options].");
            }
            var options = new F5Options
            {
                SolutionPath = Path.GetFullPath(arguments[0]),
                DocumentPath = Path.GetFullPath(arguments[1]),
                Line = line,
                OutputDirectory = Path.GetFullPath(arguments[3]),
            };
            if (!File.Exists(options.SolutionPath))
            {
                throw new FileNotFoundException("The solution was not found.", options.SolutionPath);
            }
            if (!File.Exists(options.DocumentPath))
            {
                throw new FileNotFoundException("The source document was not found.", options.DocumentPath);
            }
            if (options.Line <= 0 || options.Line > File.ReadLines(options.DocumentPath).Count())
            {
                throw new ArgumentOutOfRangeException(nameof(arguments), "The line is outside the document.");
            }
            for (var index = 4; index < arguments.Count; index++)
            {
                var name = arguments[index];
                if (name == "--accept-project-security-warnings")
                {
                    options.AcceptProjectSecurityWarnings = true;
                    continue;
                }
                if (name == "--close-existing")
                {
                    options.CloseExisting = true;
                    continue;
                }
                if (index + 1 >= arguments.Count)
                {
                    throw new ArgumentException($"Option '{name}' requires a value.");
                }
                var value = arguments[++index];
                if (name == "--startup-project")
                {
                    options.StartupProjectPath = Path.GetFullPath(value);
                }
                else if (name == "--navigation-url")
                {
                    options.NavigationUrl = new Uri(value, UriKind.Absolute);
                    if (options.NavigationUrl.Scheme != Uri.UriSchemeHttp
                        && options.NavigationUrl.Scheme != Uri.UriSchemeHttps)
                    {
                        throw new ArgumentException(
                            "--navigation-url must use HTTP or HTTPS.");
                    }
                }
                else if (name == "--root-suffix")
                {
                    options.RootSuffix = value;
                }
                else if (name == "--timeout")
                {
                    options.Timeout = TimeSpan.FromSeconds(int.Parse(value));
                }
                else
                {
                    throw new ArgumentException($"Unknown option '{name}'.");
                }
            }
            if (options.StartupProjectPath != null
                && !File.Exists(options.StartupProjectPath))
            {
                throw new FileNotFoundException(
                    "The startup project was not found.",
                    options.StartupProjectPath);
            }
            if (options.Timeout < TimeSpan.FromSeconds(30)
                || options.Timeout > TimeSpan.FromMinutes(10))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arguments),
                    "--timeout must be between 30 and 600 seconds.");
            }
            return options;
        }
    }
}
