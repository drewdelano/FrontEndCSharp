namespace FrontEndCSharp.Compiler.Integration;

internal static class FecsxReactDemoBrowserSmoke
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);

    public static void Run(string browserPath, bool noBuild)
    {
        RunAsync(browserPath, noBuild).GetAwaiter().GetResult();
    }

    private static async Task RunAsync(string browserPath, bool noBuild)
    {
        browserPath = Path.GetFullPath(browserPath);
        if (!File.Exists(browserPath))
        {
            throw new FileNotFoundException("The Chromium browser does not exist.", browserPath);
        }
        var repositoryRoot = RepositoryLayout.Root;
        var demoRoot = Path.Combine(repositoryRoot, "Demo", "FrontEndCSharp.ReactDemo");
        var demoProject = Path.Combine(demoRoot, "FrontEndCSharp.ReactDemo.csproj");
        var hostProject = Path.Combine(
            repositoryRoot,
            "Demo",
            "FrontEndCSharp.Demo",
            "FrontEndCSharp.Demo.csproj");
        var demoAssembly = Path.Combine(
            demoRoot,
            "bin",
            "Debug",
            "net8.0",
            "FrontEndCSharp.ReactDemo.dll");
        var compilerProject = Path.Combine(
            repositoryRoot,
            "Compiler",
            "Core",
            "Compiler.csproj");
        var compilerPath = Path.Combine(
            repositoryRoot,
            "Compiler",
            "Core",
            "bin",
            "Debug",
            "net8.0",
            "Compiler.dll");
        var generatedOutput = CreateTemporaryDirectory("FrontEndCSharp-FecsxDemo-Smoke-");
        var browserProfile = CreateTemporaryDirectory("FrontEndCSharp-FecsxDemo-Browser-");
        try
        {
            if (!noBuild)
            {
                RunDotNet(repositoryRoot, "build", compilerProject, "--configuration", "Debug");
                RunDotNet(repositoryRoot, "build", hostProject, "--configuration", "Debug");
            }
            var compilerStopwatch = System.Diagnostics.Stopwatch.StartNew();
            RunDotNet(
                repositoryRoot,
                compilerPath,
                demoProject,
                generatedOutput,
                demoAssembly);
            compilerStopwatch.Stop();
            using var server = new StaticFileServer(generatedOutput);
            File.WriteAllText(
                Path.Combine(generatedOutput, "index.html"),
                "<!doctype html><html class=\"dark\"><head><meta charset=\"utf-8\">"
                + "<link rel=\"stylesheet\" href=\"/react-demo.css\">"
                + "<link rel=\"stylesheet\" href=\"/fecscn.css\"></head><body>"
                + $"<div id=\"app\" data-fecs-smoke=\"true\"></div>"
                + "<script type=\"module\" src=\"/index.js\"></script></body></html>"
                + Environment.NewLine);
            File.Copy(
                Path.Combine(
                    repositoryRoot,
                    "Demo",
                    "FrontEndCSharp.Demo",
                    "wwwroot",
                    "css",
                    "react-demo.css"),
                Path.Combine(generatedOutput, "react-demo.css"));
            File.Copy(
                Path.Combine(
                    repositoryRoot,
                    "Demo",
                    "FrontEndCSharp.Demo",
                    "wwwroot",
                    "css",
                    "fecscn.css"),
                Path.Combine(generatedOutput, "fecscn.css"));

            var observation = await MotionObserver.ObserveAsync(
                browserPath,
                browserProfile,
                server.Url);
            foreach (var marker in new[]
            {
                "data-orbit-status=\"ready\"",
                "data-fecsx-react=\"ready\"",
                "data-fecsx-mobx=\"ready\"",
                "data-fecsx-motion=\"ready\"",
            })
            {
                Require(observation.Dom, marker, $"FECSX demo did not report {marker}.");
            }
            Require(observation.Dom, "Pause orbit", "FECSX component content was absent.");
            Require(
                observation.Dom,
                "weather-live-icon",
                "The weather panel Cloud Lightning icon was absent.");
            Require(
                observation.Dom,
                "intent-title-icon",
                "The focus-intent panel Activity icon was absent.");
            Require(
                observation.Dom,
                "section-title-icon",
                "The anchors panel Crosshair icon was absent.");
            Require(
                observation.Dom,
                "data-slot=\"input\"",
                "The fecscn Base UI input was absent.");
            Require(
                observation.Dom,
                "data-slot=\"button\"",
                "The fecscn Base UI button was absent.");
            if (!string.Equals(observation.Fecscn.CardBorderRadius, "14px", StringComparison.Ordinal)
                || !string.Equals(observation.Fecscn.InputHeight, "32px", StringComparison.Ordinal)
                || !string.Equals(observation.Fecscn.ButtonDisplay, "flex", StringComparison.Ordinal)
                || !string.Equals(observation.Fecscn.ButtonHeight, "32px", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The React demo did not apply the generated fecscn Tailwind styles: "
                    + $"card radius {observation.Fecscn.CardBorderRadius}, "
                    + $"input height {observation.Fecscn.InputHeight}, "
                    + $"button display {observation.Fecscn.ButtonDisplay}, "
                    + $"button height {observation.Fecscn.ButtonHeight}.");
            }
            Require(
                observation.Dom,
                "Shape the product narrative",
                "Observed MobX content was absent.");
            Require(
                observation.Dom,
                "Motion makes state visible",
                "Motion-composed content was absent.");
            if (!string.Equals(observation.JsxTextParity, "Alpha & beta 🚀", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "FECSX JSX child text did not match the TypeScript/React whitespace and entity contract: "
                    + observation.JsxTextParity);
            }
            Require(
                observation.Dom,
                "Intent saved: Ship React validation",
                "The valid managed form result was absent.");
            if (!string.Equals(
                observation.FormValidation.InvalidFocusIntentError,
                "Name one focus outcome.",
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The React demo returned an unexpected focus-intent error: "
                    + observation.FormValidation.InvalidFocusIntentError);
            }
            if (!string.Equals(
                observation.FormValidation.InvalidAccountabilityEmailError,
                "Add an accountability email.",
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The React demo returned an unexpected accountability-email error: "
                    + observation.FormValidation.InvalidAccountabilityEmailError);
            }
            if (!string.Equals(
                observation.FormValidation.InvalidPriorityError,
                "Choose a priority from 1 to 3.",
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The React demo returned an unexpected priority error: "
                    + observation.FormValidation.InvalidPriorityError);
            }
            if (!string.Equals(
                observation.FormValidation.OutOfRangePriorityValue,
                "5",
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The React demo did not retain the out-of-range priority input: "
                    + observation.FormValidation.OutOfRangePriorityValue);
            }
            if (!string.Equals(
                observation.FormValidation.ValidResult,
                "Intent saved: Ship React validation",
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The React demo returned an unexpected valid-form result: "
                    + observation.FormValidation.ValidResult);
            }
            Console.WriteLine(
                "Verified FECSX, fecscn/Tailwind, MobX observation, managed form validation, and computed Motion entrance/hover transitions in Chromium.");
            Console.WriteLine(
                $"ReactDemo timing: compiler {compilerStopwatch.Elapsed.TotalMilliseconds:0.0} ms; "
                + $"browser launch-to-ready {observation.ReadyMilliseconds:0.0} ms.");
        }
        finally
        {
            Directory.Delete(generatedOutput, recursive: true);
            Directory.Delete(browserProfile, recursive: true);
        }
    }

    private static void Require(string text, string marker, string message)
    {
        if (!text.Contains(marker, StringComparison.Ordinal))
        {
            throw new InvalidDataException(message + "\nDOM:\n" + text);
        }
    }

    private static string CreateTemporaryDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void RunDotNet(string workingDirectory, params string[] arguments) =>
        BoundedProcess.Run("dotnet", workingDirectory, BuildTimeout, arguments);
}
