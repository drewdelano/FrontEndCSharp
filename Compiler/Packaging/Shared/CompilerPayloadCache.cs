using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace FrontEndCSharp.Packaging;

public static class CompilerPayloadCache
{
    public static string GetToolPath(string payloadPath, string relativeToolPath)
    {
        if (!File.Exists(payloadPath))
        {
            throw new FileNotFoundException(
                "The FrontEndCSharp compiler payload is missing.",
                payloadPath);
        }

        string payloadHash;
        using (var stream = File.OpenRead(payloadPath))
        using (var algorithm = SHA256.Create())
        {
            payloadHash = string.Concat(algorithm.ComputeHash(stream)
                .Take(12)
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        var cacheRoot = GetCacheRoot();
        var compilerDirectory = Path.Combine(cacheRoot, payloadHash);
        var toolPath = Path.Combine(compilerDirectory, relativeToolPath);
        if (File.Exists(toolPath))
        {
            return toolPath;
        }

        Directory.CreateDirectory(cacheRoot);
        var stagingRoot = Path.Combine(cacheRoot, "Temp");
        Directory.CreateDirectory(stagingRoot);
        var stagingDirectory = Path.Combine(
            stagingRoot,
            Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Extract(payloadPath, stagingDirectory);
            try
            {
                Directory.Move(stagingDirectory, compilerDirectory);
            }
            catch (IOException) when (File.Exists(toolPath))
            {
                DeleteDirectoryTree(stagingDirectory);
            }
        }
        finally
        {
            if (Directory.Exists(ToExtendedLengthPath(stagingDirectory)))
            {
                DeleteDirectoryTree(stagingDirectory);
            }
        }

        if (!File.Exists(toolPath))
        {
            throw new FileNotFoundException(
                "The FrontEndCSharp compiler payload could not be extracted.",
                toolPath);
        }

        return toolPath;
    }

    private static void Extract(string archivePath, string destinationDirectory)
    {
        var root = Path.GetFullPath(destinationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var relativePath = entry.FullName.Replace(
                Path.AltDirectorySeparatorChar,
                Path.DirectorySeparatorChar);
            var destinationPath = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!destinationPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"The compiler payload contains a path outside its extraction root: '{entry.FullName}'.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(ToExtendedLengthPath(destinationPath));
                continue;
            }

            var destinationParent = Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidDataException(
                    $"The compiler payload contains an invalid path: '{entry.FullName}'.");
            Directory.CreateDirectory(ToExtendedLengthPath(destinationParent));
            using var input = entry.Open();
            using var output = File.Create(ToExtendedLengthPath(destinationPath));
            input.CopyTo(output);
        }
    }

    private static void DeleteDirectoryTree(string path)
    {
        Directory.Delete(ToExtendedLengthPath(path), recursive: true);
    }

    private static string ToExtendedLengthPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return fullPath;
        }
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return @"\\?\UNC\" + fullPath.Substring(2);
        }
        return @"\\?\" + fullPath;
    }

    private static string GetCacheRoot()
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
        var preferred = string.IsNullOrWhiteSpace(systemDrive)
            ? null
            : Path.Combine(systemDrive, "FrontEndCSharp");
        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FEC");

        foreach (var candidate in new[] { preferred, fallback })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var probe = Path.Combine(
                candidate,
                ".write-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(probe);
                Directory.Delete(probe);
                return candidate!;
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        throw new IOException(
            "No writable short-path cache is available for the FrontEndCSharp compiler payload.");
    }
}
