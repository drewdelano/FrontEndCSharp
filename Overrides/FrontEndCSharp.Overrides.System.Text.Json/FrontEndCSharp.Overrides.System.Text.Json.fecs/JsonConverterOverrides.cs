using FrontEndCSharp.RuntimeOverrides;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace System.Text.JsonRuntimeOverrides;

[RuntimeOverrideType(typeof(JsonConverter))]
internal static class JsonConverterOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern JsonConverter Create();
}
