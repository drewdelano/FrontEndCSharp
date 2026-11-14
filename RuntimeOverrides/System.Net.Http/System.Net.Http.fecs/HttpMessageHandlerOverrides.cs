using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(HttpMessageHandler))]
internal static class HttpMessageHandlerOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> SendAsync(
        HttpMessageHandler @this,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _ = request.Method.Method;
        _ = request.RequestUri?.IsAbsoluteUri;
        _ = new HttpRequestException();
        _ = new PlatformNotSupportedException();

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        _ = new ByteArrayContent(new byte[0]);
        return Task.FromResult(response);
    }
}
