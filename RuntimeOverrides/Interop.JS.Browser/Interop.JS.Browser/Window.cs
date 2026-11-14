using System;
using System.Runtime.CompilerServices;

namespace Interop.JS.Browser;

public static class Window
{
    public static extern int InnerWidth
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public static extern int InnerHeight
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public static extern double DevicePixelRatio
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern int SetTimeout(Action callback, int milliseconds);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern void ClearTimeout(int handle);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern int SetInterval(Action callback, int milliseconds);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern void ClearInterval(int handle);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern void QueueMicrotask(Action callback);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern int RequestAnimationFrame(Action<double> callback);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern void CancelAnimationFrame(int handle);
}
