using System.IO.Compression;
using FrontEndCSharp.Packaging;

namespace Compiler.Tests;

public sealed class CompilerPayloadCacheTests
{
    [Test]
    public void RejectsArchiveEntryOutsideExtractionRoot()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"FrontEndCSharp_Payload_{Guid.NewGuid():N}");
        var archivePath = Path.Combine(directory, "payload.zip");

        try
        {
            Directory.CreateDirectory(directory);
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open());
                writer.Write("must not escape");
            }

            var exception = Assert.Throws<InvalidDataException>(() =>
                CompilerPayloadCache.GetToolPath(archivePath, "Compiler.dll"));

            Assert.Contains("outside its extraction root", exception.Message);
            Assert.False(File.Exists(Path.Combine(directory, "escape.txt")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
