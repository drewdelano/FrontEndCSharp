using Interop.JS.Browser;
using System.Net.Http.Json;

var endpoint = Document.Body.GetAttribute("data-http-json-endpoint");
if (string.IsNullOrWhiteSpace(endpoint))
{
    Document.Body.SetAttribute("data-fecs-status", "missing-endpoint");
    return;
}

using var client = new HttpClient();
var requestPayload = new HttpJsonResponse
{
    Location = "Portland",
    Forecasts =
    [
        new HttpJsonForecast { Day = "Wednesday", TemperatureC = 16 },
    ],
    Measurements = new Dictionary<string, int> { ["Humidity"] = 81 },
};
var echoEndpoint = endpoint.Replace("response.json", "echo.json");
using var postResponse = await client.PostAsJsonAsync(echoEndpoint, requestPayload);
var postPayload = await postResponse.Content.ReadFromJsonAsync<HttpJsonResponse>();

var succeeded = postResponse.IsSuccessStatusCode
    && postPayload?.Location == "Portland"
    && postPayload.Forecasts[0].Day == "Wednesday"
    && postPayload.Measurements["Humidity"] == 81;

Document.Body.SetAttribute(
    "data-fecs-http-json-post-extension",
    postPayload?.Location ?? "null");
Document.Body.SetAttribute("data-fecs-status", succeeded ? "ready" : "failed");

internal sealed class HttpJsonResponse
{
    public string? Location { get; set; }

    public List<HttpJsonForecast> Forecasts { get; set; } = [];

    public Dictionary<string, int> Measurements { get; set; } = [];
}

internal sealed class HttpJsonForecast
{
    public string? Day { get; set; }

    public int TemperatureC { get; set; }
}
