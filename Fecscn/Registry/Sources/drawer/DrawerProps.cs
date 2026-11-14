using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum DrawerDirection
{
    Down,
    Up,
    Left,
    Right
}

public sealed class DrawerProps
{
    public bool? Open { get; set; }
    public bool? DefaultOpen { get; set; }
    public DrawerDirection Direction { get; set; }
}

public sealed class DrawerActionProps
{
    public string? ClassName { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class DrawerContentProps
{
    public string? ClassName { get; set; }
    public DrawerDirection Direction { get; set; }
    public bool ShowSwipeHandle { get; set; }
}

public sealed class DrawerPartProps
{
    public string? ClassName { get; set; }
}

public static class DrawerPrimitive
{
    public static ReactProps CreateRootProps(DrawerProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetString("data-slot", "drawer");
        importedProps.SetBoolean("modal", true);
        importedProps.SetString("swipeDirection", Direction(props.Direction));
        if (props.Open.HasValue) importedProps.SetBoolean("open", props.Open.Value);
        if (props.DefaultOpen.HasValue) importedProps.SetBoolean("defaultOpen", props.DefaultOpen.Value);
        return importedProps;
    }

    public static ReactProps CreateActionProps(string slot, DrawerActionProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = props.ClassName;
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", slot);
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreatePartProps(string slot, string? baseClasses, DrawerPartProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(baseClasses, props.ClassName);
        importedProps.SetString("data-slot", slot);
        return importedProps;
    }

    public static ReactProps CreateOverlayProps() =>
        CreatePartProps("drawer-overlay", DrawerStyles.Overlay, new DrawerPartProps());

    public static ReactProps CreateViewportProps()
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = DrawerStyles.Viewport;
        importedProps.SetString("data-slot", "drawer-viewport");
        importedProps.SetBoolean("data-modal", true);
        return importedProps;
    }

    public static ReactProps CreatePopupProps(DrawerContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(DrawerStyles.Popup, props.ClassName);
        importedProps.SetString("data-slot", "drawer-popup");
        importedProps.SetString("data-swipe-axis", Axis(props.Direction));
        return importedProps;
    }

    public static ReactProps CreateInnerContentProps()
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = DrawerStyles.Content;
        importedProps.SetString("data-slot", "drawer-content");
        return importedProps;
    }

    public static ReactNode CreateSwipeHandle(bool show)
    {
        if (!show) return React.CreateFragment();
        var props = ReactProps.Create();
        props.ClassName = DrawerStyles.SwipeHandle;
        props.SetString("data-slot", "drawer-swipe-handle");
        props.SetBoolean("aria-hidden", true);
        return React.CreateElement("div", props);
    }

    private static string Direction(DrawerDirection value)
    {
        if (value == DrawerDirection.Up) return "up";
        if (value == DrawerDirection.Left) return "left";
        if (value == DrawerDirection.Right) return "right";
        return "down";
    }

    private static string Axis(DrawerDirection value) =>
        value == DrawerDirection.Left || value == DrawerDirection.Right ? "x" : "y";
}

public static class DrawerStyles
{
    public const string Overlay = "fixed inset-0 z-50 min-h-dvh bg-black/10 opacity-[max(var(--drawer-overlay-min-opacity,0),calc(1-var(--drawer-swipe-progress)))] transition-opacity duration-450 ease-[cubic-bezier(0.32,0.72,0,1)] select-none data-ending-style:pointer-events-none data-ending-style:opacity-0 data-ending-style:duration-[calc(var(--drawer-swipe-strength)*400ms)] data-starting-style:opacity-0 data-swiping:duration-0 supports-backdrop-filter:backdrop-blur-xs supports-[-webkit-touch-callout:none]:absolute";
    public const string Viewport = "pointer-events-none fixed inset-0 z-50 select-none data-[modal=true]:pointer-events-auto";
    public const string Popup = "group/drawer-popup pointer-events-auto fixed z-50 m-(--drawer-inset,0px) flex h-(--drawer-content-height) max-h-(--drawer-content-max-height,none) min-h-0 w-(--drawer-content-width,auto) transform-[translate3d(var(--translate-x,0px),var(--translate-y,0px),0)_scale(var(--stack-scale))] flex-col bg-popover text-sm text-popover-foreground transition-[transform,height,opacity,filter] duration-450 ease-[cubic-bezier(0.22,1,0.36,1)] will-change-transform outline-none select-none [interpolate-size:allow-keywords] data-[swipe-direction=down]:rounded-t-xl data-[swipe-direction=down]:border-t data-[swipe-direction=left]:rounded-r-xl data-[swipe-direction=left]:border-r data-[swipe-direction=right]:rounded-l-xl data-[swipe-direction=right]:border-l data-[swipe-direction=up]:rounded-b-xl data-[swipe-direction=up]:border-b data-nested-drawer-open:overflow-hidden data-nested-drawer-open:brightness-95 [--drawer-content-height:var(--drawer-height,auto)] data-[swipe-axis=x]:[--drawer-content-width:75%] data-[swipe-axis=y]:[--drawer-content-max-height:calc(100dvh-6rem)] data-[swipe-axis=x]:sm:[--drawer-content-width:24rem] data-ending-style:transform-(--closed-transform) data-ending-style:opacity-[0.9999] data-starting-style:transform-(--closed-transform) data-swiping:duration-0 data-[swipe-axis=y]:inset-x-0 data-[swipe-axis=x]:inset-y-0 data-[swipe-axis=x]:flex-row data-[swipe-direction=down]:bottom-0 data-[swipe-direction=down]:origin-bottom data-[swipe-direction=up]:top-0 data-[swipe-direction=up]:origin-top data-[swipe-direction=left]:left-0 data-[swipe-direction=left]:origin-left data-[swipe-direction=right]:right-0 data-[swipe-direction=right]:origin-right";
    public const string SwipeHandle = "relative z-10 flex h-3 w-full shrink-0 cursor-grab items-end justify-center transition-opacity duration-200 after:block after:h-1 after:w-24 after:shrink-0 after:rounded-full after:bg-muted active:cursor-grabbing";
    public const string Content = "flex min-h-0 flex-1 flex-col overflow-hidden overscroll-contain rounded-[inherit] transition-opacity duration-300 ease-[cubic-bezier(0.45,1.005,0,1.005)] select-text group-data-nested-drawer-open/drawer-popup:opacity-0 group-data-nested-drawer-swiping/drawer-popup:opacity-100 group-data-swiping/drawer-popup:select-none";
    public const string Header = "flex shrink-0 flex-col gap-0.5 p-4 pb-0 group-data-[swipe-axis=y]/drawer-popup:text-center md:gap-0.5 md:text-left";
    public const string Footer = "mt-auto flex shrink-0 flex-col gap-2 p-4 pt-0";
    public const string Title = "cn-font-heading text-base font-medium text-foreground";
    public const string Description = "text-sm text-balance text-muted-foreground";
}
