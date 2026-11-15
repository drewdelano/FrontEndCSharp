using FrontEndCSharp.RuntimeOverrides;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(HttpHeaders))]
internal static class HttpHeadersOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Add(HttpHeaders @this, string name, string value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern bool Contains(HttpHeaders @this, string name);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static IEnumerator<KeyValuePair<string, IEnumerable<string>>> GetEnumerator(
        HttpHeaders @this)
    {
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            [string.Empty] = System.Array.Empty<string>(),
        };
        var enumerator = headers.GetEnumerator();
        _ = enumerator.MoveNext();
        _ = enumerator.Current;
        enumerator.Dispose();
        return headers.GetEnumerator();
    }

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern IEnumerable<string> GetValues(HttpHeaders @this, string name);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static bool TryGetValues(
        HttpHeaders @this,
        string name,
        out IEnumerable<string>? values)
    {
        values = System.Array.Empty<string>();
        if (@this == null)
        {
            values = null;
        }
        return false;
    }
}
