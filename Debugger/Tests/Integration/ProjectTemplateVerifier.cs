using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Xml.Linq;

namespace FrontEndCSharp.Debugger.Integration;

internal static class ProjectTemplateVerifier
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(5);

    public static void Verify(
        string templateKind,
        string path,
        string buildPackagePath,
        string repositoryRoot)
    {
        if (templateKind is not ("simple" or "fecsx"))
        {
            throw new ArgumentException(
                "Project-template kind must be 'simple' or 'fecsx'.",
                nameof(templateKind));
        }
        var templatePath = RequireFile(path);
        var buildPackage = RequireFile(buildPackagePath);
        var repository = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(repository))
        {
            throw new DirectoryNotFoundException(
                $"The repository root does not exist: '{repository}'.");
        }

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "FECT_" + Guid.NewGuid().ToString("N"));
        Process? hostProcess = null;
        Task<string>? hostOutputTask = null;
        Task<string>? hostErrorTask = null;
        try
        {
            var packageFeed = Path.Combine(temporaryDirectory, "feed");
            var packagesPath = Path.Combine(temporaryDirectory, "packages");
            var extractedTemplate = Path.Combine(temporaryDirectory, "template");
            var solutionRoot = Path.Combine(
                temporaryDirectory,
                "FrontEndCSharpTemplateSmoke");
            var hostRoot = Path.Combine(solutionRoot, "FrontEndCSharpTemplateSmoke");
            var browserRoot = Path.Combine(
                solutionRoot,
                "FrontEndCSharpTemplateSmoke.Browser");
            var hostProject = Path.Combine(
                hostRoot,
                "FrontEndCSharpTemplateSmoke.csproj");
            var browserProject = Path.Combine(
                browserRoot,
                "FrontEndCSharpTemplateSmoke.Browser.csproj");
            var solutionPath = Path.Combine(
                solutionRoot,
                "FrontEndCSharpTemplateSmoke.sln");
            Directory.CreateDirectory(packageFeed);
            Directory.CreateDirectory(extractedTemplate);
            File.Copy(
                buildPackage,
                Path.Combine(packageFeed, Path.GetFileName(buildPackage)));

            var packageProjects = new List<string>
            {
                @"Compiler\RuntimeOverrides\Abstractions\FrontEndCSharp.RuntimeOverrides.csproj",
                @"RuntimeOverrides\Interop.JS.Browser\Interop.JS.Browser\Interop.JS.Browser.csproj",
                @"RuntimeOverrides\Interop.JS.Browser\Interop.JS.Browser.fecs\Interop.JS.Browser.fecs.csproj",
                @"RuntimeOverrides\System.Private.CoreLib\System.Private.CoreLib.fecs\System.Private.CoreLib.fecs.csproj",
            };
            if (templateKind == "fecsx")
            {
                packageProjects.AddRange(
                [
                    @"Compiler\Fecsx\FrontEndCSharp.Fecsx.Build\FrontEndCSharp.Fecsx.Build.csproj",
                    @"RuntimeOverrides\Interop.JS.React\Interop.JS.React\Interop.JS.React.csproj",
                    @"RuntimeOverrides\Interop.JS.React\Interop.JS.React.fecs\Interop.JS.React.fecs.csproj",
                ]);
            }
            foreach (var relativeProject in packageProjects)
            {
                var projectPath = Path.Combine(repository, relativeProject);
                var arguments = new List<string>
                {
                    "pack",
                    projectPath,
                    "--configuration",
                    "Debug",
                    "--output",
                    packageFeed,
                    "--nologo",
                    "-nodeReuse:false",
                    "-p:RestoreFallbackFolders=",
                };
                if (!ProjectDeclaresVersion(projectPath))
                {
                    arguments.Add("-p:PackageVersion=1.0.0");
                }
                RunDotNet(
                    repository,
                    arguments.ToArray());
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(
                templatePath,
                extractedTemplate);
            ExpandTemplateProject(
                Path.Combine(extractedTemplate, "Host"),
                hostRoot,
                "FrontEndCSharpTemplateSmoke",
                "FrontEndCSharpTemplateSmoke.csproj");
            ExpandTemplateProject(
                Path.Combine(extractedTemplate, "Browser"),
                browserRoot,
                "FrontEndCSharpTemplateSmoke.Browser",
                "FrontEndCSharpTemplateSmoke.Browser.csproj");
            AssertInstantiatedProjectIsSelfContained(hostProject, browserProject);

            File.WriteAllText(Path.Combine(solutionRoot, "NuGet.config"), $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="FrontEndCSharp template verification" value="{{Escape(packageFeed)}}" />
                  </packageSources>
                </configuration>
                """);
            RunDotNet(
                solutionRoot,
                "new",
                "sln",
                "--name",
                "FrontEndCSharpTemplateSmoke",
                "--output",
                solutionRoot);
            RunDotNet(
                solutionRoot,
                "sln",
                solutionPath,
                "add",
                hostProject,
                browserProject);
            RunDotNet(
                solutionRoot,
                "build",
                solutionPath,
                "--configuration",
                "Debug",
                "--nologo",
                "-v:minimal",
                "-nodeReuse:false",
                $"-p:RestorePackagesPath={packagesPath}",
                "-p:RestoreFallbackFolders=");

            var browserOutput = Path.Combine(hostRoot, "wwwroot", "js");
            foreach (var requiredOutput in new[] { "index.js", "index.js.map", "msil.bin" })
            {
                RequireFile(Path.Combine(browserOutput, requiredOutput));
            }

            var port = ReservePort();
            var applicationUrl = $"http://127.0.0.1:{port}";
            hostProcess = StartProcess(
                "dotnet",
                solutionRoot,
                "run",
                "--no-build",
                "--project",
                hostProject,
                "--urls",
                applicationUrl);
            hostOutputTask = hostProcess.StandardOutput.ReadToEndAsync();
            hostErrorTask = hostProcess.StandardError.ReadToEndAsync();
            WaitForHost(hostProcess, hostErrorTask, applicationUrl);

            var browserPath = FindBrowser();
            var browserResult = BoundedProcess.Run(
                browserPath,
                temporaryDirectory,
                TimeSpan.FromSeconds(30),
                "--headless=new",
                "--disable-gpu",
                "--no-first-run",
                "--no-default-browser-check",
                "--user-data-dir=" + Path.Combine(temporaryDirectory, "browser-profile"),
                "--virtual-time-budget=3000",
                "--dump-dom",
                applicationUrl);
            var expectedMarker = templateKind == "fecsx"
                ? "This component was authored in FECSX and compiled from C#."
                : "Managed C# is running in the browser.";
            if (!browserResult.StandardOutput.Contains(
                expectedMarker,
                StringComparison.Ordinal))
            {
                var rendered = browserResult.StandardOutput[..Math.Min(
                    2000,
                    browserResult.StandardOutput.Length)];
                throw new InvalidDataException(
                    "The instantiated template did not run its managed entry point in the browser. "
                    + "Rendered DOM: " + rendered);
            }

            Console.WriteLine(
                "Verified that the Visual Studio project template instantiates, builds, and runs from "
                + templatePath + " (" + templateKind + ")");
        }
        finally
        {
            if (hostProcess != null)
            {
                if (!hostProcess.HasExited)
                {
                    hostProcess.Kill(entireProcessTree: true);
                    hostProcess.WaitForExit();
                }
                if (hostOutputTask != null && hostErrorTask != null)
                {
                    Task.WaitAll(hostOutputTask, hostErrorTask);
                }
                hostProcess.Dispose();
            }
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    private static bool ProjectDeclaresVersion(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants()
            .Any(element => string.Equals(
                    element.Name.LocalName,
                    "Version",
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(element.Value));

    private static void AssertInstantiatedProjectIsSelfContained(
        string hostProject,
        string browserProject)
    {
        foreach (var project in new[] { hostProject, browserProject })
        {
            var text = File.ReadAllText(project);
            if (text.Contains("<ProjectReference", StringComparison.OrdinalIgnoreCase)
                || text.Contains(@"..\..\Compiler", StringComparison.OrdinalIgnoreCase)
                || text.Contains(@"..\..\RuntimeOverrides", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Instantiated template project '{project}' contains a repository-relative dependency.");
            }
        }
    }

    private static void ExpandTemplateProject(
        string source,
        string destination,
        string safeProjectName,
        string projectFileName)
    {
        CopyDirectory(source, destination);
        var templateMetadata = Path.Combine(
            destination,
            Path.GetFileName(source) + ".vstemplate");
        if (File.Exists(templateMetadata))
        {
            File.Delete(templateMetadata);
        }
        var sourceProjects = Directory.GetFiles(destination, "*.csproj");
        if (sourceProjects.Length != 1)
        {
            throw new InvalidDataException(
                $"Expected one project file beneath '{destination}', found {sourceProjects.Length}.");
        }
        File.Move(
            sourceProjects[0],
            Path.Combine(destination, projectFileName));
        foreach (var file in Directory.EnumerateFiles(
            destination,
            "*",
            SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file)
                .Replace(
                    "$ext_safeprojectname$",
                    "FrontEndCSharpTemplateSmoke",
                    StringComparison.Ordinal)
                .Replace("$safeprojectname$", safeProjectName, StringComparison.Ordinal);
            File.WriteAllText(file, text);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var output = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.Copy(file, output, overwrite: true);
        }
    }

    private static Process StartProcess(
        string fileName,
        string workingDirectory,
        params string[] arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start();
        return process;
    }

    private static void WaitForHost(
        Process process,
        Task<string> errorTask,
        string applicationUrl)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    "The instantiated template host exited early:\n" + errorTask.Result);
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
            Thread.Sleep(100);
        }
        throw new TimeoutException("The instantiated template host did not become ready.");
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
            "Template verification requires Chrome, Edge, or Brave for the runnable-browser check.");
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

    private static void RunDotNet(string workingDirectory, params string[] arguments) =>
        BoundedProcess.Run("dotnet", workingDirectory, ProcessTimeout, arguments);

    private static void DeleteTemporaryDirectory(string path)
    {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var cleanupTarget = Path.GetFullPath(path);
        if (!cleanupTarget.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to remove template-verification directory outside '{temporaryRoot}': "
                + $"'{cleanupTarget}'.");
        }

        Exception? cleanupError = null;
        for (var attempt = 0; attempt < 12 && Directory.Exists(cleanupTarget); attempt++)
        {
            try
            {
                Thread.Sleep(250);
                Directory.Delete(cleanupTarget, recursive: true);
                cleanupError = null;
            }
            catch (IOException exception)
            {
                cleanupError = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                cleanupError = exception;
            }
        }
        if (Directory.Exists(cleanupTarget))
        {
            Console.Error.WriteLine(
                $"Warning: Could not completely remove template-verification directory "
                + $"'{cleanupTarget}': {cleanupError?.Message}");
        }
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
