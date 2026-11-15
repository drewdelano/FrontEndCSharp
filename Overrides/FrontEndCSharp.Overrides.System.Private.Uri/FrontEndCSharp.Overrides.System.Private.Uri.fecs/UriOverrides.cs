using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Runtime.CompilerServices;

namespace System.Private.UriRuntimeOverrides;

[RuntimeOverrideType(typeof(Uri))]
internal static class UriOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Uri Create(string uriString);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern bool get_IsAbsoluteUri(Uri @this);
}
