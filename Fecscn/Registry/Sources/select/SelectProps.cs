using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum SelectTriggerSize
{
    Default,
    Small
}

public enum SelectSide
{
    Bottom,
    Top,
    Left,
    Right
}

public enum SelectAlign
{
    Center,
    Start,
    End
}

public sealed class SelectProps
{
    public string? Value { get; set; }
    public string? DefaultValue { get; set; }
    public string? Name { get; set; }
    public bool Disabled { get; set; }
    public bool Required { get; set; }
}

public sealed class SelectValueProps
{
    public string? ClassName { get; set; }
    public string? Placeholder { get; set; }
}

public sealed class SelectTriggerProps
{
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public SelectTriggerSize Size { get; set; }
    public bool Disabled { get; set; }
}

public sealed class SelectContentProps
{
    public string? ClassName { get; set; }
    public SelectSide Side { get; set; }
    public double SideOffset { get; set; } = 4;
    public SelectAlign Align { get; set; }
    public double AlignOffset { get; set; }
    public bool AlignItemWithTrigger { get; set; } = true;
}

public sealed class SelectItemProps
{
    public string? ClassName { get; set; }
    public string? Value { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class SelectPartProps
{
    public string? ClassName { get; set; }
}

public static class SelectPrimitive
{
    public static ReactProps CreateRootProps(SelectProps props)
    {
        var importedProps = ReactProps.Create();
        if (props.Value is not null)
        {
            importedProps.SetString("value", props.Value);
        }
        if (props.DefaultValue is not null)
        {
            importedProps.SetString("defaultValue", props.DefaultValue);
        }
        importedProps.SetString("name", props.Name);
        importedProps.SetBoolean("disabled", props.Disabled);
        importedProps.SetBoolean("required", props.Required);
        return importedProps;
    }

    public static ReactProps CreateValueProps(SelectValueProps props)
    {
        var importedProps = CreatePartProps("select-value", SelectStyles.Value, new SelectPartProps { ClassName = props.ClassName });
        importedProps.SetString("placeholder", props.Placeholder);
        return importedProps;
    }

    public static ReactProps CreateTriggerProps(SelectTriggerProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.AriaLabel = props.AriaLabel;
        importedProps.ClassName = ClassNames.Merge(SelectStyles.Trigger, props.ClassName);
        importedProps.SetString("data-slot", "select-trigger");
        importedProps.SetString("data-size", props.Size == SelectTriggerSize.Small ? "sm" : "default");
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateIconProps()
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = SelectStyles.Icon;
        importedProps.SetString("data-slot", "select-icon");
        return importedProps;
    }

    public static ReactProps CreatePositionerProps(SelectContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = SelectStyles.Positioner;
        importedProps.SetString("side", Side(props.Side));
        importedProps.SetNumber("sideOffset", props.SideOffset);
        importedProps.SetString("align", Align(props.Align));
        importedProps.SetNumber("alignOffset", props.AlignOffset);
        importedProps.SetBoolean("alignItemWithTrigger", props.AlignItemWithTrigger);
        return importedProps;
    }

    public static ReactProps CreatePopupProps(SelectContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(SelectStyles.Popup, props.ClassName);
        importedProps.SetString("data-slot", "select-content");
        importedProps.SetBoolean("data-align-trigger", props.AlignItemWithTrigger);
        return importedProps;
    }

    public static ReactProps CreateItemProps(SelectItemProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(SelectStyles.Item, props.ClassName);
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", "select-item");
        importedProps.SetString("value", props.Value);
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateItemTextProps()
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = SelectStyles.ItemText;
        return importedProps;
    }

    public static ReactProps CreateItemIndicatorProps()
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = SelectStyles.ItemIndicator;
        importedProps.SetString("data-slot", "select-item-indicator");
        return importedProps;
    }

    public static ReactProps CreatePartProps(string slot, string baseClasses, SelectPartProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(baseClasses, props.ClassName);
        importedProps.SetString("data-slot", slot);
        return importedProps;
    }

    private static string Side(SelectSide value)
    {
        if (value == SelectSide.Top) return "top";
        if (value == SelectSide.Left) return "left";
        if (value == SelectSide.Right) return "right";
        return "bottom";
    }

    private static string Align(SelectAlign value)
    {
        if (value == SelectAlign.Start) return "start";
        if (value == SelectAlign.End) return "end";
        return "center";
    }
}

public static class SelectStyles
{
    public const string Group = "scroll-my-1 p-1";
    public const string Value = "flex flex-1 text-left";
    public const string Trigger = "flex w-fit items-center justify-between gap-1.5 rounded-lg border border-input bg-transparent py-2 pr-2 pl-2.5 text-sm whitespace-nowrap transition-colors outline-none select-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 data-placeholder:text-muted-foreground data-[size=default]:h-8 data-[size=sm]:h-7 data-[size=sm]:rounded-[min(var(--radius-md),10px)] *:data-[slot=select-value]:line-clamp-1 *:data-[slot=select-value]:flex *:data-[slot=select-value]:items-center *:data-[slot=select-value]:gap-1.5 dark:bg-input/30 dark:hover:bg-input/50 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4";
    public const string Icon = "pointer-events-none size-4 text-muted-foreground";
    public const string Positioner = "isolate z-50";
    public const string Popup = "cn-menu-target cn-menu-translucent relative isolate z-50 max-h-(--available-height) w-(--anchor-width) min-w-36 origin-(--transform-origin) overflow-x-hidden overflow-y-auto rounded-lg bg-popover text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100 data-[align-trigger=true]:animate-none data-[side=bottom]:slide-in-from-top-2 data-[side=inline-end]:slide-in-from-left-2 data-[side=inline-start]:slide-in-from-right-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95";
    public const string Label = "px-1.5 py-1 text-xs text-muted-foreground";
    public const string Item = "relative flex w-full cursor-default items-center gap-1.5 rounded-md py-1 pr-8 pl-1.5 text-sm outline-hidden select-none focus:bg-accent focus:text-accent-foreground not-data-[variant=destructive]:focus:**:text-accent-foreground data-disabled:pointer-events-none data-disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4 *:[span]:last:flex *:[span]:last:items-center *:[span]:last:gap-2";
    public const string ItemText = "flex flex-1 shrink-0 gap-2 whitespace-nowrap";
    public const string ItemIndicator = "pointer-events-none absolute right-2 flex size-4 items-center justify-center";
    public const string Separator = "pointer-events-none -mx-1 my-1 h-px bg-border";
    public const string ScrollUp = "top-0 z-10 flex w-full cursor-default items-center justify-center bg-popover py-1 [&_svg:not([class*='size-'])]:size-4";
    public const string ScrollDown = "bottom-0 z-10 flex w-full cursor-default items-center justify-center bg-popover py-1 [&_svg:not([class*='size-'])]:size-4";
}
