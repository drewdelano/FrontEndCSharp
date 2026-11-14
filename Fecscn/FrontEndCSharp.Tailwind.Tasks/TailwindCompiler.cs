using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace FrontEndCSharp.Tailwind;

public sealed record TailwindCompileRequest(
    string ProjectDirectory,
    string? ToolRoot,
    string? ToolArchive,
    string CacheDirectory,
    string? ThemeInputPath,
    string OutputPath,
    string StatePath,
    string Configuration,
    IReadOnlyList<string> Sources);

public enum TailwindCompileResult
{
    Built,
    Skipped
}

public sealed class TailwindBuildException : Exception
{
    public TailwindBuildException(string message) : base(message) { }
}

public static class TailwindCompiler
{
    public static async Task<TailwindCompileResult> CompileAsync(
        TailwindCompileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projectDirectory = FullDirectory(request.ProjectDirectory, "project");
        var cacheDirectory = Path.GetFullPath(request.CacheDirectory);
        var outputPath = Path.GetFullPath(request.OutputPath);
        var statePath = Path.GetFullPath(request.StatePath);
        var editorCatalogPath = Path.Combine(cacheDirectory, "editor-catalog.tsv.gz");
        var sources = request.Sources
            .Select(path => FullFile(path, "Tailwind source"))
            .Distinct(PathComparer)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        foreach (var source in sources)
        {
            EnsureUnder(source, projectDirectory, "Tailwind source");
            var extension = Path.GetExtension(source);
            if (!extension.Equals(".fecsx", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new TailwindBuildException(
                    $"Tailwind source '{source}' must be a .fecsx or .cs file.");
            }
        }

        Directory.CreateDirectory(cacheDirectory);
        var toolRoot = await ResolveToolRootAsync(request, cacheDirectory, cancellationToken);
        var cli = FullFile(
            Path.Combine(toolRoot, "node_modules", "@tailwindcss", "cli", "dist", "index.mjs"),
            "Tailwind CLI");
        var defaultTheme = FullFile(Path.Combine(toolRoot, "default-theme.css"), "fecscn default theme");
        var toolIdentity = !string.IsNullOrWhiteSpace(request.ToolArchive)
            ? await HashFileAsync(FullFile(request.ToolArchive, "Tailwind tool archive"), cancellationToken)
            : await HashFileAsync(
                FullFile(Path.Combine(toolRoot, "package-lock.json"), "Tailwind package lock"),
                cancellationToken);
        var themeInput = string.IsNullOrWhiteSpace(request.ThemeInputPath)
            ? null
            : FullFile(request.ThemeInputPath, "Tailwind theme input");
        if (themeInput is not null)
        {
            EnsureUnder(themeInput, projectDirectory, "Tailwind theme input");
        }

        var catalogFingerprint = await ComputeCatalogFingerprintAsync(
            request, defaultTheme, themeInput, toolIdentity, cancellationToken);
        var buildFingerprint = await ComputeBuildFingerprintAsync(
            request, sources, cli, catalogFingerprint, cancellationToken);
        var state = await ReadStateAsync(statePath, cancellationToken);
        var outputIsCurrent = File.Exists(outputPath)
            && string.Equals(state.BuildFingerprint, buildFingerprint, StringComparison.Ordinal);
        var catalogIsCurrent = File.Exists(editorCatalogPath)
            && string.Equals(state.CatalogFingerprint, catalogFingerprint, StringComparison.Ordinal);
        if (outputIsCurrent && catalogIsCurrent)
        {
            return TailwindCompileResult.Skipped;
        }

        var workDirectory = Path.Combine(toolRoot, ".fecscn-work");
        Directory.CreateDirectory(workDirectory);
        var nonce = Guid.NewGuid().ToString("N");
        var generatedInput = Path.Combine(workDirectory, $"input-{nonce}.css");
        var temporaryOutput = Path.Combine(cacheDirectory, $"output-{nonce}.css");
        var temporaryEditorCatalog = Path.Combine(
            cacheDirectory,
            $"editor-catalog-{nonce}.tsv.gz");
        try
        {
            var css = new StringBuilder()
                .AppendLine("@import \"tailwindcss\" source(none);")
                .AppendLine("@import \"tw-animate-css\";")
                .AppendLine("@import \"../default-theme.css\";");
            foreach (var source in sources)
            {
                css.Append("@source \"")
                    .Append(ToCssPath(source))
                    .AppendLine("\";");
            }
            if (themeInput is not null)
            {
                css.Append("@import \"")
                    .Append(ToCssPath(themeInput))
                    .AppendLine("\";");
            }
            await File.WriteAllTextAsync(generatedInput, css.ToString(), cancellationToken);

            if (!outputIsCurrent)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    WorkingDirectory = toolRoot,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add(cli);
                startInfo.ArgumentList.Add("--input");
                startInfo.ArgumentList.Add(generatedInput);
                startInfo.ArgumentList.Add("--output");
                startInfo.ArgumentList.Add(temporaryOutput);
                if (request.Configuration.Equals("Release", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo.ArgumentList.Add("--minify");
                }

                using var process = Process.Start(startInfo)
                    ?? throw new TailwindBuildException("Could not start the Node.js process for Tailwind CSS.");
                var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);
                var output = await stdout;
                var error = await stderr;
                if (process.ExitCode != 0)
                {
                    throw new TailwindBuildException(
                        $"Tailwind CSS exited with code {process.ExitCode}. {error}{output}".Trim());
                }
                if (!File.Exists(temporaryOutput))
                {
                    throw new TailwindBuildException("Tailwind CSS did not produce the requested output file.");
                }
            }

            if (!catalogIsCurrent)
            {
                TailwindEditorCatalogGenerator.Generate(
                    toolRoot,
                    temporaryEditorCatalog,
                    generatedInput);
            }

            if (!outputIsCurrent)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.Move(temporaryOutput, outputPath, overwrite: true);
            }
            if (!catalogIsCurrent)
            {
                File.Move(temporaryEditorCatalog, editorCatalogPath, overwrite: true);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            await WriteAtomicallyAsync(
                statePath,
                "build=" + buildFingerprint + Environment.NewLine
                    + "catalog=" + catalogFingerprint + Environment.NewLine,
                cancellationToken);
            return TailwindCompileResult.Built;
        }
        finally
        {
            File.Delete(generatedInput);
            File.Delete(temporaryOutput);
            File.Delete(temporaryEditorCatalog);
            if (Directory.Exists(workDirectory)
                && !Directory.EnumerateFileSystemEntries(workDirectory).Any())
            {
                Directory.Delete(workDirectory);
            }
        }
    }

