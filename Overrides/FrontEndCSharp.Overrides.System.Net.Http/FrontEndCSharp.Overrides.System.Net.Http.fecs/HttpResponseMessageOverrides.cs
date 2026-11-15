using FrontEndCSharp.RuntimeOverrides;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(HttpResponseMessage))]
internal static class HttpResponseMessageOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Dispose(HttpResponseMessage @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern HttpContent get_Content(HttpResponseMessage @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern HttpResponseHeaders get_Headers(HttpResponseMessage @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern HttpResponseMessage Create(HttpStatusCode statusCode);
}
