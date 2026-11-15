using FrontEndCSharp.RuntimeOverrides;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(FormUrlEncodedContent))]
internal static class FormUrlEncodedContentOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern FormUrlEncodedContent Create(
        IEnumerable<KeyValuePair<string, string>> nameValueCollection);
}
