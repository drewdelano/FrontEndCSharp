using FrontEndCSharp.RuntimeOverrides;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(HttpContent))]
internal static class HttpContentOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern HttpContentHeaders get_Headers(HttpContent @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Task<string> ReadAsStringAsync(HttpContent @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Task<string> ReadAsStringAsync(
        HttpContent @this,
        CancellationToken cancellationToken);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Task<byte[]> ReadAsByteArrayAsync(HttpContent @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Task<byte[]> ReadAsByteArrayAsync(
        HttpContent @this,
        CancellationToken cancellationToken);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<Stream> ReadAsStreamAsync(HttpContent @this)
    {
        Stream stream = @this == null
            ? new System.Net.Http.BrowserHttpReadStream()
            : new MemoryStream(Array.Empty<byte>(), writable: false);
        _ = stream.ReadAsync(Array.Empty<byte>(), 0, 0);
        _ = stream.ReadAsync(Array.Empty<byte>(), 0, 0, CancellationToken.None);
        _ = stream.Read(Array.Empty<byte>(), 0, 0);
        stream.Dispose();
        return Task.FromResult(stream);
    }

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<Stream> ReadAsStreamAsync(
        HttpContent @this,
        CancellationToken cancellationToken)
    {
        Stream stream = @this == null
            ? new System.Net.Http.BrowserHttpReadStream()
            : new MemoryStream(Array.Empty<byte>(), writable: false);
        _ = stream.ReadAsync(Array.Empty<byte>(), 0, 0);
        _ = stream.ReadAsync(Array.Empty<byte>(), 0, 0, cancellationToken);
        _ = stream.Read(Array.Empty<byte>(), 0, 0);
        stream.Dispose();
        return Task.FromResult(stream);
    }
}
