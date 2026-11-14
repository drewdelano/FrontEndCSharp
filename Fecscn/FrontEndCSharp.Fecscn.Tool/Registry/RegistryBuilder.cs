using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FrontEndCSharp.Fecscn.Registry;

public static partial class RegistryBuilder
{
    private const int SupportedVersion = 1;
    private const string RegistrySchema = "https://fecscn.dev/schema/registry.json";
    private const string RegistryItemSchema = "https://fecscn.dev/schema/registry-item.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static FecscnRegistryBuildResult Build(
        FecscnRegistryDocument document,
        string authoringRoot)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(authoringRoot);

        ValidateDocument(document);

        var root = Path.GetFullPath(authoringRoot);
        var itemsByName = document.Items
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToDictionary(item => item.Name, StringComparer.Ordinal);
        ValidateDependencies(itemsByName);
        ValidateFileDeclarations(itemsByName.Values);

        var itemJson = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in itemsByName.Values.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var files = ReadFiles(item, root);
            var serialized = new PublishedRegistryItemDocument(
                RegistryItemSchema,
                SupportedVersion,
                item.Name,
                item.Type,
                item.Upstream,
                item.Dependencies
                    .OrderBy(dependency => dependency.Name, StringComparer.Ordinal)
                    .ToArray(),
                item.RegistryDependencies
                    .OrderBy(dependency => dependency, StringComparer.Ordinal)
                    .ToArray(),
                files,
                (item.Packages ?? [])
                    .OrderBy(dependency => dependency.Name, StringComparer.Ordinal)
                    .ToArray());
            itemJson.Add(item.Name, JsonSerializer.Serialize(serialized, JsonOptions));
        }

        var index = new PublishedRegistryIndex(
            RegistrySchema,
            SupportedVersion,
            document.Profile,
            itemsByName.Values
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => new PublishedRegistryIndexItem(
                    item.Name,
                    item.Type,
                    $"{item.Name}.json",
                    item.Upstream,
                    item.Dependencies
                        .OrderBy(dependency => dependency.Name, StringComparer.Ordinal)
                        .ToArray(),
                    item.RegistryDependencies
                        .OrderBy(dependency => dependency, StringComparer.Ordinal)
                            .ToArray(),
                        (item.Packages ?? [])
                            .OrderBy(dependency => dependency.Name, StringComparer.Ordinal)
                            .ToArray()))
                .ToArray());

        return new(
            JsonSerializer.Serialize(index, JsonOptions),
            itemJson);
    }

    public static void BuildFromFile(string authoringDocumentPath, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authoringDocumentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var documentPath = Path.GetFullPath(authoringDocumentPath);
        if (!System.IO.File.Exists(documentPath))
        {
            throw new FecscnRegistryException(
                $"Registry authoring document '{documentPath}' does not exist.");
        }

        FecscnRegistryDocument document;
        try
        {
            document = FecscnJson.ParseRegistryDocument(
                System.IO.File.ReadAllText(documentPath));
        }
        catch (Fecscn.Configuration.FecscnConfigurationException exception)
        {
            throw new FecscnRegistryException(exception.Message);
        }

        var authoringRoot = Path.GetDirectoryName(documentPath)!;
        var result = Build(document, authoringRoot);
        Publish(result, Path.GetFullPath(outputDirectory));
    }

    private static void Publish(FecscnRegistryBuildResult result, string outputDirectory)
    {
        var parent = Path.GetDirectoryName(outputDirectory);
        if (string.IsNullOrEmpty(parent))
        {
            throw new FecscnRegistryException(
                $"Registry output directory '{outputDirectory}' has no parent directory.");
        }
        Directory.CreateDirectory(parent);

        var stagingDirectory = outputDirectory + ".staging-" + Guid.NewGuid().ToString("N");
        var backupDirectory = outputDirectory + ".backup-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            WriteUtf8(Path.Combine(stagingDirectory, "index.json"), result.IndexJson);
            foreach (var item in result.Items)
            {
                WriteUtf8(Path.Combine(stagingDirectory, item.Key + ".json"), item.Value);
            }

            var hadExistingOutput = Directory.Exists(outputDirectory);
            if (hadExistingOutput)
            {
                Directory.Move(outputDirectory, backupDirectory);
            }
            try
            {
                Directory.Move(stagingDirectory, outputDirectory);
            }
            catch
            {
                if (hadExistingOutput && Directory.Exists(backupDirectory))
                {
                    Directory.Move(backupDirectory, outputDirectory);
                }
                throw;
            }
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is not FecscnRegistryException)
        {
            throw new FecscnRegistryException(
                $"Unable to publish fecscn registry output '{outputDirectory}': {exception.Message}");
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
            if (Directory.Exists(backupDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.Move(backupDirectory, outputDirectory);
            }
        }
    }

    private static void WriteUtf8(string path, string content) =>
        System.IO.File.WriteAllText(
            path,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static void ValidateDocument(FecscnRegistryDocument document)
    {
        if (document.Version != SupportedVersion)
        {
            throw new FecscnRegistryException(
                $"Registry version '{document.Version}' is unsupported; expected '{SupportedVersion}'.");
        }
        if (document.Profile != FecscnProfile.Default)
        {
            throw new FecscnRegistryException(
                "The registry profile is unsupported. The initial fecscn registry requires "
                + "Base UI, Nova, Lucide, Tailwind CSS v4, CSS variables, and non-RSC output.");
        }
        if (document.Items.Count == 0)
        {
            throw new FecscnRegistryException("The registry must contain at least one item.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in document.Items)
        {
            if (!ItemNamePattern().IsMatch(item.Name))
            {
                throw new FecscnRegistryException(
                    $"Registry item name '{item.Name}' must use lowercase kebab-case.");
            }
            if (!names.Add(item.Name))
            {
                throw new FecscnRegistryException($"Registry item '{item.Name}' is duplicated.");
            }
            if (item.Type is not ("registry:ui" or "registry:lib"))
            {
                throw new FecscnRegistryException(
                    $"Registry item '{item.Name}' has unsupported type '{item.Type}'.");
            }
            ValidateUpstream(item);
            ValidateNpmDependencies(item);
            ValidateNuGetDependencies(item);
            if (item.Files.Count == 0)
            {
                throw new FecscnRegistryException(
                    $"Registry item '{item.Name}' must declare at least one source file.");
            }
        }
    }

    private static void ValidateUpstream(FecscnRegistryItem item)
    {
        if (item.Upstream.Collection.Equals("ai-elements", StringComparison.OrdinalIgnoreCase))
        {
            throw new FecscnRegistryException(
                $"Registry item '{item.Name}' belongs to shadcn AI Elements, which are outside fecscn scope.");
        }
        if (!item.Upstream.Collection.Equals("components", StringComparison.Ordinal))
        {
            throw new FecscnRegistryException(
                $"Registry item '{item.Name}' has unsupported upstream collection '{item.Upstream.Collection}'.");
        }
        if (!Uri.TryCreate(item.Upstream.Address, UriKind.Absolute, out var address)
            || address.Scheme != Uri.UriSchemeHttps)
        {
            throw new FecscnRegistryException(
                $"Registry item '{item.Name}' must use an absolute HTTPS upstream address.");
        }
        ValidateDigest(item.Upstream.Sha256, $"Registry item '{item.Name}' upstream");
    }

    private static void ValidateNpmDependencies(FecscnRegistryItem item)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependency in item.Dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependency.Name)
                || string.IsNullOrWhiteSpace(dependency.Version))
            {
                throw new FecscnRegistryException(
                    $"Registry item '{item.Name}' has an npm dependency with a missing name or version.");
            }
            if (!names.Add(dependency.Name))
            {
                throw new FecscnRegistryException(
                    $"Registry item '{item.Name}' repeats npm dependency '{dependency.Name}'.");
            }
        }
    }

    private static void ValidateNuGetDependencies(FecscnRegistryItem item)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in item.Packages ?? [])
        {
            if (string.IsNullOrWhiteSpace(dependency.Name)
                || string.IsNullOrWhiteSpace(dependency.Version)
                || !names.Add(dependency.Name))
            {
                throw new FecscnRegistryException(
                    $"Registry item '{item.Name}' has an invalid or duplicate NuGet package dependency.");
            }
        }
    }

    private static void ValidateDependencies(
        IReadOnlyDictionary<string, FecscnRegistryItem> itemsByName)
    {
        foreach (var item in itemsByName.Values)
        {
            var uniqueDependencies = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dependency in item.RegistryDependencies)
            {
                if (!uniqueDependencies.Add(dependency))
                {
                    throw new FecscnRegistryException(
                        $"Registry item '{item.Name}' repeats registry dependency '{dependency}'.");
                }
                if (!itemsByName.ContainsKey(dependency))
                {
                    throw new FecscnRegistryException(
                        $"Registry item '{item.Name}' references missing registry dependency '{dependency}'.");
                }
            }
        }

        var states = new Dictionary<string, VisitState>(StringComparer.Ordinal);
        var stack = new List<string>();
        foreach (var itemName in itemsByName.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            Visit(itemName, itemsByName, states, stack);
        }
    }

    private static void Visit(
        string itemName,
        IReadOnlyDictionary<string, FecscnRegistryItem> itemsByName,
        IDictionary<string, VisitState> states,
        IList<string> stack)
    {
        if (states.TryGetValue(itemName, out var state))
        {
            if (state == VisitState.Visited)
            {
                return;
            }
            var cycleStart = stack.IndexOf(itemName);
            var cycle = stack.Skip(cycleStart).Append(itemName);
            throw new FecscnRegistryException(
                $"Registry dependency cycle detected: {string.Join(" -> ", cycle)}.");
        }

        states[itemName] = VisitState.Visiting;
        stack.Add(itemName);
        foreach (var dependency in itemsByName[itemName].RegistryDependencies
                     .OrderBy(dependency => dependency, StringComparer.Ordinal))
        {
            Visit(dependency, itemsByName, states, stack);
        }
        stack.RemoveAt(stack.Count - 1);
        states[itemName] = VisitState.Visited;
    }

    private static void ValidateFileDeclarations(IEnumerable<FecscnRegistryItem> items)
    {
        var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in item.Files.OrderBy(file => file.Target, StringComparer.Ordinal))
            {
                var source = ValidateRelativePath(file.Source, $"Registry item '{item.Name}' source");
                var target = ValidateRelativePath(file.Target, $"Registry item '{item.Name}' target");
                if (source.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)
                    || source.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FecscnRegistryException(
                        $"Registry item '{item.Name}' source '{source}' uses unsupported .tsx or .jsx input.");
                }
                ValidateFileKind(item.Name, file, source);
                ValidateDigest(file.Sha256, $"Registry item '{item.Name}' file '{source}'");
                if (!sources.Add(source))
                {
                    throw new FecscnRegistryException(
                        $"Registry item '{item.Name}' repeats source '{source}'.");
                }
                if (targets.TryGetValue(target, out var owner))
                {
                    throw new FecscnRegistryException(
                        $"Registry target '{target}' is declared by both '{owner}' and '{item.Name}'.");
                }
                targets.Add(target, item.Name);
            }
        }
    }

    private static PublishedRegistryFile[] ReadFiles(FecscnRegistryItem item, string root)
    {
        var files = new List<PublishedRegistryFile>();
        foreach (var file in item.Files.OrderBy(file => file.Target, StringComparer.Ordinal))
        {
            var sourcePath = ResolveWithinRoot(
                root,
                file.Source,
                $"Registry item '{item.Name}' source");
            if (!System.IO.File.Exists(sourcePath))
            {
                throw new FecscnRegistryException(
                    $"Registry item '{item.Name}' source '{file.Source}' does not exist beneath '{root}'.");
            }
            var bytes = System.IO.File.ReadAllBytes(sourcePath);
            var actualDigest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!actualDigest.Equals(file.Sha256, StringComparison.Ordinal))
            {
                throw new FecscnRegistryException(
                    $"Registry item '{item.Name}' source '{file.Source}' digest mismatch: "
                    + $"expected '{file.Sha256}', actual '{actualDigest}'.");
            }

            string? content = null;
            string? contentBase64 = null;
            if (file.Kind is "fecsx" or "csharp")
            {
                try
                {
                    content = new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false,
                        throwOnInvalidBytes: true).GetString(bytes);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new FecscnRegistryException(
                        $"Registry item '{item.Name}' source '{file.Source}' is not valid UTF-8 text: "
                        + exception.Message);
                }
            }
            else
            {
                contentBase64 = Convert.ToBase64String(bytes);
            }
            files.Add(new(
                file.Source,
                file.Target,
                file.Kind,
                actualDigest,
                content,
                contentBase64));
        }
        return files.ToArray();
    }

    private static void ValidateFileKind(
        string itemName,
        FecscnRegistryFile file,
        string source)
    {
        var expectedKind = Path.GetExtension(source).ToLowerInvariant() switch
        {
            ".fecsx" => "fecsx",
            ".cs" => "csharp",
            _ => "asset"
        };
        if (!file.Kind.Equals(expectedKind, StringComparison.Ordinal))
        {
            throw new FecscnRegistryException(
                $"Registry item '{itemName}' source '{source}' must use kind '{expectedKind}', "
                + $"not '{file.Kind}'.");
        }
    }

    private static string ValidateRelativePath(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Contains('\\')
            || path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new FecscnRegistryException(
                $"{description} path '{path}' escapes its allowed root or is not canonical.");
        }
        return path;
    }

    private static string ResolveWithinRoot(string root, string relativePath, string description)
    {
        var path = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new FecscnRegistryException(
                $"{description} path '{relativePath}' escapes its allowed root.");
        }
        return path;
    }

    private static void ValidateDigest(string digest, string description)
    {
        if (!DigestPattern().IsMatch(digest))
        {
            throw new FecscnRegistryException(
                $"{description} digest must be a lowercase SHA-256 value.");
        }
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }

    private sealed record PublishedRegistryIndex(
        [property: JsonPropertyName("$schema")] string Schema,
        int Version,
        FecscnProfile Profile,
        IReadOnlyList<PublishedRegistryIndexItem> Items);

    private sealed record PublishedRegistryIndexItem(
        string Name,
        string Type,
        string Url,
        FecscnUpstreamSource Upstream,
        IReadOnlyList<NpmDependency> Dependencies,
        IReadOnlyList<string> RegistryDependencies,
        IReadOnlyList<NuGetDependency> Packages);

    private sealed record PublishedRegistryItemDocument(
        [property: JsonPropertyName("$schema")] string Schema,
        int Version,
        string Name,
        string Type,
        FecscnUpstreamSource Upstream,
        IReadOnlyList<NpmDependency> Dependencies,
        IReadOnlyList<string> RegistryDependencies,
        IReadOnlyList<PublishedRegistryFile> Files,
        IReadOnlyList<NuGetDependency> Packages);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ItemNamePattern();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();
}
