using EnvDTE;
using EnvDTE80;
using FrontEndCSharp.Debugging;
using FrontEndCSharp.Packaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FrontEndCSharp.VisualStudio;

internal sealed class DebugWithFrontEndCSharpCommand : IDisposable
{
    internal const int CommandId = 0x0200;
    internal const int ApplyCodeChangesCommandId = 0x0201;
    internal static readonly Guid CommandSet = new("39566da9-49a4-4c75-b5f0-1c97ad099c45");

    private const string DebugEngineGuid = "9e606f29-a5d4-4ea6-879b-358b21626eef";
    private const uint CreateProcessDebugLaunchOperation = 1;
    private const string ManifestSuffix = ".frontendcsharp.debug.json";
    private const int DebuggerRendezvousTimeoutMilliseconds = 10_000;
    private static readonly Guid OutputPaneGuid = new("48d9b5fc-1ff6-4a60-a18a-9798d76e7781");

    private readonly FrontEndCSharpPackage package;
    private readonly DTE2 dte;
    private readonly VisualStudioDebuggerOutput output;
    private readonly IVsDebugger4 debugger;
    private readonly DebuggerEvents debuggerEvents;
    private readonly VisualStudioF5CompatibilityController f5Compatibility;
    private readonly FrontEndCSharpEditAndContinueController editAndContinue;
    private readonly VisualStudioWebHost webHost;

