namespace FrontEndCSharp.ReactDemo;

public sealed class OrbitStore
{
    public bool IsRunning { get; set; }
    public int FocusLength { get; set; } = 25;
    public string FocusLengthLabel { get; set; } = "25 minute session";
    public int EnergyLevel { get; set; } = 2;
    public bool FirstTaskDone { get; set; }
    public bool SecondTaskDone { get; set; } = true;
    public bool ThirdTaskDone { get; set; }
    public string FocusIntent { get; set; } = string.Empty;
    public string AccountabilityEmail { get; set; } = string.Empty;
    public string PriorityInput { get; set; } = string.Empty;
    public string FocusIntentError { get; set; } = string.Empty;
    public string AccountabilityEmailError { get; set; } = string.Empty;
    public string PriorityError { get; set; } = string.Empty;
    public string FocusIntentSuccess { get; set; } = string.Empty;
    public bool WeatherLoaded { get; set; }
    public bool WeatherFailed { get; set; }
    public double WeatherTemperatureF { get; set; }
    public int WeatherHumidityPercent { get; set; }
    public double WeatherWindSpeedMph { get; set; }
    public string WeatherCondition { get; set; } = string.Empty;
    public string WeatherObservedAt { get; set; } = string.Empty;

    public int CompletedCount =>
        (FirstTaskDone ? 1 : 0)
        + (SecondTaskDone ? 1 : 0)
        + (ThirdTaskDone ? 1 : 0);

    public string CompletedLabel =>
        CompletedCount == 0 ? "0 / 3 complete"
        : CompletedCount == 1 ? "1 / 3 complete"
        : CompletedCount == 2 ? "2 / 3 complete"
        : "3 / 3 complete";

    public double ProgressValue => CompletedCount * 100d / 3d;
}
