using FrontEndCSharp.RuntimeOverrides;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(ByteArrayContent))]
internal static class ByteArrayContentOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ByteArrayContent Create(byte[] content);
}
