using System.IO.Compression;
using System.Security.Cryptography;

namespace FrontEndCSharp.Debugger.Integration;

internal static class VsixBuildModeVerifier
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(10);

    public static void Verify(string repositoryRoot)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "VSIX build-mode verification requires Visual Studio on Windows.");
        }
        var root = Path.GetFullPath(repositoryRoot);
        var project = Path.Combine(
            root,
            "Debugger",
            "VisualStudio",
            "FrontEndCSharp.VisualStudio.csproj");
        var vsix = Path.Combine(
            root,
            "Debugger",
            "VisualStudio",
            "bin",
            "Release",
            "net472",
            "FrontEndCSharp.VisualStudio.vsix");
        var compilerBundler = Path.Combine(
            root,
            "Compiler",
            "Core",
            "bin",
            "Release",
            "net8.0",
            "JsBundler",
            "index.cjs");
        if (!File.Exists(project))
        {
            throw new FileNotFoundException("The debugger VSIX project was not found.", project);
        }

        var msBuild = VisualStudioAutomation.FindMSBuild();
        RunMSBuild(msBuild, root, project, "Clean");
        var fresh = BuildAndVerify(msBuild, root, project, vsix, compilerBundler, rebuild: false);
        var noOp = BuildAndVerify(msBuild, root, project, vsix, compilerBundler, rebuild: false);
        var rebuilt = BuildAndVerify(msBuild, root, project, vsix, compilerBundler, rebuild: true);

        RequireEqual(fresh, noOp, "fresh and no-op");
        RequireEqual(fresh, rebuilt, "fresh and rebuild");
        Console.WriteLine(
            $"Verified fresh, no-op, and rebuild debugger VSIX payloads with "
            + $"JsBundler/index.cjs SHA-256 {fresh}.");
    }

    private static string BuildAndVerify(
        string msBuild,
        string root,
        string project,
        string vsix,
        string compilerBundler,
        bool rebuild)
    {
        RunMSBuild(msBuild, root, project, rebuild ? "Rebuild" : "Build");

        VsixArchiveVerifier.Verify(vsix);
        var outputHash = Hash(File.ReadAllBytes(compilerBundler));
        using var archive = ZipFile.OpenRead(vsix);
        var directPayload = ReadEntry(archive, "CompilerPayload.zip");
        var buildPackage = ReadEntry(archive, "Build/FrontEndCSharp.Build.nupkg");
        var directHash = Hash(ReadNestedEntry(directPayload, "JsBundler/index.cjs"));
        var packagedPayload = ReadNestedEntry(buildPackage, "tools/CompilerPayload.zip");
        var packagedHash = Hash(ReadNestedEntry(packagedPayload, "JsBundler/index.cjs"));
        RequireEqual(outputHash, directHash, "compiler output and direct VSIX payload");
        RequireEqual(outputHash, packagedHash, "compiler output and build-package payload");
        return outputHash;
    }

    private static byte[] ReadNestedEntry(byte[] archiveBytes, string entryName)
    {
        using var stream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        return ReadEntry(archive, entryName);
    }

    private static byte[] ReadEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.Entries.Single(candidate => string.Equals(
            candidate.FullName.Replace('\\', '/'),
            entryName,
            StringComparison.OrdinalIgnoreCase));
        using var input = entry.Open();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static string Hash(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content));

    private static void RequireEqual(string expected, string actual, string comparison)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Debugger VSIX {comparison} bundler hashes differ: '{expected}' and '{actual}'.");
        }
    }

    private static void RunMSBuild(
        string msBuild,
        string workingDirectory,
        string project,
        string target) =>
        _ = BoundedProcess.Run(
            msBuild,
            workingDirectory,
            BuildTimeout,
            project,
            "/t:" + target,
            "/p:Configuration=Release",
            "/p:DeployExtension=false",
            "/v:minimal");
}
