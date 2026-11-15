using Compiler.Helpers;
using Newtonsoft.Json.Linq;

namespace Compiler.Tests;

public sealed class RuntimeOverrideToolingTests
{
    [Test]
    public void GeneratesManifestWithCanonicalHashes()
    {
        InTemporaryDirectory(directory =>
        {
            const string signature = "System.Int32.ToString()";
            var resourceName = NameNormalizers.GetRuntimeOverrideFileName(signature);
            File.WriteAllText(Path.Combine(directory, resourceName),
                $"globalThis.DotNet.Methods['{signature}'] = function () {{}};");
            var output = Path.Combine(directory, "manifest.json");
            RunTool("--generate-runtime-override-manifest", directory, output);
            var manifest = JObject.Parse(File.ReadAllText(output));
            Assert.Equal(1, manifest.Value<int>("version"));
            var entry = Assert.Single((JArray)manifest["overrides"]!);
            Assert.Equal(signature, entry.Value<string>("signature"));
            Assert.Equal(resourceName, entry.Value<string>("file"));
            var originalManifest = File.ReadAllText(output);

            File.Move(Path.Combine(directory, resourceName), Path.Combine(directory, "wrong.js"));
            var error = Assert.Throws<InvalidOperationException>(() => RunTool(
                "--generate-runtime-override-manifest", directory, output));
            Assert.Contains("must be named", error.Message);
            Assert.Equal(originalManifest, File.ReadAllText(output));
        });
    }

    [Test]
    public void GeneratedCoreLibProvidersMatchTheCompiledTargetRuntime()
    {
        InTemporaryDirectory(directory =>
        {
            var resources = Path.Combine(RepositoryRoot, "Overrides", "FrontEndCSharp.Overrides.System.Private.CoreLib",
                "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs", "Overrides");
            var output = Path.Combine(directory, "Providers.cs");
            RuntimeOverrideProviderGenerator.Generate(resources, output);
            var source = File.ReadAllText(output);
            Assert.Contains("var enumerator = arg0.GetEnumerator();", source);
            Assert.Contains("_ = arg0!.GetHashCode();", source);
            Assert.Contains("where T : struct", source);
            Assert.Contains("TargetMemberName", source);
            Assert.Contains(
                "[ManagedTypeAccess(ManagedTypeAccess.EnumMetadata)] global::System.Enum @this",
                source);
            Assert.Contains(
                "Parse<[ManagedTypeAccess(ManagedTypeAccess.EnumMetadata)] TEnum>",
                source);
            var checkedInProviders = Path.Combine(
                RepositoryRoot,
                "Overrides",
                "FrontEndCSharp.Overrides.System.Private.CoreLib",
                "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs",
                "GeneratedOverrides.g.cs");
            Assert.Equal(File.ReadAllText(checkedInProviders), source);

            var catalog = RuntimeOverrideAssemblyCatalog.Load(Path.Combine(
                AppContext.BaseDirectory,
                "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.dll"));
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(resources, "manifest.json")));
            Assert.Equal(manifest["overrides"]!.Count(), catalog.Overrides.Count);
            Assert.Equal("System.Private.CoreLib", catalog.TargetAssemblyName);
            var enumToString = Assert.Single(
                catalog.Overrides,
                entry => entry.Key == "System.Enum.ToString()");
            var enumReceiverAccess = Assert.Single(enumToString.Value.ManagedParameterTypeAccesses);
            Assert.Equal(-1, enumReceiverAccess.ParameterIndex);
            Assert.Equal(
                FrontEndCSharp.RuntimeOverrides.ManagedTypeAccess.EnumMetadata,
                enumReceiverAccess.Access);
            var enumParse = Assert.Single(
                catalog.Overrides,
                entry => entry.Key == "System.Enum.Parse<TEnum>(System.String)");
            var enumGenericAccess = Assert.Single(enumParse.Value.ManagedGenericTypeAccesses);
            Assert.Equal(0, enumGenericAccess.GenericParameterIndex);
            Assert.Equal(
                FrontEndCSharp.RuntimeOverrides.ManagedTypeAccess.EnumMetadata,
                enumGenericAccess.Access);
        });
    }

    [Test]
    public void ProviderGenerationPreservesExplicitInterfaceTargetNamesOnClrSignatureCollisions()
    {
        InTemporaryDirectory(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "public.js"),
                "globalThis.DotNet.Methods['System.String.GetEnumerator()'] = function () {};");
            File.WriteAllText(Path.Combine(directory, "explicit.js"),
                "globalThis.DotNet.Methods['System.String.System.Collections.IEnumerable.GetEnumerator()'] = function () {};");
            var output = Path.Combine(directory, "Providers.cs");

            RunTool("--generate-runtime-override-providers", directory, output);

            Assert.Contains(
                "TargetMemberName = \"System.Collections.IEnumerable.GetEnumerator\"",
                File.ReadAllText(output));
        });
    }

    [Test]
    public void ProviderGenerationRejectsMalformedAndUnknownRegistrations()
    {
        InTemporaryDirectory(directory =>
        {
            var resource = Path.Combine(directory, "method.js");
            var output = Path.Combine(directory, "Providers.cs");
            File.WriteAllText(output, "unchanged");
            foreach (var (source, diagnostic) in new[]
            {
                ("// no registration", "exactly one managed registration"),
                ("globalThis.DotNet.Methods['System.NoSuchType.Missing()'] = () => {};", "does not exist")
            })
            {
                File.WriteAllText(resource, source);
                var exception = Assert.Throws<InvalidOperationException>(() => RunTool(
                    "--generate-runtime-override-providers", directory, output));
                Assert.Contains(diagnostic, exception.Message);
                Assert.Equal("unchanged", File.ReadAllText(output));
            }
        });
    }

    [Test]
    public void ProviderGenerationRejectsUnknownCompanionBodySignatures()
    {
        InTemporaryDirectory(directory =>
        {
            const string signature = "System.Int32.ToString()";
            File.WriteAllText(Path.Combine(directory, "method.js"),
                $"globalThis.DotNet.Methods['{signature}'] = function () {{}};");
            File.WriteAllText(Path.Combine(directory, "provider-bodies.json"), """
                {
                  "version": 1,
                  "bodies": {
                    "System.Int32.Missing()": [ "throw null!;" ]
                  }
                }
                """);

            var exception = Assert.Throws<InvalidOperationException>(() => RunTool(
                "--generate-runtime-override-providers",
                directory,
                Path.Combine(directory, "Providers.cs")));
            Assert.Contains("target registrations that do not exist", exception.Message);
            Assert.Contains("System.Int32.Missing()", exception.Message);
        });
    }

    [Test]
    public void RuntimeOverrideGenerationDoesNotUsePowerShell()
    {
        var toolsDirectory = Path.Combine(
            RepositoryRoot,
            "Compiler",
            "RuntimeOverrides",
            "Tools");

        Assert.False(File.Exists(Path.Combine(
            toolsDirectory,
            "GenerateRuntimeOverrideManifest.ps1")));
        Assert.False(File.Exists(Path.Combine(
            toolsDirectory,
            "GenerateRuntimeOverrideProviders.ps1")));
    }

    private static void RunTool(string command, params string[] arguments) =>
        TestProcess.Run(
            "dotnet",
            RepositoryRoot,
            TimeSpan.FromSeconds(10),
            new[] { typeof(Compiler.Program).Assembly.Location, command }
                .Concat(arguments)
                .ToArray());

    internal static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static void InTemporaryDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "FrontEndCSharp-OverrideTools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
