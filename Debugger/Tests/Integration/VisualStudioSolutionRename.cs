using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FrontEndCSharp.Debugger.Integration;

[SupportedOSPlatform("windows")]
internal static class VisualStudioSolutionRename
{
    public static void Run(IReadOnlyList<string> arguments)
    {
        var options = RenameOptions.Parse(arguments);
        Directory.CreateDirectory(options.OutputDirectory);
        using var fixture = RenameFixture.Create(options.KeepFixture);
        fixture.CreateSolution();

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
                + "Use --close-existing.");
        }

        using var process = Launch(options.RootSuffix, fixture);
        try
        {
            VerifyRename(process, fixture, options);
            fixture.MarkCompleted();
        }
        catch
        {
            try
            {
                VisualStudioAutomation.SaveWindowScreenshot(
                    process,
                    Path.Combine(
                        options.OutputDirectory,
                        "fecsx-solution-rename-failure.png"));
            }
            catch (Exception screenshotException)
            {
                Console.Error.WriteLine(
                    "Could not capture the Visual Studio failure state: "
                    + screenshotException.Message);
            }
            throw;
        }
        finally
        {
            VisualStudioAutomation.CloseProcesses([process.Id]);
        }
    }

    private static Process Launch(string rootSuffix, RenameFixture fixture)
    {
        var start = new ProcessStartInfo
        {
            FileName = VisualStudioAutomation.FindDevenv(),
            UseShellExecute = false,
        };
        start.ArgumentList.Add("/RootSuffix");
        start.ArgumentList.Add(rootSuffix);
        start.ArgumentList.Add(fixture.SolutionPath);
        return Process.Start(start)
            ?? throw new InvalidOperationException("Visual Studio did not start.");
    }

    private static void VerifyRename(
        Process process,
        RenameFixture fixture,
        RenameOptions options)
    {
        var deadline = DateTime.UtcNow + options.Timeout;
        dynamic dte = VisualStudioAutomation.WaitForDte(process, deadline);
        VisualStudioAutomation.WaitForSolution(dte, process, deadline);
        dte.ItemOperations.OpenFile(fixture.FirstComponentPath);
        VisualStudioAutomation.WaitForDocument(
            dte,
            fixture.FirstComponentPath,
            DateTime.UtcNow + TimeSpan.FromSeconds(30));

        dynamic selection = dte.ActiveDocument.Selection;
        selection.MoveToLineAndOffset(4, 33, false);
        VisualStudioAutomation.BringToForeground(process);
        dte.ExecuteCommand("Refactor.Rename");

        var nameBox = WaitForRenameTextBox(
            process,
            DateTime.UtcNow + TimeSpan.FromSeconds(20));
        VisualStudioAutomation.SetValue(nameBox, "RenamedProps");
        var renameButton = VisualStudioAutomation.FindElement(
            process,
            "Rename",
            VisualStudioControlType.Button)
            ?? throw new InvalidOperationException(
                "The FECSX Rename Symbol dialog did not expose its Rename button.");
        VisualStudioAutomation.Invoke(renameButton);

        WaitForRename(dte, fixture, DateTime.UtcNow + TimeSpan.FromSeconds(30));
        fixture.VerifyAuthoredFiles();
        var imagePath = Path.Combine(
            options.OutputDirectory,
            "fecsx-solution-rename.png");
        VisualStudioAutomation.SaveWindowScreenshot(process, imagePath);
        if (new FileInfo(imagePath).Length < 10_000)
        {
            throw new InvalidDataException("The rename screenshot is unexpectedly small.");
        }
        Console.WriteLine(
            "Verified solution-wide rename in Program.cs, First.fecsx, and "
            + "Second.fecsx without projection scaffolding.");
        Console.WriteLine($"Captured '{imagePath}'.");
    }

    private static VisualStudioAutomationElement WaitForRenameTextBox(
        Process process,
        DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            var element = VisualStudioAutomation.FindEditWithValue(
                process,
                "SharedProps");
            if (element != null)
            {
                return element;
            }
            Thread.Sleep(200);
        }
        throw new TimeoutException(
            "The FECSX Rename Symbol dialog did not expose the 'SharedProps' edit control.");
    }

    private static void WaitForRename(
        dynamic dte,
        RenameFixture fixture,
        DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(250);
            try
            {
                dte.ExecuteCommand("File.SaveAll");
            }
            catch (COMException)
            {
            }
            if (fixture.HasExpectedRename())
            {
                return;
            }
        }
        throw new TimeoutException(
            "Solution-wide rename did not update Program.cs, First.fecsx, and Second.fecsx.");
    }

    private sealed class RenameOptions
    {
        public required string OutputDirectory { get; init; }
        public string RootSuffix { get; set; } = "Exp";
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(180);
        public bool KeepFixture { get; set; }
        public bool CloseExisting { get; set; }

        public static RenameOptions Parse(IReadOnlyList<string> arguments)
        {
            if (arguments.Count == 0)
            {
                throw new ArgumentException(
                    "verify-solution-rename requires <output-directory> [options].");
            }
            var options = new RenameOptions
            {
                OutputDirectory = Path.GetFullPath(arguments[0]),
            };
            for (var index = 1; index < arguments.Count; index++)
            {
                var name = arguments[index];
                if (name == "--keep-fixture")
                {
                    options.KeepFixture = true;
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
            if (options.Timeout < TimeSpan.FromSeconds(30)
                || options.Timeout > TimeSpan.FromSeconds(300))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arguments),
                    "--timeout must be between 30 and 300 seconds.");
            }
            return options;
        }
    }

    private sealed class RenameFixture : IDisposable
    {
        private readonly bool _keepFixture;
        private bool _completed;

        private RenameFixture(string rootDirectory, bool keepFixture)
        {
            RootDirectory = rootDirectory;
            _keepFixture = keepFixture;
        }

        public string RootDirectory { get; }
        public string ComponentsDirectory => Path.Combine(RootDirectory, "Components");
        public string ProjectPath => Path.Combine(RootDirectory, "FecsxRenameFixture.csproj");
        public string SolutionPath => Path.Combine(RootDirectory, "FecsxRenameFixture.sln");
        public string ProgramPath => Path.Combine(RootDirectory, "Program.cs");
        public string FirstComponentPath => Path.Combine(ComponentsDirectory, "First.fecsx");
        public string SecondComponentPath => Path.Combine(ComponentsDirectory, "Second.fecsx");

        public static RenameFixture Create(bool keepFixture)
        {
            var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var root = Path.GetFullPath(Path.Combine(
                temp,
                "FrontEndCSharp-FecsxRename-" + Guid.NewGuid().ToString("N")));
            if (!root.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                || !System.Text.RegularExpressions.Regex.IsMatch(
                    Path.GetFileName(root),
                    "^FrontEndCSharp-FecsxRename-[0-9a-f]{32}$"))
            {
                throw new InvalidOperationException(
                    $"The disposable fixture path is outside the validated temp location: '{root}'.");
            }
            Directory.CreateDirectory(Path.Combine(root, "Components"));
            return new RenameFixture(root, keepFixture);
        }

        public void CreateSolution()
        {
            File.WriteAllText(ProjectPath, ProjectText);
            File.WriteAllText(ProgramPath, ProgramText);
            File.WriteAllText(FirstComponentPath, FirstText);
            File.WriteAllText(SecondComponentPath, SecondText);
            BoundedProcess.Run(
                "dotnet",
                RootDirectory,
                TimeSpan.FromSeconds(30),
                "new",
                "sln",
                "--name",
                "FecsxRenameFixture",
                "--output",
                RootDirectory,
                "--force");
            BoundedProcess.Run(
                "dotnet",
                RootDirectory,
                TimeSpan.FromSeconds(30),
                "sln",
                SolutionPath,
                "add",
                ProjectPath);
        }

        public bool HasExpectedRename() =>
            File.ReadAllText(ProgramPath).Contains(
                "class RenamedProps",
                StringComparison.Ordinal)
            && File.ReadAllText(FirstComponentPath).Contains(
                "Default(RenamedProps props",
                StringComparison.Ordinal)
            && File.ReadAllText(SecondComponentPath).Contains(
                "Default(RenamedProps props",
                StringComparison.Ordinal);

        public void VerifyAuthoredFiles()
        {
            foreach (var path in new[]
            {
                ProgramPath,
                FirstComponentPath,
                SecondComponentPath,
            })
            {
                var text = File.ReadAllText(path);
                if (text.Contains("SharedProps", StringComparison.Ordinal)
                    || text.Contains("__FecsxEditorProjection", StringComparison.Ordinal)
                    || text.Contains(".editor.g.cs", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Rename left the old name or projection scaffolding in '{path}'.");
                }
            }
            var generatedFiles = Directory.GetFiles(
                RootDirectory,
                "*.editor.g.cs",
                SearchOption.AllDirectories);
            if (generatedFiles.Length != 0)
            {
                throw new InvalidDataException(
                    "Rename wrote projection files to disk: "
                    + string.Join(", ", generatedFiles));
            }
        }

        public void MarkCompleted() => _completed = true;

        public void Dispose()
        {
            if (_completed && !_keepFixture)
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
            else if (Directory.Exists(RootDirectory))
            {
                Console.WriteLine($"Preserved rename fixture at '{RootDirectory}'.");
            }
        }

        private const string ProjectText = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <AdditionalFiles Include="Components\*.fecsx" />
              </ItemGroup>
            </Project>
            """;

        private const string ProgramText = """
            namespace Interop.JS.React
            {
                public sealed class ReactNode { }
            }

            namespace FecsxRenameFixture
            {
                public sealed class SharedStore
                {
                    public int Count { get; set; }
                }

                public sealed class SharedProps
                {
                    public SharedStore Store { get; set; } = new();
                }
            }
            """;

        private const string FirstText = """
            using Interop.JS.React;
            namespace FecsxRenameFixture;

            public static ReactNode Default(SharedProps props, ReactNode[] children) =>
                <section>{props.Store.Count}</section>;
            """;

        private const string SecondText = """
            using Interop.JS.React;
            namespace FecsxRenameFixture;

            public static ReactNode Default(SharedProps props, ReactNode[] children) =>
                <aside>{props.Store.Count}</aside>;
            """;
    }
}
