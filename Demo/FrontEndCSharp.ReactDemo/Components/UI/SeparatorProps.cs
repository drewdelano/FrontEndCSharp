using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum ComponentOrientation
{
    Horizontal,
    Vertical
}

public sealed class SeparatorProps
{
    public string? ClassName { get; set; }
    public ComponentOrientation Orientation { get; set; }
}

public static class SeparatorPrimitive
{
    public static ReactProps CreateProps(SeparatorProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(SeparatorStyles.Base, props.ClassName);
        importedProps.SetString("data-slot", "separator");
        importedProps.SetString("orientation", Orientation(props.Orientation));
        return importedProps;
    }

    public static string Orientation(ComponentOrientation value) =>
        value == ComponentOrientation.Vertical ? "vertical" : "horizontal";
}

public static class SeparatorStyles
{
    public const string Base = "shrink-0 bg-border data-horizontal:h-px data-horizontal:w-full data-vertical:w-px data-vertical:self-stretch";
}
