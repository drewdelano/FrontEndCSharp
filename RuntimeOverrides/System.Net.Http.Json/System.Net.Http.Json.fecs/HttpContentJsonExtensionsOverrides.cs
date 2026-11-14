using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.JsonRuntimeOverrides;

[RuntimeOverrideType(typeof(HttpContentJsonExtensions))]
internal static class HttpContentJsonExtensionsOverrides
{
    [JavaScriptOverride]
    internal static Task<T?> ReadFromJsonAsync<T>(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        _ = content.ReadAsStringAsync(cancellationToken);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _ = JsonSerializer.Deserialize<T>(string.Empty, options);
        throw new NotSupportedException();
    }
}

[RuntimeOverrideType(typeof(HttpClientJsonExtensions))]
internal static class HttpClientJsonExtensionsOverrides
{
    [JavaScriptOverride]
    internal static Task<TValue?> GetFromJsonAsync<TValue>(
        HttpClient client,
        string? requestUri,
        CancellationToken cancellationToken)
    {
        _ = client.GetAsync(requestUri, cancellationToken);
        _ = default(HttpResponseMessage)!.Content.ReadFromJsonAsync<TValue>(cancellationToken);
        throw new NotSupportedException();
    }

    [JavaScriptOverride]
    internal static Task<HttpResponseMessage> PostAsJsonAsync<TValue>(
        HttpClient client,
        string? requestUri,
        TValue value,
        JsonSerializerOptions? options,
        CancellationToken cancellationToken)
    {
        options ??= new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var content = new StringContent(JsonSerializer.Serialize(value, options));
        _ = cancellationToken.IsCancellationRequested;
        _ = client.PostAsync(requestUri, content);
        throw new NotSupportedException();
    }

    [JavaScriptOverride]
    internal static Task<HttpResponseMessage> PutAsJsonAsync<TValue>(
        HttpClient client,
        string? requestUri,
        TValue value,
        JsonSerializerOptions? options,
        CancellationToken cancellationToken)
    {
        options ??= new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var content = new StringContent(JsonSerializer.Serialize(value, options));
        _ = cancellationToken.IsCancellationRequested;
        _ = client.PutAsync(requestUri, content);
        throw new NotSupportedException();
    }
}
