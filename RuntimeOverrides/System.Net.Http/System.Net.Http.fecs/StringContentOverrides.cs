using FrontEndCSharp.RuntimeOverrides;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(StringContent))]
internal static class StringContentOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern StringContent Create(string content);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern StringContent Create(string content, Encoding? encoding);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern StringContent Create(
        string content,
        Encoding? encoding,
        string? mediaType);
}
