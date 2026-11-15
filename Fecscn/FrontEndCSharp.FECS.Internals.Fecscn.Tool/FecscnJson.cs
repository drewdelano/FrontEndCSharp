using System.Text.Json;
using System.Text.Json.Serialization;
using FrontEndCSharp.Fecscn.Configuration;
using FrontEndCSharp.Fecscn.Registry;

namespace FrontEndCSharp.Fecscn;

public static class FecscnJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static string SerializeProjectConfig(FecscnProjectConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        return JsonSerializer.Serialize(configuration, Options);
    }

    public static FecscnProjectConfig ParseProjectConfig(string json)
    {
        var configuration = Deserialize<FecscnProjectConfig>(json, "fecscn project configuration");
        configuration.Validate();
        return configuration;
    }

    public static string SerializeRegistryDocument(FecscnRegistryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static FecscnRegistryDocument ParseRegistryDocument(string json) =>
        Deserialize<FecscnRegistryDocument>(json, "fecscn registry authoring document");

    private static T Deserialize<T>(string json, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                   ?? throw new FecscnConfigurationException(
                       $"The {description} contains JSON null instead of an object.");
        }
        catch (JsonException exception)
        {
            throw new FecscnConfigurationException(
                $"The {description} is invalid: {exception.Message}");
        }
    }
}
