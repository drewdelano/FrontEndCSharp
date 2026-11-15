using FrontEndCSharp.RuntimeOverrides;
using Interop.JS.Browser;
using System.Runtime.CompilerServices;

namespace Interop.JS.BrowserRuntimeOverrides;

[RuntimeOverrideType(typeof(Document))]
internal static class DocumentOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Element get_Body();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Element get_Head();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Element get_DocumentElement();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Element? get_ActiveElement();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern string get_ReadyState();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern string get_Title();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_Title(string value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Element? GetElementById(string id);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Element? QuerySelector(string selectors);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Element[] QuerySelectorAll(string selectors);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Element CreateElement(string tagName);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Node CreateTextNode(string data);
}
