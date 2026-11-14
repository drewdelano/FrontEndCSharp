using FrontEndCSharp.RuntimeOverrides;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(HttpMethod))]
internal static class HttpMethodOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern HttpMethod Create(string method, int http3Index);
}
