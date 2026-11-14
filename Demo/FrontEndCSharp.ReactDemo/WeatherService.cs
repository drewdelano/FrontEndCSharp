using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace FrontEndCSharp.ReactDemo;

internal static class WeatherService
{
    public static async Task LoadAsync(OrbitStore store, string endpoint)
    {
        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                SetFailure(store);
                return;
            }

            var forecast = await response.Content.ReadFromJsonAsync<OpenMeteoForecast>();
            var current = forecast?.Current;
            if (current == null)
            {
                SetFailure(store);
                return;
            }

            var condition = DescribeWeatherCode(current.WeatherCode);
            store.WeatherTemperatureF = current.TemperatureF;
            store.WeatherHumidityPercent = current.RelativeHumidity;
            store.WeatherWindSpeedMph = current.WindSpeedMph;
            store.WeatherCondition = condition;
            store.WeatherObservedAt = current.Time;
            store.WeatherFailed = false;
            store.WeatherLoaded = true;
        }
        catch (Exception)
        {
            SetFailure(store);
        }
    }

    private static void SetFailure(OrbitStore store)
    {
        store.WeatherLoaded = false;
        store.WeatherFailed = true;
    }

    private static string DescribeWeatherCode(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Foggy",
        51 or 53 or 55 => "Drizzle",
        56 or 57 => "Freezing drizzle",
        61 or 63 or 65 => "Rain",
        66 or 67 => "Freezing rain",
        71 or 73 or 75 => "Snow",
        77 => "Snow grains",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm with hail",
        _ => "Current conditions",
    };
}

public sealed class OpenMeteoForecast
{
    [JsonInclude]
    [JsonPropertyName("current")]
    public OpenMeteoCurrentWeather? Current;
}

public  sealed class OpenMeteoCurrentWeather
{
    [JsonInclude]
    [JsonPropertyName("time")]
    public string Time = string.Empty;

    [JsonInclude]
    [JsonPropertyName("temperature_2m")]
    public double TemperatureF;

    [JsonInclude]
    [JsonPropertyName("relative_humidity_2m")]
    public int RelativeHumidity;

    [JsonInclude]
    [JsonPropertyName("weather_code")]
    public int WeatherCode;

    [JsonInclude]
    [JsonPropertyName("wind_speed_10m")]
    public double WindSpeedMph;
}
