using System.IO.Compression;

namespace FrontEndCSharp.Debugger.Integration;

internal static class VsixArchiveVerifier
{
    private static readonly string[] RequiredVsixEntries =
    {
        "extension.vsixmanifest",
        "AdapterRegistration.pkgdef",
        "FrontEndCSharp.VisualStudio.dll",
        "Adapter/FrontEndCSharp.DebugAdapter.exe",
        "Adapter/FrontEndCSharp.DebugAdapter.dll",
        "Adapter/FrontEndCSharp.DebugAdapter.deps.json",
        "Adapter/FrontEndCSharp.DebugAdapter.runtimeconfig.json",
        "Adapter/Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.dll",
        "Adapter/Newtonsoft.Json.dll",
        "Build/FrontEndCSharp.targets",
        "Build/FrontEndCSharp.FECS.Internals.Build.nupkg",
        "ProjectTemplates/templateManifest0.1033.vstman",
        "CompilerPayload.zip",
    };

    private static readonly string[] RequiredRegistrationFragments =
    {
        @"[$RootKey$\CLSID\{FC152D13-6E45-44C6-B80C-25A153B931F8}]",
        @"""Class""=""FrontEndCSharp.VisualStudio.JavaScriptDebuggerProxyLauncher""",
        @"""CodeBase""=""$PackageFolder$\FrontEndCSharp.VisualStudio.dll""",
        @"""AdapterLauncher""=""{FC152D13-6E45-44C6-B80C-25A153B931F8}""",
        @"""LanguageId""=""3F5162F8-07C6-11D3-9053-00C04FA302A1""",
        "ExceptionBreakpointMappings",
        @"""Thrown managed exceptions""=""frontendcsharp-thrown""",
        @"""Unhandled managed exceptions""=""frontendcsharp-unhandled""",
        @"""FrontEndCSharp: thrown managed exceptions""=""frontendcsharp-thrown""",
        @"""FrontEndCSharp: unhandled managed exceptions""=""frontendcsharp-unhandled""",
        @"[$RootKey$\AD7Metrics\Exception\{1CE4D9F7-FAAC-42EE-B352-87FDFB6FB37B}\JavaScript Exceptions\FrontEndCSharp: thrown managed exceptions]",
        @"[$RootKey$\AD7Metrics\Exception\{1CE4D9F7-FAAC-42EE-B352-87FDFB6FB37B}\JavaScript Exceptions\FrontEndCSharp: unhandled managed exceptions]",
        "ExceptionCategoryMappings",
        @"""FrontEndCSharp""=""4DCFA4A3-9B82-4CB6-A98A-6490AF36E702""",
    };

    private static readonly string[] RequiredTemplateEntries =
    {
        "FrontEndCSharp.Browser.vstemplate",
        "Host/Host.vstemplate",
        "Host/Host.csproj",
        "Host/Program.cs",
        "Host/Properties/launchSettings.json",
        "Host/wwwroot/index.html",
        "Browser/Browser.vstemplate",
        "Browser/Browser.csproj",
        "Browser/Program.cs",
    };

    private const string ProjectTemplatePrefix =
        "ProjectTemplates/CSharp/1033/FrontEndCSharp.Browser/";

    private static readonly string[] RequiredPayloadEntries =
    {
        "Compiler.exe",
        "Compiler.dll",
        "Compiler.deps.json",
        "Compiler.runtimeconfig.json",
        "Newtonsoft.Json.dll",
        "System.Reflection.MetadataLoadContext.dll",
        "JsRuntime/msil.js",
        "JsRuntime/managed-debugger.js",
        "JsRuntime/jit-engine/_executeInstruction.js",
        "JsBundler/index.cjs",
        "EditAndContinue/FrontEndCSharp.EditAndContinue.exe",
        "EditAndContinue/FrontEndCSharp.EditAndContinue.dll",
        "EditAndContinue/FrontEndCSharp.EditAndContinue.deps.json",
        "EditAndContinue/FrontEndCSharp.EditAndContinue.runtimeconfig.json",
        "EditAndContinue/Microsoft.CodeAnalysis.dll",
        "EditAndContinue/Microsoft.CodeAnalysis.Workspaces.MSBuild.dll",
    };

    private static readonly string[] RequiredBuildPackagePayloadEntries =
        RequiredPayloadEntries.Where(entry => !entry.StartsWith(
            "EditAndContinue/",
            StringComparison.OrdinalIgnoreCase)).ToArray();

    private static readonly string[] RequiredBuildPackageEntries =
    {
        "buildTransitive/FrontEndCSharp.FECS.Internals.Build.props",
        "buildTransitive/FrontEndCSharp.FECS.Internals.Build.targets",
        "tools/CompilerPayload.zip",
        "tools/bootstrap/FrontEndCSharp.Build.Tasks.dll",
        "tools/bootstrap/FrontEndCSharp.Build.Tasks.deps.json",
        "tools/bootstrap/FrontEndCSharp.Build.Tasks.runtimeconfig.json",
    };

