global using Interop.JS.MobX.React;

using Interop.JS.Browser;
using Interop.JS.MobX;
using Interop.JS.React;
using MobXApi = Interop.JS.MobX.MobX;

namespace FrontEndCSharp.ReactDemo;

public static class Program
{
    private const string WeatherEndpoint =
        "https://api.open-meteo.com/v1/forecast?latitude=47.6062&longitude=-122.3321&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m&temperature_unit=fahrenheit&wind_speed_unit=mph&timezone=America%2FLos_Angeles";

    public static void Main()
    {
        Document.Title = "Orbit — managed focus";
        MobXApi.Configure(MobXActionEnforcement.Never);
        var steppingFixture = new DebuggerSteppingFixture(1);
        var steppingResult = steppingFixture.Run(2);
        if (steppingResult != 7)
        {
            throw new InvalidOperationException("The debugger stepping fixture produced an invalid result.");
        }

        var host = Document.GetElementById("app")
            ?? throw new InvalidOperationException("The Orbit React host was not found.");
        var store = MobXApi.MakeAutoObservable(new OrbitStore());
        var root = ReactRoot.Create(host);
        root.Render(App.Create(props => props.Store = store));
        _ = WeatherService.LoadAsync(
            store,
            WeatherEndpoint);

        var isSmokeTest = host.GetAttribute("data-fecs-smoke") == "true";
        if (isSmokeTest)
        {
            Window.SetTimeout(() => Actions.ToggleFocus(store), 75);
        }

        Window.SetTimeout(() =>
        {
            var focusCard = Document.QuerySelector(".focus-card");
            var reactReady = focusCard != null
                && Document.QuerySelector(".task-grid") != null
                && Document.QuerySelector("[data-slot='button-group'].energy-grid") != null
                && Document.QuerySelector("[data-slot='progress'].task-progress") != null;
            var mobXReady = !isSmokeTest || Document.QuerySelector(".orbit-visual.is-running") != null;
            var motionStyle = focusCard?.GetAttribute("style");
            var motionReady = motionStyle != null && motionStyle != string.Empty;
            Document.Body.SetAttribute("data-fecsx-react", reactReady ? "ready" : "failed");
            Document.Body.SetAttribute("data-fecsx-mobx", mobXReady ? "ready" : "failed");
            Document.Body.SetAttribute("data-fecsx-motion", motionReady ? "ready" : "failed");
            Document.Body.SetAttribute("data-orbit-status", reactReady && mobXReady && motionReady ? "ready" : "failed");
        }, isSmokeTest ? 500 : 100);
    }
}
