using Interop.JS.Shadcn.BaseUI;
using Interop.JS.LucideIcons;
using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum ComboboxSide
{
    Bottom,
    Top,
    Left,
    Right
}

public enum ComboboxAlign
{
    Start,
    Center,
    End
}

public sealed class ComboboxProps
{
    public string? Value { get; set; }
    public string? DefaultValue { get; set; }
    public string? Name { get; set; }
    public bool Disabled { get; set; }
    public bool? Open { get; set; }
    public bool? DefaultOpen { get; set; }
}

public sealed class ComboboxValueProps
{
    public string? ClassName { get; set; }
    public string? Placeholder { get; set; }
}

public sealed class ComboboxTriggerProps
{
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class ComboboxActionProps
{
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class ComboboxInputProps
{
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public string? Placeholder { get; set; }
    public string? Value { get; set; }
    public bool Disabled { get; set; }
    public bool ShowTrigger { get; set; } = true;
    public bool ShowClear { get; set; }
    public Action<DomEvent>? OnChange { get; set; }
    public Action<DomEvent>? OnInput { get; set; }
}

public sealed class ComboboxContentProps
{
    public string? ClassName { get; set; }
    public ComboboxSide Side { get; set; }
    public double SideOffset { get; set; } = 6;
    public ComboboxAlign Align { get; set; }
    public double AlignOffset { get; set; }
}

public sealed class ComboboxItemProps
{
    public string? ClassName { get; set; }
    public string? Value { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class ComboboxChipProps
{
    public string? ClassName { get; set; }
    public bool ShowRemove { get; set; } = true;
}

public sealed class ComboboxPartProps
{
    public string? ClassName { get; set; }
}

public static class ComboboxPrimitive
{
    public static ReactProps CreateRootProps(ComboboxProps props)
    {
        var importedProps = ReactProps.Create();
        if (props.Value is not null) importedProps.SetString("value", props.Value);
        if (props.DefaultValue is not null) importedProps.SetString("defaultValue", props.DefaultValue);
        if (props.Open.HasValue) importedProps.SetBoolean("open", props.Open.Value);
        if (props.DefaultOpen.HasValue) importedProps.SetBoolean("defaultOpen", props.DefaultOpen.Value);
        importedProps.SetString("name", props.Name);
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateValueProps(ComboboxValueProps props)
    {
        var importedProps = CreatePartProps("combobox-value", string.Empty, new ComboboxPartProps { ClassName = props.ClassName });
        importedProps.SetString("placeholder", props.Placeholder);
        return importedProps;
    }

    public static ReactProps CreateTriggerProps(ComboboxTriggerProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.AriaLabel = props.AriaLabel;
        importedProps.ClassName = ClassNames.Merge(ComboboxStyles.Trigger, props.ClassName);
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", "combobox-trigger");
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateClearProps(ComboboxActionProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.AriaLabel = props.AriaLabel;
        importedProps.ClassName = ClassNames.Merge(ComboboxStyles.Clear, props.ClassName);
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", "combobox-clear");
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateInputProps(ComboboxInputProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.AriaLabel = props.AriaLabel;
        importedProps.ClassName = ComboboxStyles.Input;
        importedProps.OnChange = props.OnChange;
        importedProps.OnInput = props.OnInput;
        importedProps.SetString("data-slot", "input-group-control");
        importedProps.SetString("placeholder", props.Placeholder);
        if (props.Value is not null) importedProps.SetString("value", props.Value);
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactNode CreateInputTrigger(ComboboxInputProps props)
    {
        if (!props.ShowTrigger) return React.CreateFragment();
        var triggerProps = ReactProps.Create();
        triggerProps.AriaLabel = "Toggle suggestions";
        triggerProps.ClassName = ComboboxStyles.InputTrigger;
        triggerProps.SetString("data-slot", "combobox-trigger");
        triggerProps.SetBoolean("disabled", props.Disabled);
        return BaseUI.Combobox.Trigger.Create(
            triggerProps,
            LucideIcons.ChevronDownIcon.Create(icon => icon.AriaHidden = true));
    }

    public static ReactNode CreateInputClear(ComboboxInputProps props)
    {
        if (!props.ShowClear) return React.CreateFragment();
        var clearProps = ReactProps.Create();
        clearProps.AriaLabel = "Clear selection";
        clearProps.ClassName = ComboboxStyles.Clear;
        clearProps.SetString("data-slot", "combobox-clear");
        clearProps.SetBoolean("disabled", props.Disabled);
        return BaseUI.Combobox.Clear.Create(
            clearProps,
            LucideIcons.XIcon.Create(icon => icon.AriaHidden = true));
    }

    public static ReactProps CreatePositionerProps(ComboboxContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ComboboxStyles.Positioner;
        importedProps.SetString("side", Side(props.Side));
        importedProps.SetNumber("sideOffset", props.SideOffset);
        importedProps.SetString("align", Align(props.Align));
        importedProps.SetNumber("alignOffset", props.AlignOffset);
        return importedProps;
    }

    public static ReactProps CreatePopupProps(ComboboxContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(ComboboxStyles.Popup, props.ClassName);
        importedProps.SetString("data-slot", "combobox-content");
        importedProps.SetBoolean("data-chips", false);
        return importedProps;
    }

    public static ReactProps CreateItemProps(ComboboxItemProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(ComboboxStyles.Item, props.ClassName);
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", "combobox-item");
        importedProps.SetString("value", props.Value);
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateItemIndicatorProps() =>
        CreatePartProps("combobox-item-indicator", ComboboxStyles.ItemIndicator, new ComboboxPartProps());

    public static ReactProps CreateChipProps(ComboboxChipProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(ComboboxStyles.Chip, props.ClassName);
        importedProps.SetString("data-slot", "combobox-chip");
        return importedProps;
    }

    public static ReactNode CreateChipRemove(bool show)
    {
        if (!show) return React.CreateFragment();
        var importedProps = ReactProps.Create();
        importedProps.AriaLabel = "Remove";
        importedProps.ClassName = ComboboxStyles.ChipRemove;
        importedProps.SetString("data-slot", "combobox-chip-remove");
        return BaseUI.Combobox.ChipRemove.Create(
            importedProps,
            LucideIcons.XIcon.Create(icon => icon.AriaHidden = true));
    }

    public static ReactProps CreateChipsInputProps(ComboboxInputProps props)
    {
        var importedProps = CreateInputProps(props);
        importedProps.ClassName = ClassNames.Merge(ComboboxStyles.ChipsInput, props.ClassName);
        importedProps.SetString("data-slot", "combobox-chip-input");
        return importedProps;
    }

    public static ReactProps CreatePartProps(string slot, string baseClasses, ComboboxPartProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(baseClasses, props.ClassName);
        importedProps.SetString("data-slot", slot);
        return importedProps;
    }

    private static string Side(ComboboxSide value)
    {
        if (value == ComboboxSide.Top) return "top";
        if (value == ComboboxSide.Left) return "left";
        if (value == ComboboxSide.Right) return "right";
        return "bottom";
    }

    private static string Align(ComboboxAlign value)
    {
        if (value == ComboboxAlign.Center) return "center";
        if (value == ComboboxAlign.End) return "end";
        return "start";
    }
}

public static class ComboboxStyles
{
    public const string Trigger = "[&_svg:not([class*='size-'])]:size-4";
    public const string TriggerIcon = "pointer-events-none size-4 text-muted-foreground";
    public const string Clear = "flex size-6 items-center justify-center rounded-[calc(var(--radius)-3px)] text-sm shadow-none opacity-70 hover:bg-muted hover:opacity-100";
    public const string Input = "flex-1 rounded-none border-0 bg-transparent px-2.5 shadow-none outline-none ring-0 focus-visible:ring-0 disabled:bg-transparent aria-invalid:ring-0 dark:bg-transparent dark:disabled:bg-transparent";
    public const string InputTrigger = "group-has-data-[slot=combobox-clear]/input-group:hidden flex size-6 items-center justify-center rounded-[calc(var(--radius)-3px)] data-pressed:bg-transparent";
    public const string Positioner = "isolate z-50";
    public const string Popup = "cn-menu-target cn-menu-translucent group/combobox-content relative max-h-(--available-height) w-(--anchor-width) max-w-(--available-width) min-w-[calc(var(--anchor-width)+--spacing(7))] origin-(--transform-origin) overflow-hidden rounded-lg bg-popover text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100 data-[chips=true]:min-w-(--anchor-width) data-[side=bottom]:slide-in-from-top-2 data-[side=inline-end]:slide-in-from-left-2 data-[side=inline-start]:slide-in-from-right-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 *:data-[slot=input-group]:m-1 *:data-[slot=input-group]:mb-0 *:data-[slot=input-group]:h-8 *:data-[slot=input-group]:border-input/30 *:data-[slot=input-group]:bg-input/30 *:data-[slot=input-group]:shadow-none data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95";
    public const string List = "no-scrollbar max-h-[min(calc(--spacing(72)---spacing(9)),calc(var(--available-height)---spacing(9)))] scroll-py-1 overflow-y-auto overscroll-contain p-1 data-empty:p-0";
    public const string Item = "relative flex w-full cursor-default items-center gap-2 rounded-md py-1 pr-8 pl-1.5 text-sm outline-hidden select-none data-highlighted:bg-accent data-highlighted:text-accent-foreground not-data-[variant=destructive]:data-highlighted:**:text-accent-foreground data-disabled:pointer-events-none data-disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4";
    public const string ItemIndicator = "pointer-events-none absolute right-2 flex size-4 items-center justify-center";
    public const string Label = "px-2 py-1.5 text-xs text-muted-foreground";
    public const string Empty = "hidden w-full justify-center py-2 text-center text-sm text-muted-foreground group-data-empty/combobox-content:flex";
    public const string Separator = "-mx-1 my-1 h-px bg-border";
    public const string Chips = "flex min-h-8 flex-wrap items-center gap-1 rounded-lg border border-input bg-transparent bg-clip-padding px-2.5 py-1 text-sm transition-colors focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50 has-aria-invalid:border-destructive has-aria-invalid:ring-3 has-aria-invalid:ring-destructive/20 has-data-[slot=combobox-chip]:px-1 dark:bg-input/30 dark:has-aria-invalid:border-destructive/50 dark:has-aria-invalid:ring-destructive/40";
    public const string Chip = "flex h-[calc(--spacing(5.25))] w-fit items-center justify-center gap-1 rounded-sm bg-muted px-1.5 text-xs font-medium whitespace-nowrap text-foreground has-disabled:pointer-events-none has-disabled:cursor-not-allowed has-disabled:opacity-50 has-data-[slot=combobox-chip-remove]:pr-0";
    public const string ChipRemove = "-ml-1 flex size-5 items-center justify-center rounded-sm opacity-50 hover:opacity-100";
    public const string ChipsInput = "min-w-16 flex-1 outline-none";
}
