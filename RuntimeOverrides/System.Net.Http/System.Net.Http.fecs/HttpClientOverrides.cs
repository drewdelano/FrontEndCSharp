using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.HttpRuntimeOverrides;

[RuntimeOverrideType(typeof(HttpClient))]
internal sealed class HttpClientOverrides : HttpClientHandler
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void CheckRequestBeforeSend(HttpClient @this, HttpRequestMessage request);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void CheckRequestMessage(HttpRequestMessage request);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Uri CreateUri(string? uri);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Uri? get_BaseAddress(HttpClient @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern HttpRequestHeaders get_DefaultRequestHeaders(HttpClient @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern TimeSpan get_Timeout(HttpClient @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Task<string> GetStringAsync(HttpClient @this, string? requestUri);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> GetAsync(
        HttpClient @this,
        string? requestUri) =>
        @this.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, requestUri),
            HttpCompletionOption.ResponseContentRead,
            default);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> GetAsync(
        HttpClient @this,
        string? requestUri,
        CancellationToken cancellationToken) =>
        @this.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, requestUri),
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> GetAsync(
        HttpClient @this,
        string? requestUri,
        HttpCompletionOption completionOption) =>
        @this.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, requestUri),
            completionOption,
            default);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> GetAsync(
        HttpClient @this,
        string? requestUri,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken) =>
        @this.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, requestUri),
            completionOption,
            cancellationToken);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_BaseAddress(HttpClient @this, Uri? value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void set_Timeout(HttpClient @this, TimeSpan value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> SendAsync(
        HttpClient @this,
        HttpRequestMessage request) =>
        @this.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            default);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> SendAsync(
        HttpClient @this,
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        @this.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> SendAsync(
        HttpClient @this,
        HttpRequestMessage request,
        HttpCompletionOption completionOption) =>
        @this.SendAsync(request, completionOption, default);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> SendAsync(
        HttpClient @this,
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        _ = new CancellationTokenSource();
        return new HttpClientOverrides().SendAsync(request, cancellationToken);
    }

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> PostAsync(
        HttpClient @this,
        string? requestUri,
        HttpContent? content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content,
        };
        return @this.SendAsync(request);
    }

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> PutAsync(
        HttpClient @this,
        string? requestUri,
        HttpContent? content)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = content,
        };
        return @this.SendAsync(request);
    }

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> PatchAsync(
        HttpClient @this,
        string? requestUri,
        HttpContent? content)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, requestUri)
        {
            Content = content,
        };
        return @this.SendAsync(request);
    }

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Task<HttpResponseMessage> DeleteAsync(
        HttpClient @this,
        string? requestUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        return @this.SendAsync(request);
    }

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static void PrepareRequestMessage(HttpClient @this, HttpRequestMessage request) =>
        throw new InvalidOperationException("The request URI must be an absolute HTTP or HTTPS URI.");

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ValueTuple<CancellationTokenSource, bool, CancellationTokenSource>
        PrepareCancellationTokenSource(HttpClient @this, CancellationToken cancellationToken);

    [JavaScriptOverride(
        MemberKind = RuntimeOverrideMemberKind.InstanceMethod,
        TargetMemberName = "<SendAsync>g__Core|83_0")]
    internal static Task<HttpResponseMessage> SendAsyncCore(
        HttpClient @this,
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationTokenSource cancellationTokenSource,
        bool disposeCancellationTokenSource,
        CancellationTokenSource pendingRequestsCancellationTokenSource,
        CancellationToken originalCancellationToken) =>
        new HttpClientOverrides().SendAsync(request, originalCancellationToken);
}
