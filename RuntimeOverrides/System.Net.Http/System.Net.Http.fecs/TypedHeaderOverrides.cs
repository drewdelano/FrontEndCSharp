using FrontEndCSharp.RuntimeOverrides;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(AuthenticationHeaderValue))]
internal static class AuthenticationHeaderValueOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern AuthenticationHeaderValue Create(string scheme, string? parameter);
}

[RuntimeOverrideType(typeof(MediaTypeWithQualityHeaderValue))]
internal static class MediaTypeWithQualityHeaderValueOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MediaTypeWithQualityHeaderValue Create(string mediaType);
}

[RuntimeOverrideType(typeof(MediaTypeHeaderValue))]
internal static class MediaTypeHeaderValueOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern string? get_MediaType(MediaTypeHeaderValue @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern string? get_CharSet(MediaTypeHeaderValue @this);
}

[RuntimeOverrideType(typeof(HttpContentHeaders))]
internal static class HttpContentHeadersOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MediaTypeHeaderValue? get_ContentType(HttpContentHeaders @this);
}

[RuntimeOverrideType(typeof(HttpRequestHeaders))]
internal static class HttpRequestHeadersOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern HttpHeaderValueCollection<MediaTypeWithQualityHeaderValue> get_Accept(
        HttpRequestHeaders @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_Authorization(
        HttpRequestHeaders @this,
        AuthenticationHeaderValue? value);
}

[RuntimeOverrideType(typeof(HttpHeaderValueCollection<>))]
internal static class HttpHeaderValueCollectionOverrides<T>
    where T : class
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static void Add(HttpHeaderValueCollection<T> @this, T item) => throw null!;
}
