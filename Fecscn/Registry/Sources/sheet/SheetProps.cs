using Interop.JS.Shadcn.BaseUI;
using Interop.JS.LucideIcons;
using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum SheetSide
{
    Right,
    Left,
    Top,
    Bottom
}

public sealed class SheetProps
{
    public bool? Open { get; set; }
    public bool? DefaultOpen { get; set; }
}

public sealed class SheetActionProps
{
    public string? ClassName { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class SheetContentProps
{
    public string? ClassName { get; set; }
    public SheetSide Side { get; set; }
    public bool ShowCloseButton { get; set; } = true;
}

public sealed class SheetPartProps
{
    public string? ClassName { get; set; }
}

public static class SheetPrimitive
{
    public static ReactProps CreateRootProps(SheetProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetString("data-slot", "sheet");
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

    public static ReactProps CreateActionProps(string slot, SheetActionProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = props.ClassName;
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", slot);
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateOverlayProps()
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = SheetStyles.Overlay;
        importedProps.SetString("data-slot", "sheet-overlay");
        return importedProps;
    }

    public static ReactProps CreateContentProps(SheetContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(SheetStyles.Content, props.ClassName);
        importedProps.SetString("data-slot", "sheet-content");
        importedProps.SetString("data-side", Side(props.Side));
        return importedProps;
    }

    public static ReactProps CreatePartProps(string slot, string baseClasses, SheetPartProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(baseClasses, props.ClassName);
        importedProps.SetString("data-slot", slot);
        return importedProps;
    }

    public static ReactNode CreateContentClose(bool show)
    {
        if (!show)
        {
            return React.CreateFragment();
        }
        var closeProps = ReactProps.Create();
        closeProps.ClassName = ClassNames.Merge(
            ButtonStyles.Base,
            ButtonStyles.Variant(ButtonVariant.Ghost),
            ButtonStyles.Size(ButtonSize.IconSmall),
            SheetStyles.Close);
        closeProps.SetString("data-slot", "sheet-close");
        closeProps.SetString("aria-label", "Close");
        return BaseUI.Dialog.Close.Create(
            closeProps,
            LucideIcons.XIcon.Create(icon => icon.AriaHidden = true));
    }

    private static string Side(SheetSide value)
    {
        if (value == SheetSide.Left) return "left";
        if (value == SheetSide.Top) return "top";
        if (value == SheetSide.Bottom) return "bottom";
        return "right";
    }
}

public static class SheetStyles
{
    public const string Overlay = "fixed inset-0 z-50 bg-black/10 transition-opacity duration-150 data-ending-style:opacity-0 data-starting-style:opacity-0 supports-backdrop-filter:backdrop-blur-xs";
    public const string Content = "fixed z-50 flex flex-col gap-4 bg-popover bg-clip-padding text-sm text-popover-foreground shadow-lg transition duration-200 ease-in-out data-ending-style:opacity-0 data-starting-style:opacity-0 data-[side=bottom]:inset-x-0 data-[side=bottom]:bottom-0 data-[side=bottom]:h-auto data-[side=bottom]:border-t data-[side=bottom]:data-ending-style:translate-y-[2.5rem] data-[side=bottom]:data-starting-style:translate-y-[2.5rem] data-[side=left]:inset-y-0 data-[side=left]:left-0 data-[side=left]:h-full data-[side=left]:w-3/4 data-[side=left]:border-r data-[side=left]:data-ending-style:translate-x-[-2.5rem] data-[side=left]:data-starting-style:translate-x-[-2.5rem] data-[side=right]:inset-y-0 data-[side=right]:right-0 data-[side=right]:h-full data-[side=right]:w-3/4 data-[side=right]:border-l data-[side=right]:data-ending-style:translate-x-[2.5rem] data-[side=right]:data-starting-style:translate-x-[2.5rem] data-[side=top]:inset-x-0 data-[side=top]:top-0 data-[side=top]:h-auto data-[side=top]:border-b data-[side=top]:data-ending-style:translate-y-[-2.5rem] data-[side=top]:data-starting-style:translate-y-[-2.5rem] data-[side=left]:sm:max-w-sm data-[side=right]:sm:max-w-sm";
    public const string Close = "absolute top-3 right-3";
    public const string Header = "flex flex-col gap-0.5 p-4";
    public const string Footer = "mt-auto flex flex-col gap-2 p-4";
    public const string Title = "cn-font-heading text-base font-medium text-foreground";
    public const string Description = "text-sm text-muted-foreground";
}
