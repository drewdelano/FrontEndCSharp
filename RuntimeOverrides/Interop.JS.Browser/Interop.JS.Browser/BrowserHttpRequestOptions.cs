using System;
using System.Net.Http;

namespace Interop.JS.Browser;

public enum BrowserRequestCredentials
{
    Omit,
    SameOrigin,
    Include,
}

public enum BrowserRequestCache
{
    Default,
    NoStore,
    Reload,
    NoCache,
    ForceCache,
    OnlyIfCached,
}

public enum BrowserRequestMode
{
    Cors,
    NoCors,
    SameOrigin,
}

public enum BrowserRedirectMode
{
    Follow,
    Error,
    Manual,
}

public static class BrowserHttpRequestOptions
{
    public const string CredentialsOptionName = "FrontEndCSharp.Browser.RequestCredentials";
    public const string CacheOptionName = "FrontEndCSharp.Browser.RequestCache";
    public const string ModeOptionName = "FrontEndCSharp.Browser.RequestMode";
    public const string RedirectOptionName = "FrontEndCSharp.Browser.RedirectMode";
    public const string IntegrityOptionName = "FrontEndCSharp.Browser.Integrity";

    public static void SetBrowserCredentials(
        this HttpRequestMessage request,
        BrowserRequestCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Options.Set(
            new HttpRequestOptionsKey<BrowserRequestCredentials>(CredentialsOptionName),
            credentials);
    }

    public static void SetBrowserCache(this HttpRequestMessage request, BrowserRequestCache cache)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Options.Set(new HttpRequestOptionsKey<BrowserRequestCache>(CacheOptionName), cache);
    }

    public static void SetBrowserMode(this HttpRequestMessage request, BrowserRequestMode mode)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Options.Set(new HttpRequestOptionsKey<BrowserRequestMode>(ModeOptionName), mode);
    }

    public static void SetBrowserRedirect(this HttpRequestMessage request, BrowserRedirectMode redirect)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Options.Set(new HttpRequestOptionsKey<BrowserRedirectMode>(RedirectOptionName), redirect);
    }

    public static void SetBrowserIntegrity(this HttpRequestMessage request, string integrity)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(integrity);
        request.Options.Set(new HttpRequestOptionsKey<string>(IntegrityOptionName), integrity);
    }
}
