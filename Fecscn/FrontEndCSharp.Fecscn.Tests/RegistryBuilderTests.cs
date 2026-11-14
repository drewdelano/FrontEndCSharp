using System.Security.Cryptography;
using System.Text;
using FrontEndCSharp.Fecscn.Registry;

namespace FrontEndCSharp.Fecscn.Tests;

public sealed class RegistryBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "FrontEndCSharp.Fecscn.Tests",
        Guid.NewGuid().ToString("N"));

    [Test]
    public void BuildsDeterministicStaticRegistryWithSeparateDependencyKinds()
    {
        var sourcePath = WriteSource("card/Card.fecsx", "public static ReactNode Default() => <div />;");
        var document = CreateDocument(
            Item(
                "card",
                File("card/Card.fecsx", "Components/UI/Card.fecsx", sourcePath),
                npmDependencies: [new("cn", "1.0.0")],
                registryDependencies: ["utils"]),
            Item("utils", File("utils/ClassNames.cs", "Components/UI/ClassNames.cs", WriteSource("utils/ClassNames.cs", "public static class ClassNames { }"))));

        var first = RegistryBuilder.Build(document, _root);
        var second = RegistryBuilder.Build(document, _root);

        Assert.Equal(first.IndexJson, second.IndexJson);
        Assert.Equal(first.Items["card"], second.Items["card"]);
        Assert.Contains("\"dependencies\":[{\"name\":\"cn\",\"version\":\"1.0.0\"}]", first.Items["card"], StringComparison.Ordinal);
        Assert.Contains("\"registryDependencies\":[\"utils\"]", first.Items["card"], StringComparison.Ordinal);
        Assert.Contains("public static ReactNode Default()", first.Items["card"], StringComparison.Ordinal);
    }

    [Test]
    public void BuildsStaticRegistryDirectoryFromAuthoringJson()
    {
        var sourcePath = WriteSource("card/Card.fecsx", "public static ReactNode Default() => <div />;");
        var document = CreateDocument(Item(
            "card",
            File("card/Card.fecsx", "Components/UI/Card.fecsx", sourcePath)));
        var authoringPath = Path.Combine(_root, "registry.json");
        var outputPath = Path.Combine(_root, "public", "r");
        System.IO.File.WriteAllText(
            authoringPath,
            FecscnJson.SerializeRegistryDocument(document),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        RegistryBuilder.BuildFromFile(authoringPath, outputPath);

        Assert.True(System.IO.File.Exists(Path.Combine(outputPath, "index.json")));
        Assert.True(System.IO.File.Exists(Path.Combine(outputPath, "card.json")));
        Assert.Contains(
            "public static ReactNode Default()",
            System.IO.File.ReadAllText(Path.Combine(outputPath, "card.json")),
            StringComparison.Ordinal);
    }

    [Test]
    public void RejectsUnsupportedProfile()
    {
        var document = CreateDocument(Item("card", EmptyFile("card/Card.fecsx", "Components/UI/Card.fecsx"))) with
        {
            Profile = FecscnProfile.Default with { Style = "maia" }
        };

        var exception = Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(document, _root));

        Assert.Contains("profile", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public void RejectsAiElementsAndTsxSources()
    {
        var aiDocument = CreateDocument(Item("message", EmptyFile("message/Message.fecsx", "Components/UI/Message.fecsx")) with
        {
            Upstream = new("https://ui.shadcn.com/r/message.json", Hash("message"), "ai-elements")
        });
        var tsxDocument = CreateDocument(Item("button", EmptyFile("button/button.tsx", "Components/UI/button.tsx")));

        Assert.Contains(
            "AI Elements",
            Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(aiDocument, _root)).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            ".tsx",
            Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(tsxDocument, _root)).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public void RejectsMissingSourceAndDigestMismatch()
    {
        var missing = CreateDocument(Item("card", EmptyFile("card/Card.fecsx", "Components/UI/Card.fecsx")));

        Assert.Contains(
            "does not exist",
            Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(missing, _root)).Message,
            StringComparison.OrdinalIgnoreCase);

        var sourcePath = WriteSource("card/Card.fecsx", "changed");
        var mismatched = CreateDocument(Item("card", File("card/Card.fecsx", "Components/UI/Card.fecsx", sourcePath) with
        {
            Sha256 = Hash("different")
        }));

        Assert.Contains(
            "digest",
            Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(mismatched, _root)).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public void RejectsDependencyCyclesAndUnknownDependencies()
    {
        var cycle = CreateDocument(
            Item("card", EmptyFile("card/Card.fecsx", "Components/UI/Card.fecsx"), registryDependencies: ["utils"]),
            Item("utils", EmptyFile("utils/Utils.cs", "Components/UI/Utils.cs"), registryDependencies: ["card"]));
        var unknown = CreateDocument(
            Item("card", EmptyFile("card/Card.fecsx", "Components/UI/Card.fecsx"), registryDependencies: ["missing"]));

        Assert.Contains(
            "cycle",
            Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(cycle, _root)).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "missing",
            Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(unknown, _root)).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public void RejectsDuplicateTargetsAndPathEscapes()
    {
        var duplicates = CreateDocument(
            Item("card", EmptyFile("card/Card.fecsx", "Components/UI/Shared.fecsx")),
            Item("input", EmptyFile("input/Input.fecsx", "Components/UI/Shared.fecsx")));
        var sourceEscape = CreateDocument(Item("card", EmptyFile("../Card.fecsx", "Components/UI/Card.fecsx")));
        var targetEscape = CreateDocument(Item("card", EmptyFile("card/Card.fecsx", "../Card.fecsx")));

        Assert.Contains(
            "target",
            Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(duplicates, _root)).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "escapes",
            Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(sourceEscape, _root)).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "escapes",
            Assert.Throws<FecscnRegistryException>(() => RegistryBuilder.Build(targetEscape, _root)).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string WriteSource(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static FecscnRegistryDocument CreateDocument(params FecscnRegistryItem[] items) =>
        new(1, FecscnProfile.Default, items);

    private static FecscnRegistryItem Item(
        string name,
        FecscnRegistryFile file,
        IReadOnlyList<NpmDependency>? npmDependencies = null,
        IReadOnlyList<string>? registryDependencies = null) =>
        new(
            name,
            "registry:ui",
            new($"https://ui.shadcn.com/r/styles/base-nova/{name}.json", Hash(name), "components"),
            npmDependencies ?? [],
            registryDependencies ?? [],
            [file]);

    private static FecscnRegistryFile File(string source, string target, string absolutePath) =>
        new(source, target, Path.GetExtension(source) switch
        {
            ".cs" => "csharp",
            ".fecsx" => "fecsx",
            _ => "asset"
        }, Hash(System.IO.File.ReadAllText(absolutePath)));

    private static FecscnRegistryFile EmptyFile(string source, string target) =>
        new(source, target, Path.GetExtension(source) switch
        {
            ".cs" => "csharp",
            ".fecsx" => "fecsx",
            _ => "asset"
        }, Hash(string.Empty));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
