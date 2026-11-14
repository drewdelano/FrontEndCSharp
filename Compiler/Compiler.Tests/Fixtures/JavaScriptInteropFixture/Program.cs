using Interop.JS.Browser;
using Interop.JS.MobX;
using Interop.JS.MobX.React;
using Interop.JS.Motion;
using Interop.JS.React;
using MobXApi = Interop.JS.MobX.MobX;
using MotionApi = Interop.JS.Motion.Motion;

Document.Title = "FrontEndCSharp interop";

var mobXOutput = Document.CreateElement("output");
mobXOutput.Id = "mobx-output";
Document.Body.AppendChild(mobXOutput);

MobXApi.Configure(MobXActionEnforcement.Never);
var count = MobXApi.Box(1);
var reactionRuns = 0;
var reaction = MobXApi.Autorun(() =>
{
    reactionRuns++;
    mobXOutput.TextContent = count.Value == 2 ? "two" : "one";
});
count.Value = 2;
reaction.Dispose();
count.Value = 3;

var model = MobXApi.MakeAutoObservable(new ManagedModel
{
    Age = 18,
    Name = "Ada"
});
var modelReactionRuns = 0;
var observedAge = 0;
var modelReaction = MobXApi.Autorun(() =>
{
    modelReactionRuns++;
    observedAge = model.Age;
});
model.Age = 19;
modelReaction.Dispose();
model.Age = 20;

var motionClicks = 0;
var motionProps = MotionProps.Create();
motionProps.Id = "managed-motion";
motionProps.ClassName = "managed-motion-button";
motionProps.Type = "button";
motionProps.AriaLabel = "Managed Motion";
motionProps.OnClick = _ => motionClicks++;
motionProps.Properties["data-props-source"] = "fallback";
motionProps.Initial = MotionStyle.Create().SetNumber("opacity", 0);
motionProps.Animate = MotionStyle.Create().SetNumber("opacity", 1).SetNumber("x", count.Value);
motionProps.Transition = MotionTransition.Create().SetType("spring").SetDuration(0.25);
var node = MotionApi.Element(
    "button",
    motionProps,
    React.Text("Managed Motion"));

var clicks = 0;
var buttonProps = ReactProps.Create();
buttonProps.Id = "managed-button";
buttonProps.Type = "button";
buttonProps.Properties["label"] = "Managed React";
buttonProps.OnClick = _ => clicks++;
var propertyBagVerified = motionProps is ReactProps
    && string.Equals(motionProps.Id, "managed-motion")
    && string.Equals(motionProps.Type, "button")
    && string.Equals(motionProps.Properties["data-props-source"] as string, "fallback")
    && string.Equals(buttonProps.Properties["label"] as string, "Managed React");
var button = React.Component(props => React.CreateElement(
    "button",
    props,
    React.Text(props.GetString("label") ?? "missing")));
var typedPanel = React.Component<ManagedPanelProps>((props, children) =>
{
    var panelProps = ReactProps.Create();
    panelProps.Id = "typed-panel";
    return React.CreateElement(
        "article",
        panelProps,
        React.CreateElement("h2", null, React.Text(props.Title)),
        React.CreateElement("div", null, children));
});
var observedPanel = MobXReact.Observer(React.Component<ManagedModelProps>((props, _) => React.CreateElement(
    "output",
    ReactProps.Create().SetString("id", "observed-age"),
    React.Text(props.Model.Age == 21 ? "updated" : "initial"))));
var observedRegionPanel = React.Component<ManagedModelProps>((props, _) => MobXReact.Observe(() => React.CreateElement(
    "output",
    ReactProps.Create().SetString("id", "observed-region-age"),
    React.Text(props.Model.Age == 21 ? "updated" : "initial"))));
var motionHost = React.CreateElement(
    "section",
    ReactProps.Create().SetString("id", "motion-host"),
    node);
var application = React.CreateElement(
    "main",
    ReactProps.Create().SetString("id", "managed-root"),
    React.CreateElement(button, buttonProps),
    React.CreateElement(
        typedPanel,
        new ManagedPanelProps { Title = "Typed composition" },
        React.Text("Managed children")),
    React.CreateElement(observedPanel, new ManagedModelProps { Model = model }),
    React.CreateElement(observedRegionPanel, new ManagedModelProps { Model = model }),
    motionHost);

var reactHost = Document.CreateElement("div");
reactHost.Id = "react-host";
Document.Body.AppendChild(reactHost);
var root = ReactRoot.Create(reactHost);
root.Render(application);
Window.SetTimeout(() => model.Age = 21, 10);

