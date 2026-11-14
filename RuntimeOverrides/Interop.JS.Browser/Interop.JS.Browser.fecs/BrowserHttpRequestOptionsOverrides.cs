using FrontEndCSharp.RuntimeOverrides;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace Interop.JS.Browser.RuntimeOverrides;

[RuntimeOverrideType(typeof(BrowserHttpRequestOptions))]
internal static class BrowserHttpRequestOptionsOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void SetBrowserCredentials(
        HttpRequestMessage request,
        BrowserRequestCredentials credentials);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void SetBrowserCache(
        HttpRequestMessage request,
        BrowserRequestCache cache);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void SetBrowserMode(
        HttpRequestMessage request,
        BrowserRequestMode mode);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void SetBrowserRedirect(
        HttpRequestMessage request,
        BrowserRedirectMode redirect);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void SetBrowserIntegrity(
        HttpRequestMessage request,
        string integrity);
}
