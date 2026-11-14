using System.Runtime.CompilerServices;

namespace Interop.JS.Browser;

public sealed class DomEvent
{
    private DomEvent()
    {
    }

    public extern string Type
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public extern bool Bubbles
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public extern bool Cancelable
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public extern bool DefaultPrevented
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public extern Element? CurrentTarget
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void PreventDefault();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void StopPropagation();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void StopImmediatePropagation();
}
