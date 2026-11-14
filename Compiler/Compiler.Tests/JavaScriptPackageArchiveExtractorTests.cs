using System.IO.Compression;
using System.Text;
using Compiler.Helpers;

namespace Compiler.Tests;

public sealed class JavaScriptPackageArchiveExtractorTests
{
    [Test]
    public void Extract_allows_identical_compressed_files_from_multiple_companions()
    {
        var target = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-package-extractor-" + Guid.NewGuid().ToString("N"));
        try
        {
            var archive = CreateArchive("shared/index.js", "export const value = 42;\n");

            JavaScriptPackageArchiveExtractor.Extract(target, [archive, archive]);

            Assert.Equal(
                "export const value = 42;\n",
                File.ReadAllText(Path.Combine(target, "node_modules", "shared", "index.js")));
        }
        finally
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }

    [Test]
    public void Extract_compares_identical_compressed_files_across_partial_reads()
    {
        var target = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-package-extractor-" + Guid.NewGuid().ToString("N"));
        try
        {
            var content = string.Join(
                '\n',
                Enumerable.Range(0, 4_000).Select(index =>
                    $"export const value{index} = '{index:D8}-{index * 17:D8}';"));
            var archive = CreateArchive("shared/large.js", content);

            JavaScriptPackageArchiveExtractor.Extract(target, [archive, archive]);

            Assert.Equal(
                content,
                File.ReadAllText(Path.Combine(target, "node_modules", "shared", "large.js")));
        }
        finally
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }

    private static byte[] CreateArchive(string path, string content)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using var writer = new StreamWriter(
                entry.Open(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content);
        }
        return stream.ToArray();
    }
}
