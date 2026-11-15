using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using EnvDTE80;

namespace FrontEndCSharp.Debugger.Integration;

[SupportedOSPlatform("windows")]
internal static class VisualStudioProjectTemplateVerification
{
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromMinutes(5);

    public static void Run(IReadOnlyList<string> arguments)
    {
        var options = Options.Parse(arguments);
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

        var root = Path.Combine(
            Path.GetTempPath(),
            "FECTVS_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Process? process = null;
        dynamic? dte = null;
        try
        {
            var feed = Path.Combine(root, "feed");
            var packages = Path.Combine(root, "packages");
            Directory.CreateDirectory(feed);
            CreatePackageFeed(feed);
            WriteNuGetConfiguration(root, feed, packages);

            process = Launch(options.RootSuffix);
            var deadline = DateTime.UtcNow + options.Timeout;
            dte = VisualStudioAutomation.WaitForDte(
                process,
                deadline,
                options.AcceptProjectSecurityWarnings);

            VerifyTemplate(
                dte,
                process,
                root,
                "FrontEndCSharp.Browser.zip",
                "FecSimpleSmoke",
                "Managed C# is running in the browser.",
                options,
                deadline);
            VerifyTemplate(
                dte,
                process,
                root,
                "FrontEndCSharp.Fecsx.zip",
                "FecFecsxSmoke",
                "This component was authored in FECSX and compiled from C#.",
                options,
                deadline);

            Console.WriteLine(
                $"Verified installed simple and FECSX templates in the '{options.RootSuffix}' "
                + "Visual Studio hive: discovery, creation, build, and launch all succeeded.");
        }
        finally
        {
            if (dte != null)
            {
                TryStopDebugging(dte);
                TryCloseSolution(dte);
            }
            if (process != null)
            {
                VisualStudioAutomation.CloseProcesses([process.Id]);
                process.Dispose();
            }
            if (!options.KeepOutput)
            {
                DeleteTemporaryDirectory(root);
            }
            else
            {
                Console.WriteLine("Kept project-template acceptance output at " + root);
            }
        }
    }

    private static void VerifyTemplate(
        dynamic dte,
        Process process,
        string root,
        string templateArchiveName,
        string solutionName,
        string expectedMarker,
        Options options,
        DateTime deadline)
    {
        TryCloseSolution(dte);
        var solutionRoot = Path.Combine(root, solutionName);

        var solution = (Solution2)dte.Solution;
        var templateDisplayName = templateArchiveName == "FrontEndCSharp.Fecsx.zip"
            ? "FrontEndCSharp FECSX React Application"
            : "FrontEndCSharp Browser Application";
        CreateFromNewProjectDialog(
            dte,
            process,
            solutionRoot,
            solutionName,
            templateDisplayName,
            options.AcceptProjectSecurityWarnings,
            deadline);
        WaitForCreatedProjects(solutionRoot, solutionName, deadline);

        var hostProject = Directory.EnumerateFiles(
                solutionRoot,
                solutionName + ".csproj",
                SearchOption.AllDirectories)
            .Single(path => !path.Contains(
                solutionName + ".Browser",
                StringComparison.OrdinalIgnoreCase));
        var browserProject = Directory.EnumerateFiles(
                solutionRoot,
                solutionName + ".Browser.csproj",
                SearchOption.AllDirectories)
            .Single();
        WaitForCreatedTemplateFiles(
            hostProject,
            browserProject,
            templateArchiveName == "FrontEndCSharp.Fecsx.zip",
            deadline);
        AssertSelfContained(hostProject, browserProject);

        var port = ReservePort();
        RewriteLaunchUrl(
            Path.Combine(
                Path.GetDirectoryName(hostProject)!,
                "Properties",
                "launchSettings.json"),
            port);
        dte.Solution.SolutionBuild.StartupProjects = new[]
        {
            FindProjectUniqueName(dte, hostProject),
        };
        dte.Solution.SolutionBuild.Build(true);
        if ((int)dte.Solution.SolutionBuild.LastBuildInfo != 0)
        {
            throw new InvalidOperationException(
                $"Visual Studio failed to build installed template '{templateArchiveName}'.");
        }

        var output = Path.Combine(
            Path.GetDirectoryName(hostProject)!,
            "wwwroot",
            "js");
        foreach (var name in new[] { "index.js", "index.js.map", "msil.bin" })
        {
            RequireFile(Path.Combine(output, name));
        }

        var applicationUrl = $"http://127.0.0.1:{port}";
        try
        {
            dte.ExecuteCommand("Debug.Start");
            WaitForHost(process, applicationUrl, deadline, options.AcceptProjectSecurityWarnings);
        }
        finally
        {
            TryStopDebugging(dte);
        }

        // F5 intentionally arms a debugger rendezvous build. Rebuild after the
        // debugger has consumed its one-shot arm, then run that ordinary bundle
        // in a controlled host so runtime verification is independent of an IDE
        // browser selection while the Visual Studio launch remains covered above.
        dte.Solution.SolutionBuild.Build(true);
        if ((int)dte.Solution.SolutionBuild.LastBuildInfo != 0)
        {
            throw new InvalidOperationException(
                $"Visual Studio failed to rebuild installed template '{templateArchiveName}' "
                + "after debugger launch.");
        }
        VerifyStandaloneLaunch(
            root,
            solutionName,
            hostProject,
            expectedMarker,
            deadline);
        Console.WriteLine(
            $"Verified installed template '{templateArchiveName}' through New Project.");
    }

    private static void CreateFromNewProjectDialog(
        dynamic dte,
        Process process,
        string solutionRoot,
        string solutionName,
        string templateDisplayName,
        bool acceptProjectSecurityWarnings,
        DateTime deadline)
    {
        var dialogCommand = Task.Run(() => dte.ExecuteCommand("File.NewProject"));
        var search = WaitForElement(
            process,
            "_Search for templates (Alt+S)",
            VisualStudioControlType.Edit,
            deadline,
            "Create a new project search box");
        VisualStudioAutomation.SetValue(search, templateDisplayName);
        var template = WaitForElement(
            process,
            templateDisplayName,
            VisualStudioControlType.ListItem,
            deadline,
            "project template result");
        VisualStudioAutomation.Select(template);
        var next = WaitForEnabledElement(
            process,
            "Next",
            VisualStudioControlType.Button,
            deadline,
            "New Project Next button");
        VisualStudioAutomation.Invoke(next);
        var projectName = WaitForElementByAutomationId(
            process,
            "projectNameText",
            VisualStudioControlType.Edit,
            deadline,
            "Project name box");
        var location = WaitForElementByAutomationId(
            process,
            "PART_EditableTextBox",
            VisualStudioControlType.Edit,
            deadline,
            "Location box");
        var configuredSolutionName = WaitForElementByAutomationId(
            process,
            "solutionNameText",
            VisualStudioControlType.Edit,
            deadline,
            "Solution name box");
        VisualStudioAutomation.SetValue(projectName, solutionName);
        VisualStudioAutomation.SetValue(location, Path.GetDirectoryName(solutionRoot)!);
        VisualStudioAutomation.SetValue(configuredSolutionName, solutionName);
        var sameDirectory = WaitForElementByAutomationId(
            process,
            "slnProjInSameDirChk",
            VisualStudioControlType.CheckBox,
            deadline,
            "Place solution and project in the same directory box");
        VisualStudioAutomation.SetChecked(sameDirectory, isChecked: false);
        var create = WaitForEnabledElementByAutomationId(
            process,
            "button_Next",
            VisualStudioControlType.Button,
            deadline,
            "Create button");
        if (create.Name != "Create")
        {
            throw new InvalidOperationException(
                $"Expected the New Project Create button, but found '{create.Name}'.");
        }
        VisualStudioAutomation.Invoke(create);
        var createDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (!dialogCommand.IsCompleted
               && DateTime.UtcNow < deadline
               && DateTime.UtcNow < createDeadline)
        {
            VisualStudioAutomation.ResolveProjectSecurityWarning(
                process,
                acceptProjectSecurityWarnings);
            var problem = VisualStudioAutomation.FindElements(
                    process,
                    VisualStudioControlType.Text)
                .FirstOrDefault(element => element.Name.StartsWith(
                    "A problem was encountered creating the sub project",
                    StringComparison.Ordinal));
            if (problem != null)
            {
                var ok = VisualStudioAutomation.FindElement(
                    process,
                    "OK",
                    VisualStudioControlType.Button);
                if (ok != null)
                {
                    VisualStudioAutomation.Invoke(ok);
                }
                throw new InvalidOperationException(problem.Name);
            }
            Thread.Sleep(250);
        }
        if (!dialogCommand.IsCompleted)
        {
            VisualStudioAutomation.SaveWindowScreenshot(
                process,
                Path.Combine(Path.GetDirectoryName(solutionRoot)!, "project-create-wait.png"));
            foreach (var element in VisualStudioAutomation.FindElements(
                process,
                VisualStudioControlType.Text))
            {
                Console.Error.WriteLine(
                    $"PROJECT_CREATE_TEXT Name='{element.Name}' "
                    + $"AutomationId='{element.AutomationId}' Offscreen={element.IsOffscreen}");
            }
            throw new TimeoutException(
                $"Visual Studio did not finish creating '{solutionName}'.");
        }
        dialogCommand.GetAwaiter().GetResult();
        VisualStudioAutomation.WaitForSolution(dte, process, deadline);
        dte.ExecuteCommand("File.SaveAll");
    }

    private static VisualStudioAutomationElement WaitForElement(
        Process process,
        string name,
        VisualStudioControlType controlType,
        DateTime deadline,
        string description)
    {
        while (DateTime.UtcNow < deadline)
        {
            var element = VisualStudioAutomation.FindElement(process, name, controlType);
            if (element != null)
            {
                return element;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException($"Visual Studio did not expose the {description}.");
    }

    private static VisualStudioAutomationElement WaitForElementByAutomationId(
        Process process,
        string automationId,
        VisualStudioControlType controlType,
        DateTime deadline,
        string description)
    {
        while (DateTime.UtcNow < deadline)
        {
            var element = VisualStudioAutomation.FindElementByAutomationId(
                process,
                automationId,
                controlType);
            if (element != null)
            {
                return element;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException($"Visual Studio did not expose the {description}.");
    }

    private static VisualStudioAutomationElement WaitForEnabledElement(
        Process process,
        string name,
        VisualStudioControlType controlType,
        DateTime deadline,
        string description)
    {
        while (DateTime.UtcNow < deadline)
        {
            var element = VisualStudioAutomation.FindElement(process, name, controlType);
            if (element is { IsEnabled: true })
            {
                return element;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            $"Visual Studio did not enable the {description}.");
    }

    private static VisualStudioAutomationElement WaitForEnabledElementByAutomationId(
        Process process,
        string automationId,
        VisualStudioControlType controlType,
        DateTime deadline,
        string description)
    {
        while (DateTime.UtcNow < deadline)
        {
            var element = VisualStudioAutomation.FindElementByAutomationId(
                process,
                automationId,
                controlType);
            if (element is { IsEnabled: true })
            {
                return element;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            $"Visual Studio did not enable the {description}.");
    }

    private static void CreatePackageFeed(string feed)
    {
        foreach (var relativeProject in new[]
        {
            @"Compiler\Packaging\FrontEndCSharp.FECS.Internals.Build\FrontEndCSharp.FECS.Internals.Build.csproj",
            @"Compiler\RuntimeOverrides\Abstractions\FrontEndCSharp.RuntimeOverrides.csproj",
            @"Compiler\Fecsx\FrontEndCSharp.FECS.Internals.Fecsx.Build\FrontEndCSharp.FECS.Internals.Fecsx.Build.csproj",
            @"Overrides\FrontEndCSharp.Overrides.Interop.JS.Browser\FrontEndCSharp.Overrides.Interop.JS.Browser\FrontEndCSharp.Overrides.Interop.JS.Browser.csproj",
            @"Overrides\FrontEndCSharp.Overrides.Interop.JS.Browser\FrontEndCSharp.Overrides.Interop.JS.Browser.fecs\FrontEndCSharp.Overrides.Interop.JS.Browser.fecs.csproj",
            @"Overrides\FrontEndCSharp.Overrides.Interop.JS.React\FrontEndCSharp.Overrides.Interop.JS.React\FrontEndCSharp.Overrides.Interop.JS.React.csproj",
            @"Overrides\FrontEndCSharp.Overrides.Interop.JS.React\FrontEndCSharp.Overrides.Interop.JS.React.fecs\FrontEndCSharp.Overrides.Interop.JS.React.fecs.csproj",
            @"Overrides\FrontEndCSharp.Overrides.System.Private.CoreLib\FrontEndCSharp.Overrides.System.Private.CoreLib.fecs\FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.csproj",
        })
        {
            BoundedProcess.Run(
                "dotnet",
                RepositoryLayout.Root,
                ToolTimeout,
                "pack",
                Path.Combine(RepositoryLayout.Root, relativeProject),
                "--configuration",
                "Debug",
                "--output",
                feed,
                "--nologo",
                "-p:RestoreFallbackFolders=");
        }
    }

    private static void WriteNuGetConfiguration(
        string solutionRoot,
        string feed,
        string packages) =>
        File.WriteAllText(
            Path.Combine(solutionRoot, "NuGet.config"),
            $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <config>
                <add key="globalPackagesFolder" value="{{Escape(packages)}}" />
              </config>
              <packageSources>
                <clear />
                <add key="FrontEndCSharp template acceptance" value="{{Escape(feed)}}" />
              </packageSources>
            </configuration>
            """);

    private static void WaitForCreatedProjects(
        string solutionRoot,
        string solutionName,
        DateTime deadline)
    {
        var host = solutionName + ".csproj";
        var browser = solutionName + ".Browser.csproj";
        while (DateTime.UtcNow < deadline)
        {
            var projects = Directory.EnumerateFiles(
                    solutionRoot,
                    "*.csproj",
                    SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (projects.Contains(host) && projects.Contains(browser))
            {
                return;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            $"Visual Studio did not instantiate both projects for '{solutionName}'.");
    }

    private static void WaitForCreatedTemplateFiles(
        string hostProject,
        string browserProject,
        bool isFecsx,
        DateTime deadline)
    {
        var hostDirectory = Path.GetDirectoryName(hostProject)!;
        var browserDirectory = Path.GetDirectoryName(browserProject)!;
        var required = new List<string>
        {
            Path.Combine(hostDirectory, "Program.cs"),
            Path.Combine(hostDirectory, "Properties", "launchSettings.json"),
            Path.Combine(hostDirectory, "wwwroot", "index.html"),
            Path.Combine(hostDirectory, "wwwroot", "site.css"),
            Path.Combine(browserDirectory, "Program.cs"),
        };
        if (isFecsx)
        {
            required.Add(Path.Combine(browserDirectory, "App.fecsx"));
            required.Add(Path.Combine(browserDirectory, "AppProps.cs"));
        }
        while (DateTime.UtcNow < deadline)
        {
            if (required.All(File.Exists))
            {
                return;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            "Visual Studio did not finish materializing template files:\n  "
            + string.Join("\n  ", required.Where(path => !File.Exists(path))));
    }

    private static void RewriteLaunchUrl(string path, int port)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))
            ?? throw new InvalidDataException($"'{path}' contains no JSON value.");
        var profiles = root["profiles"]?.AsObject()
            ?? throw new InvalidDataException($"'{path}' has no profiles object.");
        var profile = profiles.First().Value?.AsObject()
            ?? throw new InvalidDataException($"'{path}' has no launch profile.");
        profile["applicationUrl"] = $"http://127.0.0.1:{port}";
        profile["launchBrowser"] = false;
        File.WriteAllText(
            path,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AssertSelfContained(string hostProject, string browserProject)
    {
        foreach (var path in new[] { hostProject, browserProject })
        {
            var text = File.ReadAllText(path);
            if (text.Contains("<ProjectReference", StringComparison.OrdinalIgnoreCase)
                || text.Contains(@"..\..\Compiler", StringComparison.OrdinalIgnoreCase)
                || text.Contains(@"..\..\Overrides", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Visual Studio instantiated repository-relative dependencies in '{path}'.");
            }
        }
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
            $"Visual Studio did not load host project '{projectPath}'.");
    }

    private static string? FindProjectUniqueNameInProject(dynamic project, string projectPath)
    {
        try
        {
            if (string.Equals(
                Path.GetFullPath((string)project.FullName),
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

    private static void WaitForHost(
        Process process,
        string applicationUrl,
        DateTime deadline,
        bool acceptProjectSecurityWarnings)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        while (DateTime.UtcNow < deadline)
        {
            VisualStudioAutomation.ResolveProjectSecurityWarning(
                process,
                acceptProjectSecurityWarnings);
            try
            {
                using var response = client.GetAsync(applicationUrl).GetAwaiter().GetResult();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            $"Visual Studio did not launch template host '{applicationUrl}'.");
    }

    private static void VerifyStandaloneLaunch(
        string root,
        string solutionName,
        string hostProject,
        string expectedMarker,
        DateTime deadline)
    {
        var port = ReservePort();
        var applicationUrl = $"http://127.0.0.1:{port}";
        using var host = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(hostProject)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        host.StartInfo.ArgumentList.Add("run");
        host.StartInfo.ArgumentList.Add("--no-build");
        host.StartInfo.ArgumentList.Add("--project");
        host.StartInfo.ArgumentList.Add(hostProject);
        host.StartInfo.ArgumentList.Add("--urls");
        host.StartInfo.ArgumentList.Add(applicationUrl);
        host.Start();
        var standardOutput = host.StandardOutput.ReadToEndAsync();
        var standardError = host.StandardError.ReadToEndAsync();
        try
        {
            WaitForStandaloneHost(host, standardError, applicationUrl, deadline);
            VerifyRenderedMarker(root, solutionName, applicationUrl, expectedMarker);
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
                host.WaitForExit();
            }
            Task.WaitAll(standardOutput, standardError);
        }
    }

    private static void WaitForStandaloneHost(
        Process host,
        Task<string> standardError,
        string applicationUrl,
        DateTime deadline)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        while (DateTime.UtcNow < deadline)
        {
            if (host.HasExited)
            {
                throw new InvalidOperationException(
                    $"Template host exited with code {host.ExitCode}: {standardError.Result}");
            }
            try
            {
                using var response = client.GetAsync(applicationUrl).GetAwaiter().GetResult();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            $"Standalone template host did not launch at '{applicationUrl}'.");
    }

    private static void VerifyRenderedMarker(
        string root,
        string solutionName,
        string applicationUrl,
        string expectedMarker)
    {
        var result = BoundedProcess.Run(
            FindBrowser(),
            root,
            TimeSpan.FromSeconds(30),
            "--headless=new",
            "--disable-gpu",
            "--no-first-run",
            "--no-default-browser-check",
            "--user-data-dir=" + Path.Combine(root, solutionName + "-browser-profile"),
            "--virtual-time-budget=3000",
            "--dump-dom",
            applicationUrl);
        if (!result.StandardOutput.Contains(expectedMarker, StringComparison.Ordinal))
        {
            var rendered = result.StandardOutput[..Math.Min(2000, result.StandardOutput.Length)];
            throw new InvalidDataException(
                $"Visual Studio launched '{solutionName}', but its managed marker was absent. "
                + "Rendered DOM: " + rendered);
        }
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindBrowser()
    {
        foreach (var path in new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
        })
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        throw new FileNotFoundException(
            "Project-template acceptance requires Chrome, Edge, or Brave.");
    }

    private static Process Launch(string rootSuffix)
    {
        var start = new ProcessStartInfo
        {
            FileName = VisualStudioAutomation.FindDevenv(),
            UseShellExecute = false,
        };
        start.ArgumentList.Add("/RootSuffix");
        start.ArgumentList.Add(rootSuffix);
        return Process.Start(start)
            ?? throw new InvalidOperationException("Visual Studio did not start.");
    }

    private static void TryStopDebugging(dynamic dte)
    {
        try
        {
            if ((int)dte.Debugger.CurrentMode != 1)
            {
                dte.ExecuteCommand("Debug.StopDebugging");
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
                while ((int)dte.Debugger.CurrentMode != 1 && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(250);
                }
            }
        }
        catch (Exception exception) when (
            exception is COMException || VisualStudioAutomation.IsTransientComFailure(exception))
        {
        }
    }

    private static void TryCloseSolution(dynamic dte)
    {
        try
        {
            if (dte.Solution.IsOpen)
            {
                dte.Solution.Close(false);
            }
        }
        catch (Exception exception) when (
            exception is COMException || VisualStudioAutomation.IsTransientComFailure(exception))
        {
        }
    }

    private static string RequireFile(string path)
    {
        var resolved = Path.GetFullPath(path);
        if (!File.Exists(resolved))
        {
            throw new FileNotFoundException("Required file does not exist.", resolved);
        }
        return resolved;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(path);
        if (!target.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to remove template-acceptance output outside '{temporaryRoot}'.");
        }
        for (var attempt = 0; attempt < 12 && Directory.Exists(target); attempt++)
        {
            try
            {
                Thread.Sleep(250);
                Directory.Delete(target, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        if (Directory.Exists(target))
        {
            Console.Error.WriteLine(
                $"Warning: Could not completely remove template acceptance output '{target}'.");
        }
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private sealed class Options
    {
        public string RootSuffix { get; set; } = "Exp";
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(8);
        public bool AcceptProjectSecurityWarnings { get; set; }
        public bool CloseExisting { get; set; }
        public bool KeepOutput { get; set; }

        public static Options Parse(IReadOnlyList<string> arguments)
        {
            var options = new Options();
            for (var index = 0; index < arguments.Count; index++)
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
                if (name == "--keep-output")
                {
                    options.KeepOutput = true;
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
                else
                {
                    throw new ArgumentException($"Unknown option '{name}'.");
                }
            }
            if (options.Timeout < TimeSpan.FromMinutes(2)
                || options.Timeout > TimeSpan.FromMinutes(20))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arguments),
                    "--timeout must be between 120 and 1200 seconds.");
            }
            return options;
        }
    }
}
