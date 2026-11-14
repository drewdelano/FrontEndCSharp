using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Compiler.Debugging;
using Compiler.Helpers;
using Compiler.Read;
using Compiler.Write;
using EmbeddedPdbFixture;
using Newtonsoft.Json.Linq;
using PortablePdbFixture;

namespace Compiler.Tests;

public class PortablePdbDebugManifestTests
{
    [Test]
    public void ReadsPortablePdbAndMapsSequencePointsToRuntimeCoordinates()
    {
        var method = typeof(PortableFixture).GetMethod(nameof(PortableFixture.AddOne))!;
        var manifest = DebugManifestBuilder.Build([Decode(method)]);

        var module = Assert.Single(manifest.Modules);
        Assert.Equal(method.Module.ModuleVersionId.ToString("D"), module.ModuleId);
        Assert.False(module.IsEmbeddedPdb);
        Assert.NotEmpty(module.PdbId);

        var document = Assert.Single(module.Documents, document =>
            document.Path.EndsWith("PortableFixture.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("SHA256", document.ChecksumAlgorithm);
        Assert.NotEmpty(document.Checksum);

        var debugMethod = Assert.Single(module.Methods);
        Assert.Equal(method.MetadataToken, debugMethod.MethodToken);
        Assert.Contains(debugMethod.Parameters, parameter => parameter.Name == "value" && parameter.Index == 0);
        Assert.Contains(
            debugMethod.LocalScopes.SelectMany(scope => scope.Locals),
            local => local.Name == "result");
        Assert.Contains(debugMethod.SequencePoints, point => !point.IsHidden && point.DocumentId == document.Id);
        Assert.All(debugMethod.SequencePoints.Where(point => !point.IsHidden), point =>
        {
            Assert.NotNull(point.BlockIndex);
            Assert.NotNull(point.InstructionIndex);
        });
        Assert.NotEmpty(debugMethod.Instructions);
        Assert.Equal(
            debugMethod.Instructions.Select(instruction => instruction.IlOffset).Order(),
            debugMethod.Instructions.Select(instruction => instruction.IlOffset));
    }

    [Test]
    public void DescribesCapturedSourceLocalsWithoutExposingTheDisplayClass()
    {
        var method = typeof(PortableFixture).GetMethod(nameof(PortableFixture.ReadCapturedLocal))!;

        var manifest = DebugManifestBuilder.Build([Decode(method)]);
        var json = JObject.Parse(DebugManifestWriter.Serialize(manifest));
        var locals = (JArray)json["modules"]![0]!["methods"]![0]!["localScopes"]![0]!["locals"]!;
        var closure = Assert.Single(locals.Children<JObject>(), local =>
            local.Value<bool?>("isCompilerGeneratedClosure") == true);

        Assert.Equal("CS$<>8__locals0", closure.Value<string>("name"));
        var captured = Assert.Single(closure["capturedLocals"]!.Children<JObject>());
        Assert.Equal("captured", captured.Value<string>("name"));
        Assert.Equal(["captured"], captured["fieldPath"]!.Values<string>());
    }

    [Test]
    public void ReadsEmbeddedPortablePdb()
    {
        var method = typeof(EmbeddedFixture).GetMethod(nameof(EmbeddedFixture.MultiplyByTwo))!;
        var manifest = DebugManifestBuilder.Build([Decode(method)]);

        var module = Assert.Single(manifest.Modules);
        Assert.True(module.IsEmbeddedPdb);
        Assert.NotEmpty(module.PdbId);
        Assert.Contains(module.Documents, document =>
            document.Path.EndsWith("EmbeddedFixture.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Single(module.Methods);
    }

    [Test]
    public void RetainsAsyncStateMachineKickoffMappingAndCustomDebugInformation()
    {
        var kickoff = typeof(PortableFixture).GetMethod(nameof(PortableFixture.AddOneAsync))!;
        var stateMachineType = kickoff.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        var moveNext = stateMachineType.GetMethod(
            nameof(IAsyncStateMachine.MoveNext),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

        var manifest = DebugManifestBuilder.Build([Decode(kickoff), Decode(moveNext)]);
        var module = Assert.Single(manifest.Modules);
        var moveNextDebugMethod = Assert.Single(
            module.Methods,
            method => method.MethodToken == moveNext.MetadataToken);

        Assert.Equal(kickoff.MetadataToken, moveNextDebugMethod.StateMachineKickoffMethodToken);
        Assert.NotEmpty(moveNextDebugMethod.CustomDebugInformation);
        Assert.Contains(moveNextDebugMethod.HoistedLocals, local =>
            local.Name == "beforeAwait"
            && local.FieldName.StartsWith("<beforeAwait>5__", StringComparison.Ordinal));
    }

    [Test]
    public void ProducesDeterministicBuildIdentityAndJson()
    {
        var method = typeof(PortableFixture).GetMethod(nameof(PortableFixture.AddOne))!;
        var decoder = Decode(method);

        var first = DebugManifestBuilder.Build([decoder]);
        var second = DebugManifestBuilder.Build([decoder]);

        Assert.Equal(first.BuildId, second.BuildId);
        Assert.Equal(
            DebugManifestWriter.Serialize(first),
            DebugManifestWriter.Serialize(second));
    }

    [Test]
    public void BuildsOneManifestAcrossSeparateAndEmbeddedSymbolModules()
    {
        var portable = typeof(PortableFixture).GetMethod(nameof(PortableFixture.AddOne))!;
        var embedded = typeof(EmbeddedFixture).GetMethod(nameof(EmbeddedFixture.MultiplyByTwo))!;

        var manifest = DebugManifestBuilder.Build([Decode(portable), Decode(embedded)]);

        Assert.Equal(2, manifest.Modules.Count);
        Assert.Contains(manifest.Modules, module =>
            module.ModuleId == portable.Module.ModuleVersionId.ToString("D") && !module.IsEmbeddedPdb);
        Assert.Contains(manifest.Modules, module =>
            module.ModuleId == embedded.Module.ModuleVersionId.ToString("D") && module.IsEmbeddedPdb);
    }

    [Test]
    public void CompilerWritesManifestOutsideBrowserOutput()
    {
        var assemblyPath = typeof(PortableFixture).Assembly.Location;
        var callGraph = Reader.GetCallGraph(assemblyPath);
        var target = Path.Combine(
            Path.GetTempPath(),
            $"PortablePdbFixture_{Guid.NewGuid():N}{DebugManifestWriter.FileSuffix}");

        try
        {
            Writer.WriteDebugManifest(target, callGraph);

            var json = File.ReadAllText(target);
            Assert.Contains("\"buildId\"", json);
            Assert.Contains("\"sequencePoints\"", json);
            Assert.Contains("\"blockIndex\"", json);
            Assert.Contains("\"instructionIndex\"", json);
            Assert.DoesNotContain("\"sourceText\"", json);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Test]
    public void CompilerWritesMatchingBuildIdentityIntoBrowserOutput()
    {
        var assemblyPath = typeof(PortableFixture).Assembly.Location;
        var callGraph = Reader.GetCallGraph(assemblyPath);
        var output = Path.Combine(Path.GetTempPath(), $"FrontEndCSharp_Debug_{Guid.NewGuid():N}");

        try
        {
            var staleRuntimeAsset = Path.Combine(
                output,
                "runtime-assets",
                "Stale.fecs",
                "stale.js");
            Directory.CreateDirectory(Path.GetDirectoryName(staleRuntimeAsset)!);
            File.WriteAllText(staleRuntimeAsset, "stale");

            Writer.WriteOutput(output, callGraph, bundle: false);

            Assert.False(File.Exists(staleRuntimeAsset));
            Assert.False(File.Exists(Path.Combine(output, "reflection.js")));
            Assert.Empty(Directory.EnumerateFiles(
                Path.Combine(output, "runtime-assets"),
                "reflection.js",
                SearchOption.AllDirectories));
            var manifestPath = DebugManifestWriter.GetDefaultPath(assemblyPath);
            var manifestJson = File.ReadAllText(manifestPath);
            var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<DebugManifest>(manifestJson)!;
            var browserIdentity = File.ReadAllText(Path.Combine(output, "debug-build.js"));
            Assert.Contains(manifest.BuildId, browserIdentity);
            Assert.Contains("__FrontEndCSharpBuildIdentity", browserIdentity);
            Assert.Contains("Object.defineProperty", browserIdentity);
            Assert.DoesNotContain("sourceText", browserIdentity);
            var index = File.ReadAllText(Path.Combine(output, "index.js"));
            Assert.Contains(
                "import { buildId as debuggerBuildId } from \"./debug-build.js\";",
                index);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Test]
    public void CompilerStoresInterpretedRegistrationsInTheBinaryPackage()
    {
        var callGraph = Reader.GetCallGraph(typeof(PortableFixture).Assembly.Location);
        var output = Path.Combine(Path.GetTempPath(), $"FrontEndCSharp_HashedModules_{Guid.NewGuid():N}");

        try
        {
            var runtime = Path.Combine(output, "runtime");
            Directory.CreateDirectory(runtime);
            File.WriteAllText(Path.Combine(runtime, "method.Legacy.ReadableName().js"), "stale");
            File.WriteAllText(Path.Combine(runtime, "type.Legacy.ReadableName.js"), "stale");

            Writer.WriteOutput(output, callGraph, bundle: false);

            var moduleNames = Directory
                .GetFiles(runtime, "*.js")
                .Select(Path.GetFileName)
                .ToList();

            Assert.DoesNotContain(moduleNames, name => name!.StartsWith("type.", StringComparison.Ordinal));
            Assert.DoesNotContain(
                NameNormalizers.GetMethodModuleFileName(callGraph.EntryPoint),
                moduleNames);
            Assert.All(moduleNames, name => Assert.Matches(
                new Regex("^method\\.[0-9a-f]{64}\\.js$", RegexOptions.CultureInvariant),
                name!));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Test]
    public void BinaryMsilPackageRoundTripsThroughTheJavaScriptDecoder()
    {
        var callGraph = Reader.GetCallGraph(typeof(PortableFixture).Assembly.Location);
        var decoder = callGraph.MethodDecoders.Single(item =>
            item.MethodBase.Name == nameof(PortableFixture.AddOne));
        var utf16Decoder = callGraph.MethodDecoders.Single(item =>
            item.MethodBase.Name == nameof(PortableFixture.Utf16BoundaryString));
        var method = decoder.MethodBase;
        var output = Path.Combine(Path.GetTempPath(), $"FrontEndCSharp_MsilBinary_{Guid.NewGuid():N}");

        try
        {
            Writer.WriteOutput(output, callGraph, bundle: false);
            var packagePath = Path.Combine(output, MsilBinaryPackage.FileName);
            Assert.True(File.Exists(packagePath));
            var packageBytes = File.ReadAllBytes(packagePath);
            Assert.Equal("FECSIL\r\n", System.Text.Encoding.ASCII.GetString(
                packageBytes, 0, 8));
            Assert.Equal(7, packageBytes[8]);

            var stringTableOffset = 9;
            var stringCount = checked((int)ReadVarUInt(packageBytes, ref stringTableOffset));
            var encodedStringBytes = 0;
            var uncompressedStringBytes = 0;
            var previousBytes = Array.Empty<byte>();
            var decodedStrings = new List<(string Value, int PrefixLength)>();
            var hasMultiByteSuffixLength = false;
            var firstPrefixOffset = stringTableOffset;
            for (var stringIndex = 0; stringIndex < stringCount; stringIndex++)
            {
                var entryStart = stringTableOffset;
                var prefixLength = checked((int)ReadVarUInt(packageBytes, ref stringTableOffset));
                Assert.InRange(prefixLength, 0, previousBytes.Length);
                var suffixLengthOffset = stringTableOffset;
                var suffixLength = checked((int)ReadVarUInt(packageBytes, ref stringTableOffset));
                hasMultiByteSuffixLength |= suffixLength >= 128
                    && stringTableOffset - suffixLengthOffset > 1;
                var length = checked(prefixLength + suffixLength);
                var bytes = new byte[length];
                previousBytes.AsSpan(0, prefixLength).CopyTo(bytes);
                packageBytes.AsSpan(stringTableOffset, suffixLength)
                    .CopyTo(bytes.AsSpan(prefixLength));
                decodedStrings.Add((Wtf8Codec.Decode(bytes), prefixLength));
                stringTableOffset = checked(stringTableOffset + suffixLength);
                encodedStringBytes += stringTableOffset - entryStart;
                uncompressedStringBytes += GetVarUIntSize((ulong)length) + length;
                previousBytes = bytes;
            }
            Assert.True(
                encodedStringBytes * 10 < uncompressedStringBytes * 9,
                $"The prefix trie used {encodedStringBytes} bytes versus {uncompressedStringBytes} bytes for full strings.");
            Assert.True(hasMultiByteSuffixLength);
            Assert.Equal(
                1,
                decodedStrings.Count(item =>
                    item.Value == PortableFixture.DuplicateStringFirst()));
            var frontCodedSecond = decodedStrings.Single(item =>
                item.Value == PortableFixture.FrontCodedSecond());
            Assert.Equal(2, frontCodedSecond.PrefixLength);
            Assert.Contains(decodedStrings, item =>
                item.Value == PortableFixture.Utf16BoundaryString());

            Assert.False(File.Exists(Path.Combine(
                output,
                "runtime",
                NameNormalizers.GetMethodModuleFileName(method))));

            var key = MsilBinaryPackage.GetMethodBodyKey(method);
            var expected = MsilBinaryPackage.CreateMethodBody(decoder);
            Assert.True(JToken.DeepEquals(
                expected,
                MsilBinaryPackage.ReadMethodBody(packagePath, key)));
            var utf16Key = MsilBinaryPackage.GetMethodBodyKey(utf16Decoder.MethodBase);
            var utf16Body = MsilBinaryPackage.ReadMethodBody(packagePath, utf16Key);
            Assert.Equal(
                PortableFixture.Utf16BoundaryString(),
                utf16Body.SelectMany(block => block["instructions"]!)
                    .Single(instruction => instruction!.Value<string>("oc") == "ldstr")!["opr"]!
                    .Value<string>());

            var singleDecoder = callGraph.MethodDecoders.Single(item =>
                item.MethodBase.Name == nameof(PortableFixture.NegativeZeroSingle));
            var singleBody = MsilBinaryPackage.ReadMethodBody(
                packagePath,
                MsilBinaryPackage.GetMethodBodyKey(singleDecoder.MethodBase));
            var singleOperand = Assert.IsType<float>(Assert.IsType<JValue>(singleBody
                .SelectMany(block => block["instructions"]!)
                .Single(instruction => instruction!.Value<string>("oc") == "ldc.r4")!["opr"]!).Value);
            Assert.Equal(
                unchecked((int)0x80000000),
                BitConverter.SingleToInt32Bits(singleOperand));

            var doubleDecoder = callGraph.MethodDecoders.Single(item =>
                item.MethodBase.Name == nameof(PortableFixture.NegativeZeroDouble));
            var doubleBody = MsilBinaryPackage.ReadMethodBody(
                packagePath,
                MsilBinaryPackage.GetMethodBodyKey(doubleDecoder.MethodBase));
            var doubleOperand = Assert.IsType<double>(Assert.IsType<JValue>(doubleBody
                .SelectMany(block => block["instructions"]!)
                .Single(instruction => instruction!.Value<string>("oc") == "ldc.r8")!["opr"]!).Value);
            Assert.Equal(
                unchecked((long)0x8000000000000000),
                BitConverter.DoubleToInt64Bits(doubleOperand));

            packageBytes[firstPrefixOffset] = 1;
            var invalidPackagePath = Path.Combine(output, "invalid-prefix.bin");
            File.WriteAllBytes(invalidPackagePath, packageBytes);
            var invalidPrefix = Assert.Throws<InvalidDataException>(() =>
                MsilBinaryPackage.ReadMethodBody(invalidPackagePath, key));
            Assert.Contains(
                "prefix length 1 exceeds the previous string's 0 bytes",
                invalidPrefix.Message,
                StringComparison.Ordinal);

            var scriptPath = Path.Combine(output, "decode-msil.mjs");
            File.WriteAllText(scriptPath, """
                import { readFile } from "node:fs/promises";
                import { decodeMsilPackage } from "./msil-binary.js";
                const bytes = await readFile(new URL("./msil.bin", import.meta.url));
                const decoded = decodeMsilPackage(bytes);
                const definition = decoded.methodDefinitions.find(item => item.key === process.argv[2]);
                if (definition?.methodInfo?.fullMethodName !== process.argv[3]) {
                    throw new Error("The binary package did not contain the method registration metadata.");
                }
                if (decoded.types.size === 0) {
                    throw new Error("The binary package did not contain type registrations.");
                }
                const body = decoded.methods.get(process.argv[2]);
                const utf16Body = decoded.methods.get(process.argv[4]);
                const value = utf16Body
                    .flatMap(block => block.instructions)
                    .find(instruction => instruction.oc === "ldstr")?.opr;
                if (typeof value !== "string") {
                    throw new Error("The UTF-16 fixture did not contain a string operand.");
                }
                const codeUnits = Array.from(
                    { length: value.length },
                    (_, index) => value.charCodeAt(index));
                process.stdout.write(JSON.stringify({ body, codeUnits }));
                """);
            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add(key);
            startInfo.ArgumentList.Add(NameNormalizers.GetFullyQualifiedMethodName(method));
            startInfo.ArgumentList.Add(utf16Key);
            using var process = Process.Start(startInfo)!;
            Assert.True(process.WaitForExit(10_000), "JavaScript MSIL decoding timed out.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            Assert.True(
                process.ExitCode == 0,
                $"JavaScript MSIL decoding failed:{Environment.NewLine}{standardError}");
            var decoded = JObject.Parse(standardOutput);
            Assert.True(JToken.DeepEquals(expected, decoded["body"]));
            Assert.Equal(
                PortableFixture.Utf16BoundaryString().Select(character => (int)character),
                decoded["codeUnits"]!.Values<int>());
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Test]
    public void EditAndContinueInflatesUpdatedMethodBodiesFromTheBinaryPackage()
    {
        var callGraph = Reader.GetCallGraph(typeof(PortableFixture).Assembly.Location);
        var decoder = callGraph.MethodDecoders.First(item =>
            item.MethodBody != null
            && item.MethodBase.Module == callGraph.EntryPoint.Module
            && !PathLocator.HasBcl(item.MethodBase)
            && !callGraph.RuntimeOverrides.HasRuntimeOverride(item.MethodBase));
        var root = Path.Combine(Path.GetTempPath(), $"FrontEndCSharp_BinaryEnc_{Guid.NewGuid():N}");
        var baselineOutput = Path.Combine(root, "baseline");
        var candidateOutput = Path.Combine(root, "candidate");
        var baselineManifest = Path.Combine(root, "baseline.json");
        var candidateManifest = Path.Combine(root, "candidate.json");
        var outputManifest = Path.Combine(root, "updated.json");

        try
        {
            Writer.WriteOutput(baselineOutput, callGraph, bundle: false);
            Writer.WriteOutput(candidateOutput, callGraph, bundle: false);
            Writer.WriteDebugManifest(baselineManifest, callGraph);
            Writer.WriteDebugManifest(candidateManifest, callGraph);

            AddDependencyMethodWithSameToken(baselineManifest, decoder.MethodBase.MetadataToken);
            AddDependencyMethodWithSameToken(candidateManifest, decoder.MethodBase.MetadataToken);

            var update = EditAndContinueUpdateBuilder.Build(
                baselineManifest,
                candidateManifest,
                baselineOutput,
                candidateOutput,
                generation: 1,
                [decoder.MethodBase.MetadataToken],
                outputManifest);
            var source = File.ReadAllText(Path.Combine(baselineOutput, update.UpdateModulePath));

            Assert.Contains("const blocks = [{", source, StringComparison.Ordinal);
            Assert.Contains("\"instructions\":[", source, StringComparison.Ordinal);
            Assert.DoesNotContain("getMethodBody", source, StringComparison.Ordinal);
            Assert.DoesNotContain("import ", source, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        static void AddDependencyMethodWithSameToken(string manifestPath, int methodToken)
        {
            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var modules = (JArray)manifest["modules"]!;
            var entryModule = modules
                .OfType<JObject>()
                .Single(module => module["methods"]!
                    .Any(method => method!["methodToken"]!.Value<int>() == methodToken));
            var duplicateMethod = entryModule["methods"]!
                .Single(method => method!["methodToken"]!.Value<int>() == methodToken)
                .DeepClone();
            modules.Add(new JObject
            {
                ["moduleId"] = Guid.NewGuid().ToString("D"),
                ["assemblyName"] = "dependency.netmodule",
                ["pdbId"] = string.Empty,
                ["isEntryModule"] = false,
                ["documents"] = new JArray(),
                ["methods"] = new JArray(duplicateMethod)
            });
            File.WriteAllText(manifestPath, manifest.ToString());
        }
    }

    [Test]
    public void CompilerBundlesTheCompleteModuleGraphIntoThePublicIndex()
    {
        var callGraph = Reader.GetCallGraph(typeof(PortableFixture).Assembly.Location);
        var output = Path.Combine(Path.GetTempPath(), $"FrontEndCSharp_Bundle_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "msil.stale.bin"), "stale");
            File.WriteAllText(Path.Combine(output, "bootstrap-msil.js"), "stale");
            File.WriteAllText(Path.Combine(output, "debug-build.js"), "stale");
            Directory.CreateDirectory(Path.Combine(output, "jit-engine"));
            File.WriteAllText(Path.Combine(output, "jit-engine", "stale.js"), "stale");
            Directory.CreateDirectory(Path.Combine(output, "runtime"));
            File.WriteAllText(Path.Combine(output, "runtime", "stale.js"), "stale");
            Directory.CreateDirectory(Path.Combine(output, "runtime-assets", "stale.fecs"));
            File.WriteAllText(
                Path.Combine(output, "runtime-assets", "stale.fecs", "stale.js"),
                "stale");
            File.WriteAllText(Path.Combine(output, "site.js"), "application-owned");
            Writer.WriteOutput(output, callGraph, debuggerRendezvousTimeoutMilliseconds: 12_345);

            var indexPath = Path.Combine(output, "index.js");
            var index = File.ReadAllText(indexPath);

            Assert.DoesNotContain(" from \"./", index, StringComparison.Ordinal);
            Assert.DoesNotContain("import \"./", index, StringComparison.Ordinal);
            Assert.DoesNotContain("import(", index, StringComparison.Ordinal);
            Assert.DoesNotContain("Automatic publicPath", index, StringComparison.Ordinal);
            Assert.Contains("import.meta.url", index, StringComparison.Ordinal);
            Assert.DoesNotContain("document.baseURI", index, StringComparison.Ordinal);
            Assert.DoesNotContain("index.entry.js", Directory.GetFiles(output).Select(Path.GetFileName));
            Assert.True(File.Exists(Path.Combine(output, MsilBinaryPackage.FileName)));
            Assert.Empty(Directory.GetFiles(output, "msil.*.bin"));
            var packageHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(Path.Combine(output, MsilBinaryPackage.FileName))))
                .ToLowerInvariant();
            Assert.Contains($"msil.bin?v={packageHash}", index, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(output, "runtime")));
            Assert.False(Directory.Exists(Path.Combine(output, "jit-engine")));
            Assert.False(Directory.Exists(Path.Combine(output, "runtime-assets")));
            Assert.False(File.Exists(Path.Combine(output, "bootstrap-msil.js")));
            Assert.False(File.Exists(Path.Combine(output, "debug-build.js")));
            Assert.Equal("application-owned", File.ReadAllText(Path.Combine(output, "site.js")));
            Assert.Equal(
                new[] { "index.js", "index.js.map", "msil.bin", "site.js" },
                Directory.GetFiles(output).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
            Assert.True(File.Exists(indexPath + ".map"));
            var sourceMap = JObject.Parse(File.ReadAllText(indexPath + ".map"));
            var publishedSourceRoot = new Uri(Path.GetFullPath(output) + Path.DirectorySeparatorChar).AbsoluteUri;
            Assert.All(
                sourceMap["sources"]!.Values<string>().OfType<string>()
                    .Where(source => !source.StartsWith(
                        "webpack:///webpack/",
                        StringComparison.Ordinal)),
                source =>
                {
                    Assert.StartsWith("file:///", source, StringComparison.Ordinal);
                    Assert.DoesNotContain("FrontEndCSharp-Output-", source, StringComparison.Ordinal);
                });
            Assert.Contains(
                sourceMap["sources"]!.Values<string>(),
                source => source == publishedSourceRoot + "jit-engine/_executeInstruction.js");
            Assert.Contains(
                sourceMap["sources"]!.Values<string>(),
                source => source == publishedSourceRoot + "debug-build.js");
            Assert.DoesNotContain(
                sourceMap["sources"]!.Values<string>(),
                source => source == publishedSourceRoot + "msil.js");
            Assert.DoesNotContain(
                sourceMap["sources"]!.Values<string>(),
                source => source?.Contains("/node_modules/", StringComparison.Ordinal) == true);
            var sourceMapNames = sourceMap["names"]!.Values<string>().OfType<string>().ToHashSet();
            Assert.Contains("executeInstruction", sourceMapNames);
            Assert.Contains("opts", sourceMapNames);
            Assert.Contains("debugger", index, StringComparison.Ordinal);
            Assert.Contains("ExecuteMSIL", index, StringComparison.Ordinal);
            Assert.Matches(@"function ExecuteMSIL\([^)]*\)\{.{0,1000}\bopts=", index);
            Assert.DoesNotContain("getInstructionHandlers", index, StringComparison.Ordinal);
            Assert.True(
                index.Split('\n').Length <= 3,
                "The public index should be minified rather than retaining the emitted module formatting.");
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Test]
    public void DebuggerRendezvousIsGeneratedOnlyWhenRequested()
    {
        var assemblyPath = typeof(PortableFixture).Assembly.Location;
        var callGraph = Reader.GetCallGraph(assemblyPath);
        var normalOutput = Path.Combine(Path.GetTempPath(), $"FrontEndCSharp_Normal_{Guid.NewGuid():N}");
        var debugOutput = Path.Combine(Path.GetTempPath(), $"FrontEndCSharp_Rendezvous_{Guid.NewGuid():N}");

        try
        {
            Writer.WriteOutput(normalOutput, callGraph, bundle: false);
            Writer.WriteOutput(
                debugOutput,
                callGraph,
                debuggerRendezvousTimeoutMilliseconds: 12_345,
                bundle: false);

            var normalIndex = File.ReadAllText(Path.Combine(normalOutput, "index.js"));
            var debugIndex = File.ReadAllText(Path.Combine(debugOutput, "index.js"));
            Assert.DoesNotContain("waitForDebuggerConfiguration", normalIndex);
            Assert.Contains("buildId as debuggerBuildId", normalIndex);
            Assert.Contains("await waitForDebuggerConfiguration(debuggerBuildId, 12345);", debugIndex);
        }
        finally
        {
            if (Directory.Exists(normalOutput))
            {
                Directory.Delete(normalOutput, recursive: true);
            }
            if (Directory.Exists(debugOutput))
            {
                Directory.Delete(debugOutput, recursive: true);
            }
        }
    }

    [TestCase(0)]
    [TestCase(300_001)]
    public void DebuggerRendezvousRejectsAnUnboundedTimeout(int timeoutMilliseconds)
    {
        var callGraph = Reader.GetCallGraph(typeof(PortableFixture).Assembly.Location);
        var output = Path.Combine(Path.GetTempPath(), $"FrontEndCSharp_InvalidRendezvous_{Guid.NewGuid():N}");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Writer.WriteOutput(output, callGraph, timeoutMilliseconds));

        Assert.Equal("debuggerRendezvousTimeoutMilliseconds", exception.ParamName);
        Assert.False(Directory.Exists(output));
    }

    private static MethodDecoder Decode(MethodBase method)
    {
        var decoder = new MethodDecoder();
        decoder.LoadMethod(method);
        return decoder;
    }

    private static ulong ReadVarUInt(byte[] bytes, ref int offset)
    {
        ulong result = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            var next = bytes[offset++];
            result |= (ulong)(next & 0x7f) << shift;
            if ((next & 0x80) == 0)
            {
                return result;
            }
        }
        throw new InvalidDataException("Test package contains an invalid variable-length integer.");
    }

    private static int GetVarUIntSize(ulong value)
    {
        var size = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            size++;
        }
        return size;
    }
}
