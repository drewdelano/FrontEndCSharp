using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Compiler.Helpers
{
    internal static class JavaScriptPackageArchiveExtractor
    {
        internal static void Extract(
            string targetDirectory,
            IReadOnlyList<byte[]> packageArchives)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
            if (packageArchives.Count == 0)
            {
                return;
            }

            var nodeModulesDirectory = Path.GetFullPath(
                Path.Combine(targetDirectory, "node_modules"));
            Directory.CreateDirectory(nodeModulesDirectory);
            foreach (var archiveBytes in packageArchives)
            {
                using var stream = new MemoryStream(archiveBytes, writable: false);
                using var archive = new ZipArchive(
                    stream,
                    ZipArchiveMode.Read,
                    leaveOpen: false);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)
                        || string.Equals(
                            entry.FullName,
                            ".package-lock.json",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var relativePath = entry.FullName.Replace(
                        '/',
                        Path.DirectorySeparatorChar);
                    var destination = Path.GetFullPath(
                        Path.Combine(nodeModulesDirectory, relativePath));
                    var nodeModulesPrefix = nodeModulesDirectory + Path.DirectorySeparatorChar;
                    if (!destination.StartsWith(
                        nodeModulesPrefix,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"JavaScript package archive entry '{entry.FullName}' escapes node_modules.");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    using var input = entry.Open();
                    if (!File.Exists(destination))
                    {
                        using var output = File.Create(destination);
                        input.CopyTo(output);
                        continue;
                    }

                    using var existing = File.OpenRead(destination);
                    if (!StreamsEqual(existing, input))
                    {
                        throw new InvalidDataException(
                            "Selected runtime-override companions contain different JavaScript package files "
                            + $"for '{entry.FullName}'.");
                    }
                }
            }
        }

        private static bool StreamsEqual(Stream left, Stream right)
        {
            if (left.CanSeek && right.CanSeek && left.Length != right.Length)
            {
                return false;
            }
            var leftBuffer = new byte[81920];
            var rightBuffer = new byte[leftBuffer.Length];
            while (true)
            {
                var leftCount = FillBuffer(left, leftBuffer);
                var rightCount = FillBuffer(right, rightBuffer);
                if (leftCount != rightCount)
                {
                    return false;
                }
                if (leftCount == 0)
                {
                    return true;
                }
                if (!leftBuffer.AsSpan(0, leftCount).SequenceEqual(
                    rightBuffer.AsSpan(0, rightCount)))
                {
                    return false;
                }
            }
        }

        private static int FillBuffer(Stream stream, byte[] buffer)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var count = stream.Read(buffer, total, buffer.Length - total);
                if (count == 0)
                {
                    break;
                }
                total += count;
            }
            return total;
        }
    }
}
