using FrontEndCSharp.RuntimeOverrides;
using Interop.JS.Browser;
using System;
using System.Runtime.CompilerServices;

namespace Interop.JS.BrowserRuntimeOverrides;

[RuntimeOverrideType(typeof(Window))]
internal static class WindowOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern int get_InnerWidth();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern int get_InnerHeight();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern double get_DevicePixelRatio();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern int SetTimeout(Action callback, int milliseconds);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void ClearTimeout(int handle);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern int SetInterval(Action callback, int milliseconds);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void ClearInterval(int handle);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void QueueMicrotask(Action callback);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern int RequestAnimationFrame(Action<double> callback);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void CancelAnimationFrame(int handle);
}