    public static void Verify(string path)
    {
        var resolvedPath = Path.GetFullPath(path);
        using var archive = ZipFile.OpenRead(resolvedPath);
        var entries = GetEntries(archive);
        RequireEntries(entries, RequiredVsixEntries, "VSIX");
        RequireEntries(
            entries,
            RequiredTemplateEntries.Select(entry => ProjectTemplatePrefix + entry),
            "Project template");

        var misplaced = entries.Where(entry =>
            entry.StartsWith("Adapter/JsRuntime/", StringComparison.OrdinalIgnoreCase)
            || entry.StartsWith("Compiler/", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (misplaced.Length != 0)
        {
            throw new InvalidDataException(
                "VSIX contains misplaced runtime entries:\n  " + string.Join("\n  ", misplaced));
        }

        var registration = ReadText(GetEntry(archive, "AdapterRegistration.pkgdef"));
        var activeRegistration = string.Join(
            "\n",
            registration.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => !line.TrimStart().StartsWith(";", StringComparison.Ordinal)));
        var missingRegistration = RequiredRegistrationFragments.Where(fragment =>
            !activeRegistration.Contains(fragment, StringComparison.Ordinal)).ToArray();
        if (missingRegistration.Length != 0)
        {
            throw new InvalidDataException(
                "Adapter registration is missing required debugger-host mappings:\n  "
                + string.Join("\n  ", missingRegistration));
        }
        if (activeRegistration.Contains("CustomProtocolExtension", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The product VSIX must leave Microsoft's CustomProtocolExtension registration untouched.");
        }
        if (entries.Any(entry => entry.EndsWith(
            "DebugAdapterHost.Interfaces.dll",
            StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "The build-time-only Debug Adapter Host interface assembly must not be packaged.");
        }

        var templateEntries = entries.Where(entry => entry.StartsWith(
            ProjectTemplatePrefix,
            StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var payloadEntries = ReadNestedEntries(
            GetEntry(archive, "CompilerPayload.zip"),
            RequiredPayloadEntries,
            "Compiler payload");
        var buildPackageEntries = ReadNestedEntries(
            GetEntry(archive, "Build/FrontEndCSharp.FECS.Internals.Build.nupkg"),
            RequiredBuildPackageEntries,
            "Build package");
        var buildPackagePayloadEntries = ReadNestedArchiveEntries(
            GetEntry(archive, "Build/FrontEndCSharp.FECS.Internals.Build.nupkg"),
            "tools/CompilerPayload.zip",
            RequiredBuildPackagePayloadEntries,
            "Build-package compiler payload");
        var expandedRuntime = buildPackageEntries.Where(entry => entry.StartsWith(
            "tools/net8.0/JsRuntime/",
            StringComparison.OrdinalIgnoreCase)).ToArray();
        if (expandedRuntime.Length != 0)
        {
            throw new InvalidDataException(
                "Build package expanded long-path runtime entries instead of using CompilerPayload.zip:\n  "
                + string.Join("\n  ", expandedRuntime));
        }

        Console.WriteLine(
            $"Verified {entries.Count} VSIX entries, {payloadEntries.Count} compiler payload entries, "
            + $"{buildPackageEntries.Count} build-package entries, {buildPackagePayloadEntries.Count} "
            + $"build-package payload entries, and {templateEntries.Count} project-template entries "
            + $"in {resolvedPath}");
    }

    private static HashSet<string> ReadNestedArchiveEntries(
        ZipArchiveEntry outerEntry,
        string nestedEntryName,
        IReadOnlyCollection<string> required,
        string description)
    {
        using var stream = outerEntry.Open();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        return ReadNestedEntries(
            GetEntry(archive, nestedEntryName),
            required,
            description);
    }

    private static HashSet<string> ReadNestedEntries(
        ZipArchiveEntry entry,
        IReadOnlyCollection<string> required,
        string description)
    {
        using var stream = entry.Open();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var entries = GetEntries(archive);
        RequireEntries(entries, required, description);
        return entries;
    }

    private static HashSet<string> GetEntries(ZipArchive archive) => archive.Entries
        .Select(entry => entry.FullName.Replace('\\', '/'))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void RequireEntries(
        IReadOnlySet<string> entries,
        IEnumerable<string> required,
        string description)
    {
        var missing = required.Where(entry => !entries.Contains(entry)).ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidDataException(
                $"{description} is missing required entries:\n  " + string.Join("\n  ", missing));
        }
    }

    private static ZipArchiveEntry GetEntry(ZipArchive archive, string name) =>
        archive.Entries.Single(entry => string.Equals(
            entry.FullName.Replace('\\', '/'),
            name,
            StringComparison.OrdinalIgnoreCase));

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
