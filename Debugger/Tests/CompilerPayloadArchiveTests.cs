using System.IO.Compression;
using FrontEndCSharp.Packaging;

namespace Debugger.Tests;

public sealed class CompilerPayloadArchiveTests
{
    [Test]
    public void CreatesAtomicPayloadWithoutSymbols()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-PayloadTests-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "compiler");
        var editAndContinueSource = Path.Combine(root, "edit-and-continue");
        var destination = Path.Combine(root, "output", "CompilerPayload.zip");
        Directory.CreateDirectory(Path.Combine(source, "JsRuntime"));
        Directory.CreateDirectory(editAndContinueSource);
        try
        {
            File.WriteAllText(Path.Combine(source, "Compiler.dll"), "compiler");
            File.WriteAllText(Path.Combine(source, "Compiler.pdb"), "symbols");
            File.WriteAllText(Path.Combine(source, "JsRuntime", "msil.js"), "runtime");
            File.WriteAllText(
                Path.Combine(editAndContinueSource, "FrontEndCSharp.EditAndContinue.dll"),
                "edit-and-continue");
            File.WriteAllText(
                Path.Combine(editAndContinueSource, "FrontEndCSharp.EditAndContinue.pdb"),
                "symbols");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, "old payload");

            CompilerPayloadArchive.Create(source, destination, editAndContinueSource);

            using var archive = ZipFile.OpenRead(destination);
            var entries = archive.Entries.Select(entry => entry.FullName).ToArray();
            Assert.Equal(
                new[]
                {
                    "Compiler.dll",
                    "EditAndContinue/FrontEndCSharp.EditAndContinue.dll",
                    "JsRuntime/msil.js",
                },
                entries.OrderBy(entry => entry, StringComparer.Ordinal));
            Assert.DoesNotContain(entries, entry => entry.EndsWith(
                ".pdb",
                StringComparison.OrdinalIgnoreCase));
            Assert.Empty(Directory.EnumerateFiles(
                Path.GetDirectoryName(destination)!,
                "*.tmp-*"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void InvalidSourcePreservesExistingPayload()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-PayloadTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var destination = Path.Combine(root, "CompilerPayload.zip");
            File.WriteAllText(destination, "existing payload");

            Assert.Throws<DirectoryNotFoundException>(() => CompilerPayloadArchive.Create(
                Path.Combine(root, "missing"),
                destination));

            Assert.Equal("existing payload", File.ReadAllText(destination));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
