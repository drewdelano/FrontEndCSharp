using System.Security;
using System.Security.Cryptography;
using System.Text.Json;

namespace FrontEndCSharp.Debugger.Integration;

internal static class BuildPackageVerifier
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(5);

    public static void Verify(string path, string repositoryRoot)
    {
        var packagePath = Path.GetFullPath(path);
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("The build package does not exist.", packagePath);
        }
        var repository = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(repository))
        {
            throw new DirectoryNotFoundException(
                $"The repository root does not exist: '{repository}'.");
        }
        var packageDirectory = Path.GetDirectoryName(packagePath)!;
        var packageName = Path.GetFileNameWithoutExtension(packagePath);
        const string packagePrefix = "FrontEndCSharp.Build.";
        if (!packageName.StartsWith(packagePrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Build package '{packageName}' does not start with '{packagePrefix}'.");
        }
        var version = packageName[packagePrefix.Length..];
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp_BuildPackage_" + Guid.NewGuid().ToString("N"));
        var localCompilerDirectory = Path.Combine(
            Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\",
            "FrontEndCSharp",
            "Verification",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            var projectPath = Path.Combine(temporaryDirectory, "PackageConsumer.csproj");
            var outputPath = Path.Combine(temporaryDirectory, "wwwroot", "js");
            var frontEndProject = Path.Combine(
                repository,
                "Compiler",
                "Compiler.Tests",
                "Fixtures",
                "PortablePdbFixture",
                "PortablePdbFixture.csproj");
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "Program.cs"),
                "System.Console.WriteLine(\"host\");");
            File.WriteAllText(projectPath, $$"""
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <RestoreSources>{{Escape(packageDirectory)}}</RestoreSources>
                    <RestorePackagesPath>{{Escape(Path.Combine(temporaryDirectory, "packages"))}}</RestorePackagesPath>
                    <FrontEndCSharpProject>{{Escape(frontEndProject)}}</FrontEndCSharpProject>
                    <FrontEndCSharpOutputPath>{{Escape(outputPath)}}</FrontEndCSharpOutputPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="FrontEndCSharp.Build" Version="{{version}}" />
                  </ItemGroup>
                </Project>
                """);

            RunDotNet(repository, "restore", frontEndProject, "--nologo", "-v:minimal",
                "-p:RestoreFallbackFolders=");
            RunDotNet(temporaryDirectory, "build", projectPath, "--nologo", "-v:minimal");

            var entryPoint = RequireFile(outputPath, "index.js");
            var sourceMapPath = RequireFile(outputPath, "index.js.map");
            using (var sourceMap = JsonDocument.Parse(File.ReadAllText(sourceMapPath)))
            {
                var temporarySources = sourceMap.RootElement.GetProperty("sources")
                    .EnumerateArray()
                    .Select(source => source.GetString() ?? string.Empty)
                    .Where(source => source.Contains(
                        "FrontEndCSharp-Output-",
                        StringComparison.Ordinal))
                    .ToArray();
                if (temporarySources.Length != 0)
                {
                    throw new InvalidDataException(
                        "The generated source map exposes deleted temporary staging paths: "
                        + string.Join(", ", temporarySources));
                }
            }

            var binaryPackage = RequireFile(outputPath, "msil.bin");
            var hashedBinaryPackages = Directory.EnumerateFiles(outputPath, "msil.*.bin").ToArray();
            if (hashedBinaryPackages.Length != 0)
            {
                throw new InvalidDataException(
                    $"The first build emitted {hashedBinaryPackages.Length} obsolete hashed MSIL package files.");
            }
            var binaryHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(binaryPackage)))
                .ToLowerInvariant();
            var binaryUrl = "msil.bin?v=" + binaryHash;
            if (!File.ReadAllText(entryPoint).Contains(binaryUrl, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The generated entry point does not reference '{binaryUrl}'.");
            }

            var objDirectory = Path.Combine(temporaryDirectory, "obj");
            var staticWebAssetsManifest = FindFile(objDirectory, "staticwebassets.build.json")
                ?? throw new FileNotFoundException(
                    "The first build did not create a static web assets manifest.");
            if (!File.ReadAllText(staticWebAssetsManifest).Contains(
                "msil.bin",
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The first build did not register 'msil.bin' as a static web asset.");
            }

            var generationStamp = FindFile(objDirectory, "browser-output.stamp")
                ?? throw new FileNotFoundException(
                    "The build package did not create a dedicated browser-generation stamp.");
            var entryPointWriteTime = File.GetLastWriteTimeUtc(entryPoint);
            var binaryPackageWriteTime = File.GetLastWriteTimeUtc(binaryPackage);
            var generationState = File.ReadAllText(generationStamp);
            RunDotNet(temporaryDirectory, "build", projectPath, "--nologo", "-v:minimal");
            if (File.GetLastWriteTimeUtc(entryPoint) != entryPointWriteTime
                || File.GetLastWriteTimeUtc(binaryPackage) != binaryPackageWriteTime
                || !string.Equals(
                    File.ReadAllText(generationStamp),
                    generationState,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "An unchanged PackageReference consumer reran browser compilation or bundling.");
            }
            var compilerPathFile = FindFile(objDirectory, "compiler.path")
                ?? throw new FileNotFoundException(
                    "The build package did not record its extracted compiler path.");
            var compilerPath = File.ReadAllText(compilerPathFile).Trim();
            if (!File.Exists(compilerPath))
            {
                throw new FileNotFoundException(
                    "The extracted compiler does not exist.",
                    compilerPath);
            }
            ValidateCompilerCachePath(compilerPath);

            CopyDirectory(Path.GetDirectoryName(compilerPath)!, localCompilerDirectory);
            var localCompilerPath = Path.Combine(localCompilerDirectory, "Compiler.dll");
            var project = File.ReadAllText(projectPath).Replace(
                "<FrontEndCSharpProject>",
                $"<FrontEndCSharpCompilerPath>{Escape(localCompilerPath)}</FrontEndCSharpCompilerPath>"
                + Environment.NewLine + "    <FrontEndCSharpProject>",
                StringComparison.Ordinal);
            File.WriteAllText(projectPath, project);
            RunDotNet(temporaryDirectory, "build", projectPath, "--nologo", "-v:minimal");

            var runtimeSource = Path.Combine(localCompilerDirectory, "JsRuntime", "msil.js");
            var marker = "incremental-runtime-marker-" + Guid.NewGuid().ToString("N");
            File.AppendAllText(
                runtimeSource,
                Environment.NewLine
                + $"globalThis.__FrontEndCSharpIncrementalRuntimeMarker = '{marker}';"
                + Environment.NewLine);
            var now = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(generationStamp, now.AddSeconds(-10));
            File.SetLastWriteTimeUtc(runtimeSource, now.AddSeconds(-5));
            File.SetLastWriteTimeUtc(entryPoint, now);
            RunDotNet(temporaryDirectory, "build", projectPath, "--nologo", "-v:minimal");
            if (!File.ReadAllText(entryPoint).Contains(marker, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The webpack bundle did not include a runtime-only compiler payload update.");
            }
            if (File.Exists(Path.Combine(outputPath, "msil.js")))
            {
                throw new InvalidDataException(
                    "Bundled output retained the temporary JavaScript runtime source.");
            }

            Console.WriteLine(
                $"Verified FrontEndCSharp.Build {version} through a PackageReference consumer using {compilerPath}");
        }
        finally
        {
            DeleteDirectory(temporaryDirectory);
            DeleteDirectory(localCompilerDirectory);
        }
    }

    private static void RunDotNet(string workingDirectory, params string[] arguments) =>
        BoundedProcess.Run("dotnet", workingDirectory, ProcessTimeout, arguments);

    private static string RequireFile(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The build package did not generate '{path}'.",
                path);
        }
        return path;
    }

    private static string? FindFile(string directory, string name) =>
        Directory.EnumerateFiles(directory, name, SearchOption.AllDirectories).FirstOrDefault();

    private static void ValidateCompilerCachePath(string compilerPath)
    {
        var systemRoot = Path.Combine(
            Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\",
            "FrontEndCSharp") + Path.DirectorySeparatorChar;
        var fallbackRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FEC") + Path.DirectorySeparatorChar;
        if (!compilerPath.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase)
            && !compilerPath.StartsWith(fallbackRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The compiler was not extracted beneath an approved short cache root: '{compilerPath}'.");
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var output = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.Copy(file, output, overwrite: true);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
