using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Runtime.CompilerServices;

[assembly: RuntimeOverrideAssembly("System.Console")]

namespace FrontEndCSharp.SystemConsoleRuntimeOverrides;

[RuntimeOverrideType(typeof(Console))]
internal static class ConsoleOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Write(string value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine(bool value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine(char value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine(double value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine(int value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine(long value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine(object value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine(float value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine(string value);
}
