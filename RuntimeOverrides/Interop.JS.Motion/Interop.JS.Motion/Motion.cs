using Interop.JS.React;
using System.Runtime.CompilerServices;

namespace Interop.JS.Motion;

public sealed class MotionStyle
{
    private MotionStyle()
    {
    }

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern MotionStyle Create();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern MotionStyle SetNumber(string name, double value);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern MotionStyle SetString(string name, string value);
}

public sealed class MotionTransition
{
    private MotionTransition()
    {
    }

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern MotionTransition Create();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern MotionTransition SetType(string type);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern MotionTransition SetDuration(double seconds);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern MotionTransition SetDelay(double seconds);
}

public class MotionProps : ReactProps
{
    protected MotionProps()
    {
    }

    public MotionStyle? Initial
    {
        get => GetReferenceProperty<MotionStyle>("initial");
        set => Properties["initial"] = value;
    }

    public MotionStyle? Animate
    {
        get => GetReferenceProperty<MotionStyle>("animate");
        set => Properties["animate"] = value;
    }

    public MotionStyle? WhileHover
    {
        get => GetReferenceProperty<MotionStyle>("whileHover");
        set => Properties["whileHover"] = value;
    }

    public MotionTransition? Transition
    {
        get => GetReferenceProperty<MotionTransition>("transition");
        set => Properties["transition"] = value;
    }

    public bool Layout
    {
        get => GetValueProperty<bool>("layout");
        set => Properties["layout"] = value;
    }

    public static new MotionProps Create() => new MotionProps();

    public MotionProps SetInitial(MotionStyle style)
    {
        Initial = style;
        return this;
    }

    public MotionProps SetAnimate(MotionStyle style)
    {
        Animate = style;
        return this;
    }

    public MotionProps SetWhileHover(MotionStyle style)
    {
        WhileHover = style;
        return this;
    }

    public MotionProps SetTransition(MotionTransition transition)
    {
        Transition = transition;
        return this;
    }

    public MotionProps SetLayout(bool enabled)
    {
        Layout = enabled;
        return this;
    }
}

public static class Motion
{
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern ReactElement Element(
        string elementName,
        MotionProps? props,
        params ReactNode[] children);
}
