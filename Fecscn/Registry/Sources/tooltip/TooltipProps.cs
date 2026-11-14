using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum TooltipSide
{
    Top,
    Bottom,
    Left,
    Right
}

public enum TooltipAlign
{
    Center,
    Start,
    End
}

public sealed class TooltipProviderProps
{
    public double Delay { get; set; }
    public double CloseDelay { get; set; }
}

public sealed class TooltipProps
{
    public bool? Open { get; set; }
    public bool? DefaultOpen { get; set; }
}

public sealed class TooltipTriggerProps
{
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class TooltipContentProps
{
    public string? ClassName { get; set; }
    public TooltipSide Side { get; set; }
    public double SideOffset { get; set; } = 4;
    public TooltipAlign Align { get; set; }
    public double AlignOffset { get; set; }
}

public static class TooltipPrimitive
{
    public static ReactProps CreateProviderProps(TooltipProviderProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetString("data-slot", "tooltip-provider");
        importedProps.SetNumber("delay", props.Delay);
        importedProps.SetNumber("closeDelay", props.CloseDelay);
        return importedProps;
    }

    public static ReactProps CreateRootProps(TooltipProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetString("data-slot", "tooltip");
        if (props.Open.HasValue)
        {
            importedProps.SetBoolean("open", props.Open.Value);
        }
        if (props.DefaultOpen.HasValue)
        {
            importedProps.SetBoolean("defaultOpen", props.DefaultOpen.Value);
        }
        return importedProps;
    }

    public static ReactProps CreateTriggerProps(TooltipTriggerProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.AriaLabel = props.AriaLabel;
        importedProps.ClassName = props.ClassName;
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", "tooltip-trigger");
        return importedProps;
    }

    public static ReactProps CreatePositionerProps(TooltipContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = TooltipStyles.Positioner;
        importedProps.SetString("side", Side(props.Side));
        importedProps.SetNumber("sideOffset", props.SideOffset);
        importedProps.SetString("align", Align(props.Align));
        importedProps.SetNumber("alignOffset", props.AlignOffset);
        return importedProps;
    }

    public static ReactProps CreatePopupProps(TooltipContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(TooltipStyles.Popup, props.ClassName);
        importedProps.SetString("data-slot", "tooltip-content");
        return importedProps;
    }

    public static ReactProps CreateArrowProps()
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = TooltipStyles.Arrow;
        importedProps.SetString("data-slot", "tooltip-arrow");
        return importedProps;
    }

    private static string Side(TooltipSide value)
    {
        if (value == TooltipSide.Bottom) return "bottom";
        if (value == TooltipSide.Left) return "left";
        if (value == TooltipSide.Right) return "right";
        return "top";
    }

    private static string Align(TooltipAlign value)
    {
        if (value == TooltipAlign.Start) return "start";
        if (value == TooltipAlign.End) return "end";
        return "center";
    }
}

public static class TooltipStyles
{
    public const string Positioner = "isolate z-50";
    public const string Popup = "z-50 inline-flex w-fit max-w-xs origin-(--transform-origin) items-center gap-1.5 rounded-md bg-foreground px-3 py-1.5 text-xs text-background has-data-[slot=kbd]:pr-1.5 data-[side=bottom]:slide-in-from-top-2 data-[side=inline-end]:slide-in-from-left-2 data-[side=inline-start]:slide-in-from-right-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 **:data-[slot=kbd]:relative **:data-[slot=kbd]:isolate **:data-[slot=kbd]:z-50 **:data-[slot=kbd]:rounded-sm data-[state=delayed-open]:animate-in data-[state=delayed-open]:fade-in-0 data-[state=delayed-open]:zoom-in-95 data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95";
    public const string Arrow = "z-50 size-2.5 translate-y-[calc(-50%-2px)] rotate-45 rounded-[2px] bg-foreground fill-foreground data-[side=bottom]:top-1 data-[side=inline-end]:top-1/2! data-[side=inline-end]:-left-1 data-[side=inline-end]:-translate-y-1/2 data-[side=inline-start]:top-1/2! data-[side=inline-start]:-right-1 data-[side=inline-start]:-translate-y-1/2 data-[side=left]:top-1/2! data-[side=left]:-right-1 data-[side=left]:-translate-y-1/2 data-[side=right]:top-1/2! data-[side=right]:-left-1 data-[side=right]:-translate-y-1/2 data-[side=top]:-bottom-2.5";
}
