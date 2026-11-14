using Interop.JS.Shadcn.BaseUI;
using Interop.JS.LucideIcons;
using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public sealed class DialogProps
{
    public bool? Open { get; set; }
    public bool? DefaultOpen { get; set; }
}

public sealed class DialogActionProps
{
    public string? ClassName { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class DialogContentProps
{
    public string? ClassName { get; set; }
    public bool ShowCloseButton { get; set; } = true;
}

public sealed class DialogFooterProps
{
    public string? ClassName { get; set; }
    public bool ShowCloseButton { get; set; }
}

public sealed class DialogPartProps
{
    public string? ClassName { get; set; }
}

public static class DialogPrimitive
{
    public static ReactProps CreateRootProps(DialogProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetString("data-slot", "dialog");
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

    public static ReactProps CreateActionProps(string slot, DialogActionProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = props.ClassName;
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", slot);
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreatePartProps(string slot, string? baseClasses, DialogPartProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(baseClasses, props.ClassName);
        importedProps.SetString("data-slot", slot);
        return importedProps;
    }

    public static ReactProps CreateOverlayProps() =>
        CreatePartProps("dialog-overlay", DialogStyles.Overlay, new DialogPartProps());

    public static ReactProps CreateContentProps(DialogContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(DialogStyles.Content, props.ClassName);
        importedProps.SetString("data-slot", "dialog-content");
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
            DialogStyles.Close);
        closeProps.SetString("data-slot", "dialog-close");
        closeProps.SetString("aria-label", "Close");
        return BaseUI.Dialog.Close.Create(
            closeProps,
            LucideIcons.XIcon.Create(icon => icon.AriaHidden = true));
    }

    public static ReactNode CreateFooterClose(bool show)
    {
        if (!show)
        {
            return React.CreateFragment();
        }
        var closeProps = ReactProps.Create();
        closeProps.ClassName = ClassNames.Merge(
            ButtonStyles.Base,
            ButtonStyles.Variant(ButtonVariant.Outline),
            ButtonStyles.Size(ButtonSize.Default));
        closeProps.SetString("data-slot", "dialog-close");
        return BaseUI.Dialog.Close.Create(
            closeProps,
            React.Text("Close"));
    }
}

public static class DialogStyles
{
    public const string Overlay = "fixed inset-0 isolate z-50 bg-black/10 duration-100 supports-backdrop-filter:backdrop-blur-xs data-open:animate-in data-open:fade-in-0 data-closed:animate-out data-closed:fade-out-0";
    public const string Content = "fixed top-1/2 left-1/2 z-50 grid w-full max-w-[calc(100%-2rem)] -translate-x-1/2 -translate-y-1/2 gap-4 rounded-xl bg-popover p-4 text-sm text-popover-foreground ring-1 ring-foreground/10 duration-100 outline-none sm:max-w-sm data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95";
    public const string Close = "absolute top-2 right-2";
    public const string Header = "flex flex-col gap-2";
    public const string Footer = "-mx-4 -mb-4 flex flex-col-reverse gap-2 rounded-b-xl border-t bg-muted/50 p-4 sm:flex-row sm:justify-end";
    public const string Title = "cn-font-heading text-base leading-none font-medium";
    public const string Description = "text-sm text-muted-foreground *:[a]:underline *:[a]:underline-offset-3 *:[a]:hover:text-foreground";
}
