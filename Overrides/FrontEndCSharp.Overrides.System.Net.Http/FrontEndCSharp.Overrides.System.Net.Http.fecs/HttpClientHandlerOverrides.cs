using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Net.Http;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Threading;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(HttpClientHandler))]
internal static class HttpClientHandlerOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern HttpClientHandler Create();

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static HttpResponseMessage Send(
        HttpClientHandler @this,
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw new PlatformNotSupportedException(
            "Synchronous HTTP is not supported by the browser Fetch transport.");

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Dispose(HttpClientHandler @this, bool disposing);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_UseProxy(HttpClientHandler @this, bool value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_Proxy(HttpClientHandler @this, IWebProxy? value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_ClientCertificateOptions(
        HttpClientHandler @this,
        ClientCertificateOption value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_CheckCertificateRevocationList(
        HttpClientHandler @this,
        bool value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_SslProtocols(HttpClientHandler @this, SslProtocols value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_AutomaticDecompression(
        HttpClientHandler @this,
        DecompressionMethods value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_MaxConnectionsPerServer(HttpClientHandler @this, int value);
}