    private static async Task<string> ResolveToolRootAsync(
        TailwindCompileRequest request,
        string cacheDirectory,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ToolRoot))
        {
            return FullDirectory(request.ToolRoot, "Tailwind tool payload");
        }
        var archive = FullFile(request.ToolArchive ?? string.Empty, "Tailwind tool archive");
        var archiveHash = await HashFileAsync(archive, cancellationToken);
        var destination = Path.Combine(cacheDirectory, "payload", archiveHash);
        var complete = Path.Combine(destination, ".complete");
        if (File.Exists(complete))
        {
            return destination;
        }

        var staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(staging);
        try
        {
            using var zip = ZipFile.OpenRead(archive);
            var stagingRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                if (!target.StartsWith(stagingRoot, PathComparison))
                {
                    throw new TailwindBuildException(
                        $"Tailwind tool archive entry '{entry.FullName}' escapes the extraction directory.");
                }
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: false);
            }
            await File.WriteAllTextAsync(Path.Combine(staging, ".complete"), archiveHash, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            try
            {
                Directory.Move(staging, destination);
            }
            catch (IOException) when (Directory.Exists(destination))
            {
                Directory.Delete(staging, recursive: true);
            }
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
            throw;
        }
    }

    private static async Task<string> ComputeBuildFingerprintAsync(
        TailwindCompileRequest request,
        IReadOnlyList<string> sources,
        string cli,
        string catalogFingerprint,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value) => hash.AppendData(Encoding.UTF8.GetBytes(value + "\0"));
        Add("FrontEndCSharp.Tailwind/v1");
        Add(request.Configuration);
        Add(catalogFingerprint);
        Add(await HashFileAsync(cli, cancellationToken));
        foreach (var source in sources)
        {
            Add(Path.GetRelativePath(request.ProjectDirectory, source).Replace('\\', '/'));
            Add(await HashFileAsync(source, cancellationToken));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task<string> ComputeCatalogFingerprintAsync(
        TailwindCompileRequest request,
        string defaultTheme,
        string? themeInput,
        string toolIdentity,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value) => hash.AppendData(Encoding.UTF8.GetBytes(value + "\0"));
        Add("FrontEndCSharp.Tailwind.EditorCatalog/v1");
        Add(toolIdentity);
        Add(await HashFileAsync(defaultTheme, cancellationToken));
        Add(await HashFileAsync(typeof(TailwindCompiler).Assembly.Location, cancellationToken));
        if (themeInput is not null)
        {
            foreach (var configurationFile in GetCssConfigurationFiles(themeInput))
            {
                Add(Path.GetRelativePath(request.ProjectDirectory, configurationFile).Replace('\\', '/'));
                Add(await HashFileAsync(configurationFile, cancellationToken));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task<TailwindBuildState> ReadStateAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new TailwindBuildState(null, null);
        }

        string? build = null;
        string? catalog = null;
        foreach (var line in await File.ReadAllLinesAsync(path, cancellationToken))
        {
            if (line.StartsWith("build=", StringComparison.Ordinal))
            {
                build = line.Substring("build=".Length);
            }
            else if (line.StartsWith("catalog=", StringComparison.Ordinal))
            {
                catalog = line.Substring("catalog=".Length);
            }
        }
        return new TailwindBuildState(build, catalog);
    }

    private sealed record TailwindBuildState(
        string? BuildFingerprint,
        string? CatalogFingerprint);

    private static IReadOnlyList<string> GetCssConfigurationFiles(string entryPath)
    {
        var discovered = new HashSet<string>(PathComparer);
        var pending = new Stack<string>();
        pending.Push(entryPath);
        while (pending.Count > 0)
        {
            var path = Path.GetFullPath(pending.Pop());
            if (!discovered.Add(path))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(path)!;
            var css = File.ReadAllText(path);
            foreach (System.Text.RegularExpressions.Match match in
                System.Text.RegularExpressions.Regex.Matches(
                    css,
                    "@(import|reference)\\s+(?:url\\(\\s*)?[\"'](?<path>[^\"']+)[\"']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                var import = match.Groups["path"].Value;
                if (!import.StartsWith(".", StringComparison.Ordinal)
                    && !Path.IsPathRooted(import))
                {
                    continue;
                }

                var candidate = Path.IsPathRooted(import)
                    ? import
                    : Path.Combine(directory, import.Replace('/', Path.DirectorySeparatorChar));
                candidate = ResolveCssImport(candidate);
                if (candidate is not null)
                {
                    pending.Push(candidate);
                }
            }
        }

        return discovered.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static string? ResolveCssImport(string path)
    {
        foreach (var candidate in new[]
        {
            path,
            path + ".css",
            Path.Combine(path, "index.css"),
        })
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }
        return null;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task WriteAtomicallyAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temporary, content, cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }

    private static string FullDirectory(string path, string description)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new TailwindBuildException($"The {description} directory does not exist: '{fullPath}'.");
        }
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string FullFile(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new TailwindBuildException($"The {description} path is required.");
        }
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new TailwindBuildException($"The {description} file does not exist: '{fullPath}'.");
        }
        return fullPath;
    }

    private static void EnsureUnder(string path, string root, string description)
    {
        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSeparator, PathComparison))
        {
            throw new TailwindBuildException($"The {description} is outside the project directory: '{path}'.");
        }
    }

    private static string ToCssPath(string path) =>
        Path.GetFullPath(path).Replace('\\', '/').Replace("\"", "\\\"");

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
