using Interop.JS.React;

namespace Fecscn.Components.UI;

public sealed class ButtonGroupProps
{
    public string? ClassName { get; set; }
    public ComponentOrientation Orientation { get; set; }
}

public sealed class ButtonGroupTextProps
{
    public string? ClassName { get; set; }
}

public sealed class ButtonGroupSeparatorProps
{
    public string? ClassName { get; set; }
    public ComponentOrientation Orientation { get; set; } = ComponentOrientation.Vertical;
}

public static class ButtonGroupPrimitive
{
    public static ReactProps CreateSeparatorProps(ButtonGroupSeparatorProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(ButtonGroupStyles.Separator, props.ClassName);
        importedProps.SetString("data-slot", "button-group-separator");
        importedProps.SetString("orientation", SeparatorPrimitive.Orientation(props.Orientation));
        return importedProps;
    }
}

public static class ButtonGroupStyles
{
    public const string Base = "flex w-fit items-stretch *:focus-visible:relative *:focus-visible:z-10 has-[>[data-slot=button-group]]:gap-2 has-[select[aria-hidden=true]:last-child]:[&>[data-slot=select-trigger]:last-of-type]:rounded-r-lg [&>[data-slot=select-trigger]:not([class*='w-'])]:w-fit [&>input]:flex-1";
    public const string Text = "flex items-center gap-2 rounded-lg border bg-muted px-2.5 text-sm font-medium [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4";
    public const string Separator = "relative self-stretch bg-input data-horizontal:mx-px data-horizontal:w-auto data-vertical:my-px data-vertical:h-auto";

    public static string Orientation(ComponentOrientation value) =>
        value == ComponentOrientation.Vertical
            ? "flex-col *:data-slot:rounded-b-none [&>[data-slot]:not(:has(~[data-slot]))]:rounded-b-lg! [&>[data-slot]~[data-slot]]:rounded-t-none [&>[data-slot]~[data-slot]]:border-t-0"
            : "*:data-slot:rounded-r-none [&>[data-slot]:not(:has(~[data-slot]))]:rounded-r-lg! [&>[data-slot]~[data-slot]]:rounded-l-none [&>[data-slot]~[data-slot]]:border-l-0";
}
