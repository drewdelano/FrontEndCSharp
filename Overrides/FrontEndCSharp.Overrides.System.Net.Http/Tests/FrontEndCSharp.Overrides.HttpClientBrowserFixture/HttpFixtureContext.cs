using Interop.JS.Browser;

internal sealed class HttpFixtureEndpoints
{
    private HttpFixtureEndpoints(
        string endpoint,
        string echoEndpoint,
        string baseEndpoint)
    {
        Endpoint = endpoint;
        EchoEndpoint = echoEndpoint;
        BaseEndpoint = baseEndpoint;
    }

    public string Endpoint { get; }
    public string EchoEndpoint { get; }
    public string BaseEndpoint { get; }

    public static HttpFixtureEndpoints? Read()
    {
        var endpoint = Document.Body.GetAttribute("data-http-endpoint");
        var echoEndpoint = Document.Body.GetAttribute("data-http-echo-endpoint");
        var baseEndpoint = Document.Body.GetAttribute("data-http-base-endpoint");
        if (string.IsNullOrWhiteSpace(endpoint)
            || string.IsNullOrWhiteSpace(echoEndpoint)
            || string.IsNullOrWhiteSpace(baseEndpoint))
        {
            return null;
        }

        return new HttpFixtureEndpoints(
            endpoint,
            echoEndpoint,
            baseEndpoint);
    }
}

internal sealed class HttpFixtureContext : IDisposable
{
    public HttpFixtureContext(HttpFixtureEndpoints endpoints)
    {
        Endpoints = endpoints;
        Client = new HttpClient { BaseAddress = new Uri(endpoints.BaseEndpoint) };
    }

    public HttpFixtureEndpoints Endpoints { get; }
    public HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
    }
}
