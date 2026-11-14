using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum DropdownMenuItemVariant { Default, Destructive }
public enum DropdownMenuSide { Bottom, Top, Left, Right }
public enum DropdownMenuAlign { Start, Center, End }

public sealed class DropdownMenuProps { public bool? Open { get; set; } public bool? DefaultOpen { get; set; } }
public sealed class DropdownMenuActionProps { public string? ClassName { get; set; } public bool Disabled { get; set; } public Action<DomEvent>? OnClick { get; set; } }
public sealed class DropdownMenuPartProps { public string? ClassName { get; set; } }
public sealed class DropdownMenuInsetProps { public string? ClassName { get; set; } public bool Inset { get; set; } }

public sealed class DropdownMenuContentProps
{
    public string? ClassName { get; set; }
    public DropdownMenuAlign Align { get; set; }
    public double AlignOffset { get; set; }
    public DropdownMenuSide Side { get; set; }
    public double SideOffset { get; set; } = 4;
}

public sealed class DropdownMenuItemProps
{
    public string? ClassName { get; set; }
    public bool Inset { get; set; }
    public bool Disabled { get; set; }
    public DropdownMenuItemVariant Variant { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class DropdownMenuCheckboxItemProps
{
    public string? ClassName { get; set; }
    public bool Inset { get; set; }
    public bool? Checked { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class DropdownMenuRadioGroupProps { public string? Value { get; set; } public string? DefaultValue { get; set; } }
public sealed class DropdownMenuRadioItemProps { public string? ClassName { get; set; } public string? Value { get; set; } public bool Inset { get; set; } public bool Disabled { get; set; } public Action<DomEvent>? OnClick { get; set; } }

public static class DropdownMenuPrimitive
{
    public static ReactProps CreateRootProps(DropdownMenuProps props)
    {
        var result = ReactProps.Create();
        result.SetString("data-slot", "dropdown-menu");
        if (props.Open.HasValue) result.SetBoolean("open", props.Open.Value);
        if (props.DefaultOpen.HasValue) result.SetBoolean("defaultOpen", props.DefaultOpen.Value);
        return result;
    }

    public static ReactProps CreateActionProps(string slot, DropdownMenuActionProps props)
    {
        var result = ReactProps.Create(); result.ClassName = props.ClassName; result.OnClick = props.OnClick;
        result.SetString("data-slot", slot); result.SetBoolean("disabled", props.Disabled); return result;
    }

    public static ReactProps CreatePartProps(string slot, string? classes, DropdownMenuPartProps props)
    {
        var result = ReactProps.Create(); result.ClassName = ClassNames.Merge(classes, props.ClassName); result.SetString("data-slot", slot); return result;
    }

    public static ReactProps CreateInsetProps(string slot, string classes, DropdownMenuInsetProps props)
    {
        var result = ReactProps.Create(); result.ClassName = ClassNames.Merge(classes, props.ClassName); result.SetString("data-slot", slot); result.SetBoolean("data-inset", props.Inset); return result;
    }

    public static ReactProps CreatePositionerProps(DropdownMenuContentProps props)
    {
        var result = ReactProps.Create(); result.ClassName = DropdownMenuStyles.Positioner;
        result.SetString("align", Align(props.Align)); result.SetNumber("alignOffset", props.AlignOffset);
        result.SetString("side", Side(props.Side)); result.SetNumber("sideOffset", props.SideOffset); return result;
    }

    public static ReactProps CreatePopupProps(DropdownMenuContentProps props, string slot, string classes)
    {
        var result = ReactProps.Create(); result.ClassName = ClassNames.Merge(classes, props.ClassName); result.SetString("data-slot", slot); return result;
    }

    public static ReactProps CreateItemProps(DropdownMenuItemProps props)
    {
        var result = ReactProps.Create(); result.ClassName = ClassNames.Merge(DropdownMenuStyles.Item, props.ClassName); result.OnClick = props.OnClick;
        result.SetString("data-slot", "dropdown-menu-item"); result.SetBoolean("data-inset", props.Inset); result.SetString("data-variant", props.Variant == DropdownMenuItemVariant.Destructive ? "destructive" : "default"); result.SetBoolean("disabled", props.Disabled); return result;
    }

    public static ReactProps CreateCheckboxItemProps(DropdownMenuCheckboxItemProps props)
    {
        var result = ReactProps.Create(); result.ClassName = ClassNames.Merge(DropdownMenuStyles.ChoiceItem, props.ClassName); result.OnClick = props.OnClick;
        result.SetString("data-slot", "dropdown-menu-checkbox-item"); result.SetBoolean("data-inset", props.Inset); result.SetBoolean("disabled", props.Disabled); if (props.Checked.HasValue) result.SetBoolean("checked", props.Checked.Value); return result;
    }

    public static ReactProps CreateRadioGroupProps(DropdownMenuRadioGroupProps props)
    {
        var result = ReactProps.Create(); result.SetString("data-slot", "dropdown-menu-radio-group");
        if (props.Value is not null) result.SetString("value", props.Value); if (props.DefaultValue is not null) result.SetString("defaultValue", props.DefaultValue); return result;
    }

    public static ReactProps CreateRadioItemProps(DropdownMenuRadioItemProps props)
    {
        var result = ReactProps.Create(); result.ClassName = ClassNames.Merge(DropdownMenuStyles.ChoiceItem, props.ClassName); result.OnClick = props.OnClick;
        result.SetString("data-slot", "dropdown-menu-radio-item"); result.SetString("value", props.Value); result.SetBoolean("data-inset", props.Inset); result.SetBoolean("disabled", props.Disabled); return result;
    }

    private static string Align(DropdownMenuAlign value) { if (value == DropdownMenuAlign.Center) return "center"; if (value == DropdownMenuAlign.End) return "end"; return "start"; }
    private static string Side(DropdownMenuSide value) { if (value == DropdownMenuSide.Top) return "top"; if (value == DropdownMenuSide.Left) return "left"; if (value == DropdownMenuSide.Right) return "right"; return "bottom"; }
}

public static class DropdownMenuStyles
{
    public const string Positioner = "isolate z-50 outline-none";
    public const string Popup = "cn-menu-target cn-menu-translucent z-50 max-h-(--available-height) w-(--anchor-width) min-w-32 origin-(--transform-origin) overflow-x-hidden overflow-y-auto rounded-lg bg-popover p-1 text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100 outline-none data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-closed:animate-out data-closed:overflow-hidden data-closed:fade-out-0 data-closed:zoom-out-95";
    public const string Label = "px-1.5 py-1 text-xs font-medium text-muted-foreground data-inset:pl-7";
    public const string Item = "group/dropdown-menu-item relative flex cursor-default items-center gap-1.5 rounded-md px-1.5 py-1 text-sm outline-hidden select-none focus:bg-accent focus:text-accent-foreground data-inset:pl-7 data-[variant=destructive]:text-destructive data-[variant=destructive]:focus:bg-destructive/10 data-disabled:pointer-events-none data-disabled:opacity-50";
    public const string SubTrigger = "flex cursor-default items-center gap-1.5 rounded-md px-1.5 py-1 text-sm outline-hidden select-none focus:bg-accent focus:text-accent-foreground data-inset:pl-7 data-popup-open:bg-accent";
    public const string SubIcon = "cn-rtl-flip ml-auto";
    public const string SubPopup = "cn-menu-target cn-menu-translucent w-auto min-w-[96px] rounded-lg bg-popover p-1 text-popover-foreground shadow-lg ring-1 ring-foreground/10 duration-100 data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95";
    public const string ChoiceItem = "relative flex cursor-default items-center gap-1.5 rounded-md py-1 pr-8 pl-1.5 text-sm outline-hidden select-none focus:bg-accent focus:text-accent-foreground data-inset:pl-7 data-disabled:pointer-events-none data-disabled:opacity-50";
    public const string Indicator = "pointer-events-none absolute right-2 flex items-center justify-center";
    public const string Separator = "-mx-1 my-1 h-px bg-border";
    public const string Shortcut = "ml-auto text-xs tracking-widest text-muted-foreground group-focus/dropdown-menu-item:text-accent-foreground";
}
