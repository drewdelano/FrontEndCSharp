using Interop.JS.Browser;
using Newtonsoft.Json;

const string Json = "{\"Location\":\"Seattle\",\"TemperatureC\":21,\"Active\":true}";

var response = JsonConvert.DeserializeObject<WeatherResponse>(Json);
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
