using System;
using Interop.JS.React;
using ReactApi = Interop.JS.React.React;

namespace Interop.JS.LucideIcons;

public sealed class LucideIconProps : ReactProps
{
    public LucideIconProps()
    {
    }

    public bool AriaHidden
    {
        get => GetValueProperty<bool>("aria-hidden");
        set => Properties["aria-hidden"] = value;
    }

    public string? Color
    {
        get => GetReferenceProperty<string>("color");
        set => Properties["color"] = value;
    }

    public double Size
    {
        get => GetValueProperty<double>("size");
        set => Properties["size"] = value;
    }

    public double StrokeWidth
    {
        get => GetValueProperty<double>("strokeWidth");
        set => Properties["strokeWidth"] = value;
    }

    public bool AbsoluteStrokeWidth
    {
        get => GetValueProperty<bool>("absoluteStrokeWidth");
        set => Properties["absoluteStrokeWidth"] = value;
    }

    public string? DataSlot
    {
        get => GetReferenceProperty<string>("data-slot");
        set => Properties["data-slot"] = value;
    }
}

public static partial class LucideIcons
{
    private static ReactElement CreateElement(
        ImportedReactComponent component,
        Action<LucideIconProps> configure,
        ReactNode[] children)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var props = new LucideIconProps();
        configure(props);
        return ReactApi.CreateElement(component, props, children);
    }

    private static ReactElement CreateElement(
        ImportedReactComponent component,
        LucideIconProps? props,
        ReactNode[] children) =>
        ReactApi.CreateElement(component, props, children);
}