    private DebugWithFrontEndCSharpCommand(
        FrontEndCSharpPackage package,
        DTE2 dte,
        IVsOutputWindowPane output,
        OleMenuCommandService commandService,
        IVsDebugger4 debugger,
        IVsRegisterPriorityCommandTarget priorityCommandTarget)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        this.package = package;
        this.dte = dte;
        this.output = new VisualStudioDebuggerOutput(output);
        this.debugger = debugger;
        webHost = new VisualStudioWebHost(package, this.output);
        editAndContinue = new FrontEndCSharpEditAndContinueController(package, dte, this.output);
        debuggerEvents = dte.Events.DebuggerEvents;
        debuggerEvents.OnEnterDesignMode += OnEnterDesignMode;
        commandService.AddCommand(new OleMenuCommand(Execute, new CommandID(CommandSet, CommandId)));
        commandService.AddCommand(new OleMenuCommand(
            ExecuteApplyCodeChanges,
            new CommandID(CommandSet, ApplyCodeChangesCommandId)));
        f5Compatibility = new VisualStudioF5CompatibilityController(
            dte,
            this.output,
            priorityCommandTarget);
    }

    private void OnEnterDesignMode(dbgEventReason reason)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        f5Compatibility.RestoreBrowserSelection();
        webHost.Stop();
        editAndContinue.Reset();
    }

    internal static async Task<DebugWithFrontEndCSharpCommand?> InitializeAsync(
        FrontEndCSharpPackage package,
        CancellationToken cancellationToken)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await package.GetServiceAsync(typeof(SDTE)) as DTE2;
        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService))
            as OleMenuCommandService;
        var outputWindow = await package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
        var debugger = await package.GetServiceAsync(typeof(SVsShellDebugger)) as IVsDebugger4;
        var priorityCommandTarget = await package.GetServiceAsync(
                typeof(SVsRegisterPriorityCommandTarget)) as IVsRegisterPriorityCommandTarget
            ?? Package.GetGlobalService(
                typeof(SVsRegisterPriorityCommandTarget)) as IVsRegisterPriorityCommandTarget;
        if (dte == null
            || commandService == null
            || outputWindow == null
            || debugger == null
            || priorityCommandTarget == null)
        {
            BrowserCompatibilityTrace.Write(
                "command-initialize-services-missing",
                string.Join(
                    ",",
                    new[]
                    {
                        dte == null ? "dte" : null,
                        commandService == null ? "command-service" : null,
                        outputWindow == null ? "output-window" : null,
                        debugger == null ? "debugger" : null,
                        priorityCommandTarget == null ? "priority-command-target" : null
                    }.Where(value => value != null)));
            return null;
        }

        var paneGuid = OutputPaneGuid;
        outputWindow.CreatePane(ref paneGuid, "FrontEndCSharp Debugger", 1, 1);
        outputWindow.GetPane(ref paneGuid, out var outputPane);
        if (outputPane != null)
        {
            return new DebugWithFrontEndCSharpCommand(
                package,
                dte,
                outputPane,
                commandService,
                debugger,
                priorityCommandTarget);
        }

        return null;
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        webHost.Stop();
        debuggerEvents.OnEnterDesignMode -= OnEnterDesignMode;
        f5Compatibility.Dispose();
        editAndContinue.Dispose();
    }

    private void Execute(object sender, EventArgs e)
    {
        _ = package.JoinableTaskFactory.RunAsync(() => ExecuteAsync(project: null));
    }

    private void ExecuteApplyCodeChanges(object sender, EventArgs e)
    {
        _ = package.JoinableTaskFactory.RunAsync(editAndContinue.ApplyAsync);
    }

    private async Task ExecuteAsync(Project? project)
    {
        try
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
            {
                throw new InvalidOperationException(
                    "End the current debug session before starting FrontEndCSharp debugging.");
            }

            project ??= GetSelectedProject();
            var selectedProjectPath = Path.GetFullPath(project.FullName);
            if (!string.Equals(Path.GetExtension(selectedProjectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"'{project.Name}' is not a C# project. Select one C# project and try again.");
            }

            var solutionConfiguration = dte.Solution.SolutionBuild.ActiveConfiguration.Name;
            var projectConfiguration = project.ConfigurationManager?.ActiveConfiguration?.ConfigurationName
                ?? solutionConfiguration;
            var selectedProjectDirectory = Path.GetDirectoryName(selectedProjectPath)!;
            var selectedProjectText = File.ReadAllText(selectedProjectPath);
            var isWebHostLaunch = selectedProjectText.IndexOf(
                "Microsoft.NET.Sdk.Web",
                StringComparison.OrdinalIgnoreCase) >= 0;
            var debugProjectResult = await DebuggerProcessRunner.RunAsync(
                "dotnet",
                new[]
                {
                    "msbuild",
                    selectedProjectPath,
                    "-getProperty:FrontEndCSharpProject",
                    $"-property:Configuration={projectConfiguration}"
                },
                selectedProjectDirectory);
            DebuggerProcessRunner.EnsureSuccess("read the FrontEndCSharp project configuration", debugProjectResult);
            var configuredDebugProject = debugProjectResult.StandardOutput.Trim();
            isWebHostLaunch &= !string.IsNullOrWhiteSpace(configuredDebugProject);
            var debugProjectPath = string.IsNullOrWhiteSpace(configuredDebugProject)
                ? selectedProjectPath
                : Path.GetFullPath(Path.IsPathRooted(configuredDebugProject)
                    ? configuredDebugProject
                    : Path.Combine(selectedProjectDirectory, configuredDebugProject));
            if (!File.Exists(debugProjectPath))
            {
                throw new FileNotFoundException(
                    $"The configured FrontEndCSharp project does not exist: '{debugProjectPath}'.",
                    debugProjectPath);
            }
            var projectDirectory = Path.GetDirectoryName(debugProjectPath)!;

            string? webOutputPath = null;
            if (isWebHostLaunch)
            {
                var outputPathResult = await DebuggerProcessRunner.RunAsync(
                    "dotnet",
                    new[]
                    {
                        "msbuild",
                        selectedProjectPath,
                        "-getProperty:FrontEndCSharpOutputPath",
                        $"-property:Configuration={projectConfiguration}"
                    },
                    selectedProjectDirectory);
                DebuggerProcessRunner.EnsureSuccess("read the FrontEndCSharp web output configuration", outputPathResult);
                var configuredOutputPath = outputPathResult.StandardOutput.Trim();
                webOutputPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredOutputPath)
                    ? Path.Combine(selectedProjectDirectory, "wwwroot", "js")
                    : Path.IsPathRooted(configuredOutputPath)
                        ? configuredOutputPath
                        : Path.Combine(selectedProjectDirectory, configuredOutputPath));

                output.WriteLine($"Building web host {Path.GetFileNameWithoutExtension(selectedProjectPath)} ({solutionConfiguration})...");
                output.Activate();
                dte.Solution.SolutionBuild.BuildProject(
                    solutionConfiguration,
                    project.UniqueName,
                    WaitForBuildToFinish: true);
                if (dte.Solution.SolutionBuild.LastBuildInfo != 0)
                {
                    throw new InvalidOperationException(
                        $"The Visual Studio web-project build failed with {dte.Solution.SolutionBuild.LastBuildInfo} error(s). See the Build output.");
                }
            }

            output.WriteLine($"Building {Path.GetFileNameWithoutExtension(debugProjectPath)} ({solutionConfiguration})...");
            output.Activate();
            if (string.Equals(debugProjectPath, selectedProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                dte.Solution.SolutionBuild.BuildProject(
                    solutionConfiguration,
                    project.UniqueName,
                    WaitForBuildToFinish: true);
                if (dte.Solution.SolutionBuild.LastBuildInfo != 0)
                {
                    throw new InvalidOperationException(
                        $"The Visual Studio build failed with {dte.Solution.SolutionBuild.LastBuildInfo} error(s). See the Build output.");
                }
            }
            else
            {
                var buildResult = await DebuggerProcessRunner.RunAsync(
                    "dotnet",
                    new[] { "build", debugProjectPath, "--configuration", projectConfiguration },
                    projectDirectory);
                output.Write(buildResult);
                DebuggerProcessRunner.EnsureSuccess("build the configured FrontEndCSharp project", buildResult);
            }
            BrowserCompatibilityTrace.Write("command-build-complete", debugProjectPath);

            var targetPathResult = await DebuggerProcessRunner.RunAsync(
                "dotnet",
                new[]
                {
                    "msbuild",
                    debugProjectPath,
                    "-getProperty:TargetPath",
                    $"-property:Configuration={projectConfiguration}"
                },
                projectDirectory);
            DebuggerProcessRunner.EnsureSuccess("determine the built assembly path", targetPathResult);
            var builtTargetPath = targetPathResult.StandardOutput.Trim();
            if (!Path.IsPathRooted(builtTargetPath) || !File.Exists(builtTargetPath))
            {
                throw new FileNotFoundException(
                    $"MSBuild returned an invalid TargetPath for '{debugProjectPath}': '{builtTargetPath}'.",
                    builtTargetPath);
            }

            var packageDirectory = Path.GetDirectoryName(typeof(FrontEndCSharpPackage).Assembly.Location)!;
            var selectedCompilerPath = GetToolPath(packageDirectory, "Compiler.exe");
            var editHostPath = GetToolPath(
                packageDirectory,
                Path.Combine("EditAndContinue", "FrontEndCSharp.EditAndContinue.exe"));

            editAndContinue.Reset();
            var sessionDirectory = Path.Combine(
                projectDirectory,
                "obj",
                "FrontEndCSharp",
                SanitizePathComponent(projectConfiguration),
                $"enc-{Guid.NewGuid():N}");
            Directory.CreateDirectory(sessionDirectory);
            output.WriteLine("Establishing the Roslyn Edit and Continue baseline...");
            var editHost = await EditAndContinueClient.StartAsync(
                editHostPath,
                debugProjectPath,
                sessionDirectory);
            var targetPath = editHost.Initial.AssemblyPath;

            var selectedGeneratedOutputPath = webOutputPath ?? Path.Combine(
                    projectDirectory,
                    "obj",
                    "FrontEndCSharp",
                    SanitizePathComponent(projectConfiguration),
                    "debug-js");
            Directory.CreateDirectory(selectedGeneratedOutputPath);
            output.WriteLine($"Compiling {targetPath} to browser MSIL output...");
            var compileResult = await DebuggerProcessRunner.RunAsync(
                selectedCompilerPath,
                new[]
                {
                    "--debugger-rendezvous-timeout-ms",
                    DebuggerRendezvousTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture),
                    debugProjectPath,
                    selectedGeneratedOutputPath,
                    targetPath
                },
                projectDirectory);
            output.Write(compileResult);
            DebuggerProcessRunner.EnsureSuccess("compile the project for the browser", compileResult);
            if (isWebHostLaunch)
            {
                webHost.InvalidateNormalGenerationStamp(selectedProjectDirectory);
            }

            var manifestPath = Path.Combine(
                Path.GetDirectoryName(targetPath)!,
                Path.GetFileNameWithoutExtension(targetPath) + ManifestSuffix);
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException(
                    "The compiler did not emit a debug manifest. Build the project with matching portable or embedded PDB symbols.",
                    manifestPath);
            }

            var browserPath = FindBrowser();
            Uri? browserUrl = null;
            if (isWebHostLaunch)
            {
                webHost.Stop();
                browserUrl = await webHost.StartAsync(
                    selectedProjectPath,
                    projectConfiguration,
                    selectedProjectDirectory);
            }
            var launchPath = Path.Combine(
                projectDirectory,
                "obj",
                "FrontEndCSharp",
                SanitizePathComponent(projectConfiguration),
                "launch.json");
            WriteLaunchConfiguration(
                launchPath,
                targetPath,
                browserPath,
                browserUrl,
                selectedGeneratedOutputPath,
                manifestPath,
                $"FrontEndCSharp-EnC-{Guid.NewGuid():N}",
                Guid.NewGuid().ToString("N"));

            var launchConfiguration = ReadLaunchConfiguration(launchPath);

            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            output.WriteLine($"Launching the FrontEndCSharp debug adapter with {browserPath}...");
            var debugTargets = new[]
            {
                new VsDebugTargetInfo4
                {
                    dlo = CreateProcessDebugLaunchOperation,
                    guidLaunchDebugEngine = new Guid(DebugEngineGuid),
                    bstrExe = targetPath,
                    bstrOptions = File.ReadAllText(launchPath),
                    LaunchFlags = 0
                }
            };
            debugger.LaunchDebugTargets4(
                1,
                debugTargets,
                new VsDebugTargetProcessInfo[1]);
            BrowserCompatibilityTrace.Write("command-debug-launch-submitted");

            editAndContinue.Start(
                editHost.Client,
                selectedCompilerPath,
                debugProjectPath,
                selectedGeneratedOutputPath,
                manifestPath,
                sessionDirectory,
                launchConfiguration.EditAndContinuePipe,
                launchConfiguration.EditAndContinueToken);
        }
        catch (Exception ex)
        {
            webHost.Stop();
            output.WriteLine($"ERROR: {ex.Message}");
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            VsShellUtilities.ShowMessageBox(
                package,
                ex.Message,
                "FrontEndCSharp Debugger",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }

    private Project GetSelectedProject()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var selectedProjects = new System.Collections.Generic.List<Project>();
        for (var index = 1; index <= dte.SelectedItems.Count; index++)
        {
            var item = dte.SelectedItems.Item(index);
            var project = item.Project ?? item.ProjectItem?.ContainingProject;
            if (project != null && !selectedProjects.Contains(project))
            {
                selectedProjects.Add(project);
            }
        }
        if (selectedProjects.Count == 1)
        {
            return selectedProjects[0];
        }
        if (selectedProjects.Count > 1)
        {
            throw new InvalidOperationException("Select exactly one C# project to debug.");
        }

        if (dte.ActiveSolutionProjects is Array activeProjects
            && activeProjects.Cast<object>().OfType<Project>().FirstOrDefault() is Project activeProject)
        {
            return activeProject;
        }

        throw new InvalidOperationException(
            "Select a C# project in Solution Explorer before choosing Debug with FrontEndCSharp.");
    }

    private static string FindBrowser()
    {
        var configured = Environment.GetEnvironmentVariable("FRONTENDCSHARP_BROWSER");
        var candidates = new[]
        {
            configured,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "BraveSoftware", "Brave-Browser", "Application", "brave.exe")
        };
        var browser = candidates.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        if (browser == null)
        {
            throw new FileNotFoundException(
                "Chrome or Brave was not found. Set FRONTENDCSHARP_BROWSER to a Chromium executable path and restart Visual Studio.");
        }
        return Path.GetFullPath(browser);
    }

    private static string GetToolPath(string packageDirectory, string relativeToolPath)
    {
        var payloadPath = Path.Combine(packageDirectory, "CompilerPayload.zip");
        return CompilerPayloadCache.GetToolPath(payloadPath, relativeToolPath);
    }

    private static void WriteLaunchConfiguration(
        string path,
        string program,
        string browserPath,
        Uri? browserUrl,
        string generatedOutputPath,
        string debugManifestPath,
        string editAndContinuePipe,
        string editAndContinueToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var configuration = new FrontEndCSharpLaunchConfiguration
        {
            Type = "front-end-csharp",
            Request = "launch",
            Name = "Debug with FrontEndCSharp",
            Program = program,
            BrowserPath = browserPath,
            BrowserHeadless = false,
            BrowserUrl = browserUrl?.AbsoluteUri,
            GeneratedOutputPath = generatedOutputPath,
            DebugManifestPath = debugManifestPath,
            EditAndContinuePipe = editAndContinuePipe,
            EditAndContinueToken = editAndContinueToken
        };
        var serializer = new DataContractJsonSerializer(typeof(FrontEndCSharpLaunchConfiguration));
        using var stream = File.Create(path);
        using var writer = JsonReaderWriterFactory.CreateJsonWriter(
            stream,
            Encoding.UTF8,
            ownsStream: false,
            indent: true);
        serializer.WriteObject(writer, configuration);
    }

    private static FrontEndCSharpLaunchConfiguration ReadLaunchConfiguration(string path)
    {
        var serializer = new DataContractJsonSerializer(typeof(FrontEndCSharpLaunchConfiguration));
        using var stream = File.OpenRead(path);
        return (FrontEndCSharpLaunchConfiguration)serializer.ReadObject(stream);
    }

    private static string SanitizePathComponent(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }

}
