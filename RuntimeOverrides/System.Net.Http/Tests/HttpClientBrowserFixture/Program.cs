using Interop.JS.Browser;
using System.Net;

var endpoints = HttpFixtureEndpoints.Read();
if (endpoints == null)
{
    Document.Body.SetAttribute("data-fecs-status", "missing-endpoint");
    return;
}

using var context = new HttpFixtureContext(endpoints);
using var response = await context.Client.GetAsync(endpoints.Endpoint);
var content = await response.Content.ReadAsStringAsync();
var bytes = await response.Content.ReadAsByteArrayAsync();
var headerValues = (string[])response.Headers.GetValues("X-Fecs-Multi");
var combinedHeaderValue = headerValues[0];

using var request = new HttpRequestMessage(HttpMethod.Post, endpoints.EchoEndpoint);
request.Headers.Add("X-Fecs-Request", "request-header");
request.Content = new StringContent("browser request — buffered");
using var requestResponse = await context.Client.SendAsync(request);
var requestEcho = await requestResponse.Content.ReadAsStringAsync();

var succeeded = response.StatusCode == HttpStatusCode.Accepted
    && response.ReasonPhrase == "Accepted"
    && content == "FrontEndCSharp HTTP response — buffered"
    && bytes.Length == 41
    && bytes[0] == 0x46
    && bytes[40] == 0x64
    && combinedHeaderValue == "first, second"
    && requestResponse.StatusCode == HttpStatusCode.Created
    && requestEcho == "POST|request-header|text/plain; charset=utf-8||*/*|browser request — buffered";

Document.Body.SetAttribute("data-fecs-http-body", content);
Document.Body.SetAttribute("data-fecs-http-bytes", bytes.Length.ToString());
Document.Body.SetAttribute("data-fecs-http-multi", combinedHeaderValue);
Document.Body.SetAttribute("data-fecs-http-post", requestEcho);
Document.Body.SetAttribute("data-fecs-status", succeeded ? "ready" : "failed");
