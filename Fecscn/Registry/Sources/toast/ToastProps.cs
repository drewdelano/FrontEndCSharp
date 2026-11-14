using Interop.JS.Shadcn.BaseUI;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum ToastType
{
    Default,
    Success,
    Info,
    Warning,
    Error,
    Loading
}

public sealed class ToastHostProps
{
    public string? ClassName { get; set; }
    public double Timeout { get; set; } = 5000;
}

public static class ToastNotifications
{
    public static void Show(string title, string description, ToastType type = ToastType.Default) =>
        BaseUI.Toast.Add(title, description, Type(type));

    private static string Type(ToastType value)
    {
        if (value == ToastType.Success) return "success";
        if (value == ToastType.Info) return "info";
        if (value == ToastType.Warning) return "warning";
        if (value == ToastType.Error) return "error";
        if (value == ToastType.Loading) return "loading";
        return "default";
    }
}

public static class ToastPrimitive
{
    public static ReactProps CreateHostProps(ToastHostProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetNumber("timeout", props.Timeout);
        importedProps.SetString("viewportClassName", ClassNames.Merge(ToastStyles.Viewport, props.ClassName));
        importedProps.SetString("rootClassName", ToastStyles.Root);
        importedProps.SetString("contentClassName", ToastStyles.Content);
        importedProps.SetString("iconClassName", ToastStyles.Icon);
        importedProps.SetString("textClassName", ToastStyles.Text);
        importedProps.SetString("titleClassName", ToastStyles.Title);
        importedProps.SetString("descriptionClassName", ToastStyles.Description);
        importedProps.SetString("closeClassName", ClassNames.Merge(
            ButtonStyles.Base,
            ButtonStyles.Variant(ButtonVariant.Ghost),
            ButtonStyles.Size(ButtonSize.IconSmall),
            ToastStyles.Close));
        return importedProps;
    }
}

public static class ToastStyles
{
    public const string Viewport = "pointer-events-none fixed inset-x-4 bottom-4 z-50 mx-auto w-auto max-w-sm outline-none sm:right-4 sm:left-auto sm:mx-0 sm:w-full";
    public const string Root = "group/toast pointer-events-auto absolute right-0 bottom-0 z-[calc(1000-var(--toast-index))] w-full origin-bottom rounded-2xl border bg-popover text-popover-foreground shadow-lg will-change-transform outline-none select-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 [--gap:0.75rem] [--height:var(--toast-frontmost-height,var(--toast-height))] [--offset-y:calc(var(--toast-offset-y)*-1+calc(var(--toast-index)*var(--gap)*-1)+var(--toast-swipe-movement-y))] [--peek:0.75rem] [--scale:calc(max(0,1-(var(--toast-index)*0.1)))] [--shrink:calc(1-var(--scale))] h-(--height) [transform:translateX(var(--toast-swipe-movement-x))_translateY(calc(var(--toast-swipe-movement-y)-(var(--toast-index)*var(--peek))-(var(--shrink)*var(--height))))_scale(var(--scale))] [transition:transform_500ms_cubic-bezier(0.22,1,0.36,1),opacity_500ms,height_150ms] after:absolute after:top-full after:left-0 after:h-[calc(var(--gap)+1px)] after:w-full after:content-[''] data-expanded:h-(--toast-height) data-expanded:[transform:translateX(var(--toast-swipe-movement-x))_translateY(var(--offset-y))] data-limited:opacity-0 data-starting-style:[transform:translateY(150%)] [&[data-ending-style]:not([data-limited]):not([data-swipe-direction])]:[transform:translateY(150%)] data-ending-style:data-[swipe-direction=down]:[transform:translateY(calc(var(--toast-swipe-movement-y)+150%))] data-ending-style:data-[swipe-direction=left]:[transform:translateX(calc(var(--toast-swipe-movement-x)-150%))_translateY(var(--offset-y))] data-ending-style:data-[swipe-direction=right]:[transform:translateX(calc(var(--toast-swipe-movement-x)+150%))_translateY(var(--offset-y))] data-ending-style:data-[swipe-direction=up]:[transform:translateY(calc(var(--toast-swipe-movement-y)-150%))] data-expanded:data-ending-style:data-[swipe-direction=down]:[transform:translateY(calc(var(--toast-swipe-movement-y)+150%))] data-expanded:data-ending-style:data-[swipe-direction=left]:[transform:translateX(calc(var(--toast-swipe-movement-x)-150%))_translateY(var(--offset-y))] data-expanded:data-ending-style:data-[swipe-direction=right]:[transform:translateX(calc(var(--toast-swipe-movement-x)+150%))_translateY(var(--offset-y))] data-expanded:data-ending-style:data-[swipe-direction=up]:[transform:translateY(calc(var(--toast-swipe-movement-y)-150%))]";
    public const string Content = "flex h-full items-center gap-3 overflow-hidden p-4 transition-opacity duration-250 ease-[cubic-bezier(0.22,1,0.36,1)] data-behind:opacity-0 data-expanded:opacity-100";
    public const string Icon = "shrink-0 [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4";
    public const string Text = "flex min-w-0 flex-1 flex-col gap-1";
    public const string Title = "text-sm font-medium";
    public const string Description = "text-sm text-muted-foreground";
    public const string Close = "relative shrink-0 text-muted-foreground after:absolute after:-inset-2 after:content-[''] hover:text-foreground";
}
