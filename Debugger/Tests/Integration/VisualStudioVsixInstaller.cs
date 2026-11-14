using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace FrontEndCSharp.Debugger.Integration;

[SupportedOSPlatform("windows")]
internal static partial class VisualStudioVsixInstaller
{
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromMinutes(5);

    public static void Install(string package, string rootSuffix)
    {
        if (package == "debugger")
        {
            InstallDebugger(rootSuffix);
            return;
        }
        if (package == "fecsx")
        {
            InstallFecsx(rootSuffix);
            return;
        }
        throw new ArgumentException("install-vsix package must be 'debugger' or 'fecsx'.");
    }

    private static void InstallDebugger(string rootSuffix)
    {
        var repository = RepositoryLayout.Root;
        var visualStudio = VisualStudioInstallation.Find();
        EnsureHiveClosed(rootSuffix);
        var project = Path.Combine(
            repository,
            "Debugger",
            "VisualStudio",
            "FrontEndCSharp.VisualStudio.csproj");
        var vsix = Path.Combine(
            repository,
            "Debugger",
            "VisualStudio",
            "bin",
            "Debug",
            "net472",
            "FrontEndCSharp.VisualStudio.vsix");
        Build(visualStudio.MSBuild, project, repository, vsix);
        foreach (var extensionId in new[]
        {
            "FrontEndCSharp.JavaScriptProxyRepro.27d4cf33-b6bb-4eb4-b498-2c1785379658",
            "FrontEndCSharp.Debugger.cfcb81f4-801e-4f6b-a582-3dcd42ca0218",
        })
        {
            Run(
                visualStudio.VsixInstaller,
                repository,
                [0, 1002],
                "/quiet",
                "/rootSuffix:" + rootSuffix,
                "/uninstall:" + extensionId);
        }
        Run(
            visualStudio.VsixInstaller,
            repository,
            [0],
            "/quiet",
            "/rootSuffix:" + rootSuffix,
            vsix);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            UpdateConfiguration(visualStudio, rootSuffix, repository);
            var registration = Run(
                visualStudio.VsRegEdit,
                repository,
                [0],
                "read",
                visualStudio.InstallationPath,
                rootSuffix,
                "HKLM",
                @"CLSID\{FC152D13-6E45-44C6-B80C-25A153B931F8}",
                "CodeBase",
                "string").StandardOutput;
            var match = ValueRegex().Match(registration);
            if (match.Success && File.Exists(match.Groups["path"].Value.Trim()))
            {
                Console.WriteLine(
                    $"Installed the FrontEndCSharp debugger into the '{rootSuffix}' hive "
                    + "and verified its proxy registration.");
                return;
            }
            Thread.Sleep(TimeSpan.FromSeconds(2));
        }
        throw new InvalidDataException(
            "The FrontEndCSharp proxy CodeBase was not registered to an existing assembly.");
    }

    private static void InstallFecsx(string rootSuffix)
    {
        var repository = RepositoryLayout.Root;
        var visualStudio = VisualStudioInstallation.Find();
        EnsureHiveClosed(rootSuffix);
        var project = Path.Combine(
            repository,
            "Compiler",
            "Fecsx",
            "FrontEndCSharp.Fecsx.VSIX",
            "FrontEndCSharp.Fecsx.VSIX.csproj");
        var vsix = Path.Combine(
            repository,
            "Compiler",
            "Fecsx",
            "FrontEndCSharp.Fecsx.VSIX",
            "bin",
            "Debug",
            "net472",
            "FrontEndCSharp.Fecsx.VSIX.vsix");
        Build(visualStudio.MSBuild, project, repository, vsix);
        const string extensionId =
            "FrontEndCSharp.Fecsx.Editor.5356caa8-a2ae-456f-b56a-2c20218c1999";
        Run(
            visualStudio.VsixInstaller,
            repository,
            [0, 1002],
            "/quiet",
            "/rootSuffix:" + rootSuffix,
            "/uninstall:" + extensionId);
        Run(
            visualStudio.VsixInstaller,
            repository,
            [0],
            "/quiet",
            "/rootSuffix:" + rootSuffix,
            vsix);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            UpdateConfiguration(visualStudio, rootSuffix, repository);
            if (VerifyFecsxRegistry(visualStudio, rootSuffix, repository))
            {
                ReportFecsxInstalled(rootSuffix, "effective registry registrations");
                return;
            }
            Thread.Sleep(TimeSpan.FromSeconds(2));
        }
        if (VerifyFecsxInstalledFiles(rootSuffix))
        {
            ReportFecsxInstalled(rootSuffix, "installed payload and MEF catalog");
            return;
        }
        throw new InvalidDataException(
            "The FECSX TextMate, file icon, and MEF registrations could not be verified.");
    }

    private static bool VerifyFecsxRegistry(
        VisualStudioInstallation visualStudio,
        string rootSuffix,
        string repository)
    {
        string? Read(string key, string value)
        {
            var result = Run(
                visualStudio.VsRegEdit,
                repository,
                [0, -1],
                "read",
                visualStudio.InstallationPath,
                rootSuffix,
                "HKLM",
                key,
                value,
                "string");
            return result.ExitCode == 0 ? result.StandardOutput : null;
        }
        var textMate = Read(@"TextMate\Repositories", "FrontEndCSharp.Fecsx");
        var language = Read(
            @"TextMate\LanguageConfiguration\ContentTypeMapping",
            "FrontEndCSharp.Fecsx");
        var icon = Read(@"ShellFileAssociations\.fecsx", "DefaultIconMoniker");
        var binding = Read(
            @"BindingPaths\{dd37ad22-31f8-4dde-b7bd-5d1424d89a93}",
            "$PackageFolder$");
        return textMate?.Contains("Assets", StringComparison.Ordinal) == true
            && language?.Contains("language-configuration.json", StringComparison.Ordinal) == true
            && icon?.Contains(
                "dd37ad22-31f8-4dde-b7bd-5d1424d89a93",
                StringComparison.OrdinalIgnoreCase) == true
            && binding?.Contains("$PackageFolder$", StringComparison.Ordinal) == true;
    }

    private static bool VerifyFecsxInstalledFiles(string rootSuffix)
    {
        var visualStudioRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "VisualStudio");
        if (!Directory.Exists(visualStudioRoot))
        {
            return false;
        }
        var instance = new DirectoryInfo(visualStudioRoot)
            .EnumerateDirectories()
            .Where(item => item.Name.EndsWith(rootSuffix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.LastWriteTimeUtc)
            .FirstOrDefault();
        if (instance == null)
        {
            return false;
        }
        var extensions = Path.Combine(instance.FullName, "Extensions");
        var packageDefinition = FindFile(extensions, "Fecsx.pkgdef");
        var imageManifest = FindFile(extensions, "FecsxImages.imagemanifest");
        var catalog = Path.Combine(
            instance.FullName,
            "ComponentModelCache",
            "Microsoft.VisualStudio.Default.catalogs");
        if (packageDefinition == null || imageManifest == null || !File.Exists(catalog))
        {
            return false;
        }
        var packageText = File.ReadAllText(packageDefinition);
        var imageText = File.ReadAllText(imageManifest);
        var catalogText = Encoding.UTF8.GetString(File.ReadAllBytes(catalog));
        return packageText.Contains(@"TextMate\Repositories", StringComparison.Ordinal)
            && packageText.Contains(
                @"TextMate\LanguageConfiguration\ContentTypeMapping",
                StringComparison.Ordinal)
            && packageText.Contains(@"ShellFileAssociations\.fecsx", StringComparison.Ordinal)
            && packageText.Contains(
                @"BindingPaths\{dd37ad22-31f8-4dde-b7bd-5d1424d89a93}",
                StringComparison.OrdinalIgnoreCase)
            && imageText.Contains("FecsxFile.xaml", StringComparison.Ordinal)
            && catalogText.Contains("FrontEndCSharp.Fecsx.VSIX", StringComparison.Ordinal);
    }

    private static string? FindFile(string root, string name) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault()
            : null;

    private static void Build(
        string msBuild,
        string project,
        string repository,
        string output)
    {
        BoundedProcess.Run(
            msBuild,
            repository,
            ToolTimeout,
            project,
            "/t:Build",
            "/p:Configuration=Debug",
            "/p:DeployExtension=false",
            "/v:minimal");
        if (!File.Exists(output))
        {
            throw new FileNotFoundException("The VSIX build produced no package.", output);
        }
    }

    private static void EnsureHiveClosed(string rootSuffix)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT CommandLine FROM Win32_Process WHERE Name = 'devenv.exe'");
        foreach (var item in searcher.Get().Cast<ManagementObject>())
        {
            var commandLine = item["CommandLine"] as string;
            if (commandLine != null
                && Regex.IsMatch(
                    commandLine,
                    @"(?i)/RootSuffix\s+" + Regex.Escape(rootSuffix) + @"(?:\s|$)"))
            {
                throw new InvalidOperationException(
                    $"Close the '{rootSuffix}' Visual Studio instance before installing.");
            }
        }
    }

    private static void UpdateConfiguration(
        VisualStudioInstallation visualStudio,
        string rootSuffix,
        string repository) =>
        Run(
            visualStudio.Devenv,
            repository,
            [0],
            "/RootSuffix",
            rootSuffix,
            "/UpdateConfiguration");

    private static ProcessResult Run(
        string fileName,
        string workingDirectory,
        IReadOnlyCollection<int> acceptedExitCodes,
        params string[] arguments)
    {
        using var process = new Process
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
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(ToolTimeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"{fileName} exceeded five minutes.");
        }
        Task.WaitAll(output, error);
        if (!acceptedExitCodes.Contains(process.ExitCode))
        {
            throw new InvalidOperationException(
                $"{fileName} exited with code {process.ExitCode}.\n{output.Result}{error.Result}");
        }
        return new ProcessResult(process.ExitCode, output.Result, error.Result);
    }

    private static void ReportFecsxInstalled(string rootSuffix, string verification) =>
        Console.WriteLine(
            $"Installed the FECSX editor into the '{rootSuffix}' hive and verified "
            + $"its {verification}.");

    [GeneratedRegex(@"Value:\s*(?<path>.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ValueRegex();

    private sealed record class ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed class VisualStudioInstallation
    {
        public required string InstallationPath { get; init; }
        public required string VsixInstaller { get; init; }
        public required string Devenv { get; init; }
        public required string VsRegEdit { get; init; }
        public required string MSBuild { get; init; }

        public static VisualStudioInstallation Find()
        {
            var programFiles = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);
            var vswhere = Path.Combine(
                programFiles,
                "Microsoft Visual Studio",
                "Installer",
                "vswhere.exe");
            if (!File.Exists(vswhere))
            {
                throw new FileNotFoundException("vswhere.exe was not found.", vswhere);
            }
            var installationPath = Run(
                vswhere,
                RepositoryLayout.Root,
                [0],
                "-latest",
                "-products",
                "*",
                "-property",
                "installationPath").StandardOutput.Trim();
            if (string.IsNullOrWhiteSpace(installationPath))
            {
                throw new InvalidOperationException("No Visual Studio installation was found.");
            }
            var result = new VisualStudioInstallation
            {
                InstallationPath = installationPath,
                VsixInstaller = Path.Combine(
                    installationPath,
                    "Common7",
                    "IDE",
                    "VSIXInstaller.exe"),
                Devenv = Path.Combine(
                    installationPath,
                    "Common7",
                    "IDE",
                    "devenv.com"),
                VsRegEdit = Path.Combine(
                    installationPath,
                    "Common7",
                    "IDE",
                    "VsRegEdit.exe"),
                MSBuild = Path.Combine(
                    installationPath,
                    "MSBuild",
                    "Current",
                    "Bin",
                    "MSBuild.exe"),
            };
            foreach (var required in new[]
            {
                result.VsixInstaller,
                result.Devenv,
                result.VsRegEdit,
                result.MSBuild,
            })
            {
                if (!File.Exists(required))
                {
                    throw new FileNotFoundException(
                        "A required Visual Studio tool was not found.",
                        required);
                }
            }
            return result;
        }
    }
}
