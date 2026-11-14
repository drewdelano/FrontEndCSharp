using System.Text.Json;
using Compiler.Helpers;

namespace Compiler.Tests;

public sealed class JavaScriptBundlerTests
{
    [TestCase(JavaScriptSourceMapMode.Managed)]
    [TestCase(JavaScriptSourceMapMode.Full)]
    public void BundleHonorsTheSelectedSourceMapMode(JavaScriptSourceMapMode sourceMapMode)
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "TestOutput",
            $"FrontEndCSharp_Bundler_{Guid.NewGuid():N}");
        var vendorDirectory = Path.Combine(root, "node_modules", "nested-map-vendor");
        var runtimeAssetDirectory = Path.Combine(root, "runtime-assets", "Fixture.fecs");
        var jitEngineDirectory = Path.Combine(root, "jit-engine");
        var entryPath = Path.Combine(root, "index.entry.js");
        var outputPath = Path.Combine(root, "index.js");
        const string entrySource = """
            const { vendorValue } = require("nested-map-vendor");
            const { runtimeValue } = require("./runtime-assets/Fixture.fecs/override.js");
            const { executeInstruction } = require("./jit-engine/_executeInstruction.js");
            const { buildId } = require("./debug-build.js");
            const { coreValue } = require("./msil.js");
            globalThis.frontEndCSharpBundlerFixture = [
                vendorValue,
                runtimeValue,
                executeInstruction,
                buildId,
                coreValue
            ];
            //# sourceMappingURL=application.js.map
            """;
        const string vendorSource = """
            exports.vendorValue = "vendor-marker";
            //# sourceMappingURL=vendor.js.map
            """;
        const string runtimeAssetSource = """
            exports.runtimeValue = "runtime-asset-marker";
            //# sourceMappingURL=runtime-asset.js.map
            """;

        try
        {
            Directory.CreateDirectory(vendorDirectory);
            Directory.CreateDirectory(runtimeAssetDirectory);
            Directory.CreateDirectory(jitEngineDirectory);
            File.WriteAllText(entryPath, entrySource);
            File.WriteAllText(Path.Combine(vendorDirectory, "index.js"), vendorSource);
            File.WriteAllText(
                Path.Combine(runtimeAssetDirectory, "override.js"),
                runtimeAssetSource);
            File.WriteAllText(
                Path.Combine(jitEngineDirectory, "_executeInstruction.js"),
                "exports.executeInstruction = \"debug-anchor-marker\";");
            File.WriteAllText(
                Path.Combine(root, "debug-build.js"),
                "exports.buildId = \"debug-build-marker\";");
            File.WriteAllText(
                Path.Combine(root, "msil.js"),
                "exports.coreValue = \"compiler-core-marker\";");
            File.WriteAllText(
                Path.Combine(vendorDirectory, "package.json"),
                """
                {
                  "name": "nested-map-vendor",
                  "version": "1.0.0",
                  "main": "index.js"
                }
                """);

            JavaScriptBundler.Bundle(entryPath, outputPath, root, sourceMapMode);

            using var sourceMap = JsonDocument.Parse(File.ReadAllText(outputPath + ".map"));
            var sources = sourceMap.RootElement.GetProperty("sources")
                .EnumerateArray()
                .Select(source => source.GetString()!)
                .ToArray();
            var sourceContents = sourceMap.RootElement.GetProperty("sourcesContent")
                .EnumerateArray()
                .Select(content => content.GetString())
                .ToArray();

            Assert.Equal(sources.Length, sourceContents.Length);
            Assert.True(
                sources.Any(source => source.Replace('\\', '/').EndsWith(
                    "/runtime-assets/Fixture.fecs/override.js",
                    StringComparison.Ordinal)),
                string.Join(
                    Environment.NewLine,
                    sources.Zip(
                        sourceContents,
                        (source, content) => $"{source}: {content}")));
            Assert.Contains(sources, source => source.Replace('\\', '/').EndsWith(
                "/jit-engine/_executeInstruction.js",
                StringComparison.Ordinal));
            Assert.Contains(sources, source => source.Replace('\\', '/').EndsWith(
                "/debug-build.js",
                StringComparison.Ordinal));
            Assert.Contains(
                sourceContents,
                content => content?.Contains("runtime-asset-marker", StringComparison.Ordinal) == true);
            if (sourceMapMode == JavaScriptSourceMapMode.Managed)
            {
                Assert.DoesNotContain(sources, source => source.Contains(
                    "/node_modules/",
                    StringComparison.Ordinal));
                Assert.DoesNotContain(sources, source => source.Replace('\\', '/').EndsWith(
                    "/msil.js",
                    StringComparison.Ordinal));
                Assert.DoesNotContain(
                    sourceContents,
                    content => content?.Contains(
                        "frontEndCSharpBundlerFixture",
                        StringComparison.Ordinal) == true);
            }
            else
            {
                var bundledEntrySource = Assert.Single(
                    sourceContents,
                    content => content?.Contains(
                        "frontEndCSharpBundlerFixture",
                        StringComparison.Ordinal) == true);
                var bundledVendorSource = Assert.Single(
                    sourceContents,
                    content => content?.Contains("vendor-marker", StringComparison.Ordinal) == true);

                Assert.Contains(
                    "sourceMappingURL=application.js.map",
                    bundledEntrySource!,
                    StringComparison.Ordinal);
                Assert.DoesNotContain(
                    "sourceMappingURL=vendor.js.map",
                    bundledVendorSource!,
                    StringComparison.Ordinal);
                Assert.Equal(vendorSource.Length, bundledVendorSource!.Length);
                Assert.Equal(
                    CountLineBreaks(vendorSource),
                    CountLineBreaks(bundledVendorSource));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static int CountLineBreaks(string value) => value.Count(character => character == '\n');

}
