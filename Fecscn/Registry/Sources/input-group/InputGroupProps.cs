using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum InputGroupAddonAlign
{
    InlineStart,
    InlineEnd,
    BlockStart,
    BlockEnd
}

public enum InputGroupButtonSize
{
    ExtraSmall,
    Small,
    IconExtraSmall,
    IconSmall
}

public sealed class InputGroupProps
{
    public string? ClassName { get; set; }
}

public sealed class InputGroupAddonProps
{
    public string? ClassName { get; set; }
    public InputGroupAddonAlign Align { get; set; }
}

public sealed class InputGroupButtonProps
{
    public string? ClassName { get; set; }
    public ButtonVariant Variant { get; set; } = ButtonVariant.Ghost;
    public InputGroupButtonSize Size { get; set; }
    public string Type { get; set; } = "button";
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class InputGroupTextProps
{
    public string? ClassName { get; set; }
}

public sealed class InputGroupInputProps
{
    public string? Id { get; set; }
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public string? Name { get; set; }
    public string? Placeholder { get; set; }
    public string Type { get; set; } = "text";
    public string? Value { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnChange { get; set; }
    public Action<DomEvent>? OnInput { get; set; }
}

public sealed class InputGroupTextareaProps
{
    public string? Id { get; set; }
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public string? Placeholder { get; set; }
    public string? Value { get; set; }
    public bool Disabled { get; set; }
    public bool ReadOnly { get; set; }
    public bool Required { get; set; }
    public Action<DomEvent>? OnChange { get; set; }
    public Action<DomEvent>? OnInput { get; set; }
}

public static class InputGroupPrimitive
{
    public static ReactProps CreateButtonProps(InputGroupButtonProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.Type = props.Type;
        importedProps.ClassName = ClassNames.Merge(
            ButtonStyles.Base,
            ButtonStyles.Variant(props.Variant),
            InputGroupStyles.Button,
            InputGroupStyles.ButtonSize(props.Size),
            props.ClassName);
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", "input-group-button");
        importedProps.SetString("data-size", InputGroupStyles.ButtonSizeName(props.Size));
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateInputProps(InputGroupInputProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.Id = props.Id;
        importedProps.AriaLabel = props.AriaLabel;
        importedProps.Type = props.Type;
        importedProps.ClassName = ClassNames.Merge(InputGroupStyles.InputBase, InputGroupStyles.Input, props.ClassName);
        importedProps.OnChange = props.OnChange;
        importedProps.OnInput = props.OnInput;
        importedProps.SetString("data-slot", "input-group-control");
        importedProps.SetString("name", props.Name);
        importedProps.SetString("placeholder", props.Placeholder);
        if (props.Value is not null)
        {
            importedProps.SetString("value", props.Value);
        }
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }
}

public static class InputGroupStyles
{
    public const string InputBase = "h-8 w-full min-w-0 rounded-lg border border-input bg-transparent px-2.5 py-1 text-base transition-colors outline-none file:inline-flex file:h-6 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:pointer-events-none disabled:cursor-not-allowed disabled:bg-input/50 disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 md:text-sm dark:bg-input/30 dark:disabled:bg-input/80 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40";
    public const string Root = "group/input-group relative flex h-8 w-full min-w-0 items-center rounded-lg border border-input transition-colors outline-none in-data-[slot=combobox-content]:focus-within:border-inherit in-data-[slot=combobox-content]:focus-within:ring-0 has-disabled:bg-input/50 has-disabled:opacity-50 has-[[data-slot=input-group-control]:focus-visible]:border-ring has-[[data-slot=input-group-control]:focus-visible]:ring-3 has-[[data-slot=input-group-control]:focus-visible]:ring-ring/50 has-[[data-slot][aria-invalid=true]]:border-destructive has-[[data-slot][aria-invalid=true]]:ring-3 has-[[data-slot][aria-invalid=true]]:ring-destructive/20 has-[>[data-align=block-end]]:h-auto has-[>[data-align=block-end]]:flex-col has-[>[data-align=block-start]]:h-auto has-[>[data-align=block-start]]:flex-col has-[>textarea]:h-auto dark:bg-input/30 dark:has-disabled:bg-input/80 dark:has-[[data-slot][aria-invalid=true]]:ring-destructive/40 has-[>[data-align=block-end]]:[&>input]:pt-3 has-[>[data-align=block-start]]:[&>input]:pb-3 has-[>[data-align=inline-end]]:[&>input]:pr-1.5 has-[>[data-align=inline-start]]:[&>input]:pl-1.5";
    public const string Addon = "flex h-auto cursor-text items-center justify-center gap-2 py-1.5 text-sm font-medium text-muted-foreground select-none group-data-[disabled=true]/input-group:opacity-50 [&>kbd]:rounded-[calc(var(--radius)-5px)] [&>svg:not([class*='size-'])]:size-4";
    public const string Button = "flex items-center gap-2 text-sm shadow-none";
    public const string Text = "flex items-center gap-2 text-sm text-muted-foreground [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4";
    public const string Input = "flex-1 rounded-none border-0 bg-transparent shadow-none ring-0 focus-visible:ring-0 disabled:bg-transparent aria-invalid:ring-0 dark:bg-transparent dark:disabled:bg-transparent";
    public const string Textarea = "flex-1 resize-none rounded-none border-0 bg-transparent py-2 shadow-none ring-0 focus-visible:ring-0 disabled:bg-transparent aria-invalid:ring-0 dark:bg-transparent dark:disabled:bg-transparent";

    public static string AddonAlign(InputGroupAddonAlign value)
    {
        if (value == InputGroupAddonAlign.InlineEnd) return "order-last pr-2 has-[>button]:mr-[-0.3rem] has-[>kbd]:mr-[-0.15rem]";
        if (value == InputGroupAddonAlign.BlockStart) return "order-first w-full justify-start px-2.5 pt-2 group-has-[>input]/input-group:pt-2 [.border-b]:pb-2";
        if (value == InputGroupAddonAlign.BlockEnd) return "order-last w-full justify-start px-2.5 pb-2 group-has-[>input]/input-group:pb-2 [.border-t]:pt-2";
        return "order-first pl-2 has-[>button]:ml-[-0.3rem] has-[>kbd]:ml-[-0.15rem]";
    }

    public static string ButtonSize(InputGroupButtonSize value)
    {
        if (value == InputGroupButtonSize.Small) return string.Empty;
        if (value == InputGroupButtonSize.IconExtraSmall) return "size-6 rounded-[calc(var(--radius)-3px)] p-0 has-[>svg]:p-0";
        if (value == InputGroupButtonSize.IconSmall) return "size-8 p-0 has-[>svg]:p-0";
        return "h-6 gap-1 rounded-[calc(var(--radius)-3px)] px-1.5 [&>svg:not([class*='size-'])]:size-3.5";
    }

    public static string ButtonSizeName(InputGroupButtonSize value)
    {
        if (value == InputGroupButtonSize.Small) return "sm";
        if (value == InputGroupButtonSize.IconExtraSmall) return "icon-xs";
        if (value == InputGroupButtonSize.IconSmall) return "icon-sm";
        return "xs";
    }
}
