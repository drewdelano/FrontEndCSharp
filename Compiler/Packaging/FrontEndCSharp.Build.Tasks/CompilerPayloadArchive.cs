using System.IO.Compression;

namespace FrontEndCSharp.Packaging;

public static class CompilerPayloadArchive
{
    public static void Create(
        string sourceDirectory,
        string destinationPath,
        string? editAndContinueSourceDirectory = null)
    {
        var source = ResolveDirectory(sourceDirectory);
        var editAndContinueSource = string.IsNullOrWhiteSpace(editAndContinueSourceDirectory)
            ? null
            : ResolveDirectory(editAndContinueSourceDirectory);
        var destination = Path.GetFullPath(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException(
                $"Compiler payload destination '{destination}' has no parent directory.");
        Directory.CreateDirectory(destinationDirectory);

        var temporaryPath = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                AddDirectory(archive, source, prefix: null);
                if (editAndContinueSource != null)
                {
                    AddDirectory(archive, editAndContinueSource, "EditAndContinue");
                }
            }

            File.Move(temporaryPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ResolveDirectory(string path)
    {
        var resolved = Path.GetFullPath(path);
        if (!Directory.Exists(resolved))
        {
            throw new DirectoryNotFoundException(
                $"Compiler payload source directory does not exist: '{resolved}'.");
        }
        return resolved;
    }

    private static void AddDirectory(
        ZipArchive archive,
        string sourceDirectory,
        string? prefix)
    {
        foreach (var path in Directory.EnumerateFiles(
            sourceDirectory,
            "*",
            SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
        {
            if (string.Equals(
                Path.GetExtension(path),
                ".pdb",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(sourceDirectory, path)
                .Replace(Path.DirectorySeparatorChar, '/');
            var entryName = prefix == null
                ? relativePath
                : prefix + "/" + relativePath;
            archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
        }
    }
}
