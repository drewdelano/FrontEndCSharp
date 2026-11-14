using System.IO.Compression;

namespace FrontEndCSharp.Tailwind;

public static class TailwindPayloadArchive
{
    private static readonly DateTimeOffset StableTimestamp =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Create(string sourceDirectory, string archivePath)
    {
        var source = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(source))
        {
            throw new TailwindBuildException(
                $"The Tailwind payload directory does not exist: '{source}'.");
        }
        var required = new[]
        {
            "package.json",
            "package-lock.json",
            "default-theme.css",
            "node_modules/@tailwindcss/cli/dist/index.mjs"
        };
        foreach (var relativePath in required)
        {
            var path = Path.Combine(source, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                throw new TailwindBuildException(
                    $"The Tailwind payload is incomplete; '{relativePath}' is missing.");
            }
        }

        var destination = Path.GetFullPath(archivePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                    .Where(path => !path.Contains(
                        Path.DirectorySeparatorChar + ".fecscn-work" + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal))
                    .OrderBy(path => Path.GetRelativePath(source, path), StringComparer.Ordinal))
                {
                    var relativePath = Path.GetRelativePath(source, file).Replace('\\', '/');
                    var entry = zip.CreateEntry(relativePath, CompressionLevel.Optimal);
                    entry.LastWriteTime = StableTimestamp;
                    using var input = File.OpenRead(file);
                    using var output = entry.Open();
                    input.CopyTo(output);
                }
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
