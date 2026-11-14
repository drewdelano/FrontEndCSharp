using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FrontEndCSharp.Fecscn.Commands;

namespace FrontEndCSharp.Fecscn.Registry;

public interface IFecscnRegistrySource
{
    Task<PublishedRegistryItem> GetItemAsync(string name, CancellationToken cancellationToken = default);
}

public sealed class HttpFecscnRegistrySource : IFecscnRegistrySource
{
    private readonly HttpClient client;
    private readonly Uri baseUri;

    public HttpFecscnRegistrySource(HttpClient client, string registry)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        baseUri = new Uri(registry.TrimEnd('/') + "/", UriKind.Absolute);
    }

    public async Task<PublishedRegistryItem> GetItemAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(new Uri(baseUri, Uri.EscapeDataString(name) + ".json"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new FecscnCommandException(
                $"Registry item '{name}' could not be fetched: HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }
        try
        {
            return await JsonSerializer.DeserializeAsync<PublishedRegistryItem>(
                       await response.Content.ReadAsStreamAsync(cancellationToken),
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                       cancellationToken)
                   ?? throw new FecscnCommandException($"Registry item '{name}' contains JSON null.");
        }
        catch (JsonException exception)
        {
            throw new FecscnCommandException($"Registry item '{name}' contains invalid JSON: {exception.Message}");
        }
    }
}

public sealed record FecscnInstallationPlan(IReadOnlyList<PublishedRegistryItem> Items);

public static class FecscnRegistryResolver
{
    public static async Task<FecscnInstallationPlan> ResolveAsync(
        IFecscnRegistrySource source,
        IReadOnlyList<string> requestedItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (requestedItems.Count == 0)
        {
            throw new FecscnCommandException("fecscn add requires at least one component name.");
        }
        var resolved = new Dictionary<string, PublishedRegistryItem>(StringComparer.Ordinal);
        var visiting = new List<string>();
        var ordered = new List<PublishedRegistryItem>();

        async Task Visit(string name)
        {
            if (resolved.ContainsKey(name))
            {
                return;
            }
            var cycleAt = visiting.IndexOf(name);
            if (cycleAt >= 0)
            {
                throw new FecscnCommandException(
                    $"Registry dependency cycle detected: {string.Join(" -> ", visiting.Skip(cycleAt).Append(name))}.");
            }
            visiting.Add(name);
            var item = await source.GetItemAsync(name, cancellationToken);
            Validate(item, name);
            foreach (var dependency in item.RegistryDependencies.OrderBy(value => value, StringComparer.Ordinal))
            {
                await Visit(dependency);
            }
            visiting.RemoveAt(visiting.Count - 1);
            resolved.Add(name, item);
            ordered.Add(item);
        }

        foreach (var requested in requestedItems.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            await Visit(requested);
        }
        return new(ordered);
    }

    private static void Validate(PublishedRegistryItem item, string expectedName)
    {
        if (item.Version != 1 || item.Name != expectedName || item.Type is not ("registry:ui" or "registry:lib"))
        {
            throw new FecscnCommandException(
                $"Registry item '{expectedName}' has an unsupported schema version, name, or type.");
        }
        foreach (var file in item.Files)
        {
            byte[] bytes;
            if (file.Type is "fecsx" or "csharp")
            {
                if (file.Content is null || file.ContentBase64 is not null)
                {
                    throw new FecscnCommandException($"Registry item '{item.Name}' has invalid text content for '{file.Target}'.");
                }
                bytes = Encoding.UTF8.GetBytes(file.Content);
            }
            else if (file.Type == "asset" && file.Content is null && file.ContentBase64 is not null)
            {
                try { bytes = Convert.FromBase64String(file.ContentBase64); }
                catch (FormatException) { throw new FecscnCommandException($"Registry item '{item.Name}' has invalid base64 content for '{file.Target}'."); }
            }
            else
            {
                throw new FecscnCommandException($"Registry item '{item.Name}' has unsupported file type '{file.Type}'.");
            }
            var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(digest, file.Sha256, StringComparison.Ordinal))
            {
                throw new FecscnCommandException(
                    $"Registry item '{item.Name}' content digest mismatch for '{file.Target}'.");
            }
        }
    }
}
