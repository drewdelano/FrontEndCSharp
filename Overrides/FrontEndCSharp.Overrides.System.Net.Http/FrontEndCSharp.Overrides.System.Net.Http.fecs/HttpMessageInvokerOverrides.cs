using FrontEndCSharp.RuntimeOverrides;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(HttpMessageInvoker))]
internal static class HttpMessageInvokerOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern HttpMessageInvoker Create(HttpMessageHandler handler, bool disposeHandler);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Dispose(HttpMessageInvoker @this);
}