var browserSurfaceVerified = false;
var microtaskRan = false;
var intervalRuns = 0;
var intervalHandle = 0;
intervalHandle = Window.SetInterval(() =>
{
    intervalRuns++;
    Window.ClearInterval(intervalHandle);
}, 1);
Window.QueueMicrotask(() =>
{
    microtaskRan = true;
    var host = Document.CreateElement("section");
    host.Id = "browser-probe";
    host.ClassName = "browser-surface";
    host.SetAttribute("data-remove", "yes");

    var buttonElement = Document.CreateElement("button");
    buttonElement.Id = "browser-probe-button";
    buttonElement.ClassName = "browser-probe-match";
    buttonElement.InnerText = "Browser surface";
    var secondaryElement = Document.CreateElement("span");
    secondaryElement.ClassName = "browser-probe-match";
    var inputElement = Document.CreateElement("input");
    inputElement.SetAttribute("value", "managed input");
    var marker = Document.CreateTextNode("marker");
    host.AppendChild(buttonElement);
    host.AppendChild(secondaryElement);
    host.AppendChild(inputElement);
    host.InsertBefore(marker, buttonElement);
    host.RemoveChild(marker);
    host.RemoveAttribute("data-remove");
    Document.Body.AppendChild(host);

    var eventObserved = false;
    buttonElement.AddEventListener("click", domEvent =>
    {
        eventObserved = domEvent.Bubbles
            && domEvent.Cancelable
            && !domEvent.DefaultPrevented;
        domEvent.PreventDefault();
        eventObserved = eventObserved && domEvent.DefaultPrevented;
        domEvent.StopPropagation();
        domEvent.StopImmediatePropagation();
    });
    buttonElement.Focus();
    buttonElement.Click();
    var inputEventObserved = false;
    inputElement.AddEventListener("click", domEvent =>
    {
        var currentTarget = domEvent.CurrentTarget;
        inputEventObserved = currentTarget != null
            && string.Equals(currentTarget.Value, "managed input");
    });
    inputElement.Click();
    var activeElement = Document.ActiveElement;

    browserSurfaceVerified = Document.Head != null
        && Document.DocumentElement != null
        && activeElement != null
        && !string.Equals(Document.ReadyState, "loading")
        && Window.DevicePixelRatio > 0
        && host.IsConnected
        && host.HasChildNodes()
        && host.FirstChild != null
        && host.LastChild != null
        && host.ParentNode != null
        && string.Equals(host.ClassName, "browser-surface")
        && string.Equals(buttonElement.TagName, "BUTTON")
        && string.Equals(buttonElement.InnerText, "Browser surface")
        && !host.HasAttribute("data-remove")
        && host.QuerySelector("#browser-probe-button") != null
        && Document.QuerySelectorAll(".browser-probe-match").Length == 2
        && Document.QuerySelectorAll(".browser-probe-missing").Length == 0
        && string.Equals(activeElement.Id, "browser-probe-button")
        && eventObserved
        && inputEventObserved;
    buttonElement.Blur();
});

Window.SetTimeout(() =>
{
    Document.GetElementById("managed-button")?.Click();
    Document.GetElementById("managed-motion")?.Click();
    var succeeded = clicks == 1
        && motionClicks == 1
        && propertyBagVerified
        && reactionRuns == 2
        && modelReactionRuns == 2
        && observedAge == 19
        && string.Equals(model.Name, "Ada")
        && string.Equals(null, null)
        && !string.Equals(null, model.Name)
        && typeof(ManagedModel) == typeof(ManagedModel)
        && typeof(ManagedModel) != typeof(ManagedPanelProps)
        && browserSurfaceVerified
        && microtaskRan
        && intervalRuns == 1
        && Document.QuerySelector("#motion-host > button") != null
        && string.Equals(Document.GetElementById("typed-panel")?.TextContent, "Typed compositionManaged children")
        && string.Equals(Document.GetElementById("observed-age")?.InnerText, "updated")
        && string.Equals(Document.GetElementById("observed-region-age")?.InnerText, "updated")
        && string.Equals(Document.GetElementById("managed-motion")?.GetAttribute("data-props-source"), "fallback");
    Document.Body.SetAttribute("data-fecs-status", succeeded ? "ready" : "failed");
}, 50);

internal sealed class ManagedModel
{
    public int Age { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class ManagedPanelProps
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class ManagedModelProps
{
    public ManagedModel Model { get; set; } = null!;
}
