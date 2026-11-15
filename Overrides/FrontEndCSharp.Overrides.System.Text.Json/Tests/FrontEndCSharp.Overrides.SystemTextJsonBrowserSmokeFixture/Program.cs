using Interop.JS.Browser;

const string Json = "{\"Location\":\"Seattle\",\"TemperatureC\":21,\"Active\":true}";

var response = System.Text.Json.JsonSerializer.Deserialize<WeatherResponse>(Json);
var succeeded = response?.Location == "Seattle"
    && response.TemperatureC == 21
    && response.Active;

Document.Body.SetAttribute("data-fecs-status", succeeded ? "ready" : "failed");

internal sealed class WeatherResponse
{
    public string? Location { get; set; }

    public int TemperatureC { get; set; }

    public bool Active { get; set; }
}
