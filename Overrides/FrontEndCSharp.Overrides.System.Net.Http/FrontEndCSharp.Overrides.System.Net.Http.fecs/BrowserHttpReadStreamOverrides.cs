using FrontEndCSharp.RuntimeOverrides;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(System.Net.Http.BrowserHttpReadStream))]
internal static class BrowserHttpReadStreamOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern int Read(
        System.Net.Http.BrowserHttpReadStream @this,
        byte[] buffer,
        int offset,
        int count);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Task<int> ReadAsync(
        System.Net.Http.BrowserHttpReadStream @this,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken);

    [JavaScriptOverride(
        MemberKind = RuntimeOverrideMemberKind.InstanceMethod,
        TargetMemberName = "Dispose")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Dispose(
        System.Net.Http.BrowserHttpReadStream @this,
        bool disposing);
}
