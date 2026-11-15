# Browser HTTP

This document defines the bounded `System.Net.Http`-over-Fetch goal. A C# developer
should be able to use the ordinary `HttpClient` workflow for common browser web APIs
without learning a second HTTP object model. Complete `System.Net.Http`,
`SocketsHttpHandler`, or socket-level parity is not a project goal.

## Product outcome

The first completion boundary is a dependable buffered workflow:

- ordinary `HttpClient`, `HttpRequestMessage`, `HttpResponseMessage`, and
  `HttpContent` APIs;
- common GET, POST, PUT, PATCH, DELETE, and custom `SendAsync` requests;
- absolute and `BaseAddress`-relative HTTP/HTTPS URIs;
- common request, content, response, and content headers, including authorization;
- buffered text, byte-array, and JSON request and response bodies;
- status, visible response headers, non-success responses, and useful transport
  failures; and
- disposal, in-flight cancellation, and `HttpClient.Timeout`.

This is the same kind of enabling slice as JSON support: broad enough for ordinary
web application code, but not an independent compatibility program for every
`System.Net.Http` API or handler setting. Form and multipart content can be added
when an agreed application scenario requires them; they do not block the buffered
web-API outcome.

Asynchronous response streaming is also complete for consuming incremental output
without waiting for the complete body. Request streaming remains a separate,
capability-gated extension because browser support and Fetch requirements differ.

## Architecture boundary

Use `HttpClient`, `HttpRequestMessage`, `HttpResponseMessage`, `HttpContent`, and
`System.Net.Http.Json` as the public application model. Common environment-neutral
behavior should remain managed MSIL. The selected `FrontEndCSharp.Overrides.System.Net.Http.fecs.dll`
companion adapts only the terminal `HttpMessageHandler.SendAsync` operation and the
small number of browser-bound setup paths that cannot yet reach it through usable
managed MSIL.

The terminal operation calls Fetch. Do not introduce a separately named Fetch
client: that would duplicate URI, header, content, cancellation, and JSON APIs and
make shared desktop/browser services harder to write. A lower-level browser API is
justified only for a feature that cannot be represented through
`HttpRequestMessage.Options`.

A focused `FrontEndCSharp.Overrides.System.Private.Uri.fecs.dll` companion supplies the URI seam needed by
ordinary `HttpClient.BaseAddress` code. JSON remains a layer above the transport:
the HTTP companion handles bytes, media types, and headers, while
`System.Net.Http.Json` and the selected serializer companion own JSON behavior.

## Browser-owned policy

Delegate CORS, cookies, forbidden headers, redirects, caching, and related browser
policy to Fetch:

- A CORS rejection is an opaque Fetch network failure. Do not attempt to detect or
  label it as CORS; surface the rejected operation as the appropriate managed
  transport failure.
- The browser cookie jar supplies same-origin cookies under Fetch defaults.
  Cross-origin cookies require an explicit credentials opt-in and remain subject to
  server CORS consent and normal cookie policy.
- Fetch follows redirects by default. Preserve that default instead of reproducing
  redirect processing in the companion.
- Browser-forbidden headers such as `Cookie` and `Host` remain browser-controlled
  even when managed code attempts to add them.

The browser adapter exposes typed credentials, cache, mode, redirect, and integrity
options through `HttpRequestMessage.Options`. They are optional browser escape
hatches, not requirements for ordinary requests. Credentials are the important
opt-in for cross-origin cookie scenarios. Omitting an option retains the Fetch
default. Invalid combinations and policy failures remain Fetch failures rather than
being reinterpreted by the companion.

Application code cannot control browser TLS certificates, client certificates,
proxies, socket pooling, raw protocol negotiation, or synchronous network I/O.
Unsupported `HttpClientHandler` settings must fail explicitly rather than pretend
to work.

## Current buffered surface

The current transport supports absolute and `BaseAddress`-relative requests for
Fetch-permitted methods, the common `GetAsync`, `PostAsync`, `PutAsync`,
`PatchAsync`, and `DeleteAsync` conveniences, custom `SendAsync`, and the default
`HttpCompletionOption.ResponseContentRead`. It carries request-specific and default
headers added through `HttpHeaders.Add`, including `Authorization` and `Accept`, and
supports UTF-8 and UTF-16 `StringContent`, caller-selected string media types, and
`ByteArrayContent`.

Responses preserve status and reason phrase, visible response and content headers,
case-insensitive `Contains`, `GetValues`, and `TryGetValues` lookup, header-entry
enumeration, non-success responses without implicit exceptions, UTF-8 strings,
charset-aware strings (including ISO-8859-1), typed content media type and charset,
exact buffered bytes, empty and large bodies, concurrent requests, and request
association. Fetch provides automatic redirects, same-origin cookies, CORS
enforcement, and browser header policy. The companion supports already-canceled
requests, in-flight cancellation, `HttpClient.Timeout`, network-failure mapping,
disposal state, and the browser-specific request options described above.

The common buffered boundary is complete. `FormUrlEncodedContent` remains
explicitly deferred.

`CONNECT`, `TRACE`, and `TRACK`, bodies on GET or HEAD, synchronous send, and
application control over socket-handler settings fail explicitly. These are not
buffered-completion blockers.

## Managed JSON layer

`FrontEndCSharp.Overrides.System.Net.Http.Json.fecs` turns buffered `HttpContent` into an ordinary managed
object through `ReadFromJsonAsync<T>(CancellationToken)` and supports the familiar
`GetFromJsonAsync<T>`, `PostAsJsonAsync<T>`, and `PutAsJsonAsync<T>` workflows over
the verified `System.Text.Json` Web contract. The serializer dependency remains
closed over the caller's managed type, and JSON behavior stays in the JSON layer
rather than moving into Fetch or the compiler. A real-Chromium fixture uses one
server-observed JSON POST round trip to cover Web-default serialization, HTTP
transport, and buffered response deserialization for a representative POCO, list,
and dictionary graph. Focused tests own the duplicate GET and direct-content paths.
[`JSON.md`](JSON.md) records the serializer boundary and explicit deferrals.

## Current response-streaming surface

`HttpCompletionOption.ResponseHeadersRead` exposes a sealed
`System.Net.Http.BrowserHttpReadStream` through the public `Task<Stream>` contract,
matching the concrete browser implementation type used by .NET. It is backed by the
Fetch response body's `ReadableStream` reader. `ReadAsync` supports progressive
partial reads without losing a browser chunk's unread suffix, returns zero at end
of stream, accepts per-read cancellation, rejects reads after disposal, and maps
browser body failures to managed `IOException`. Its capabilities report readable,
non-seekable, and non-writable, and unsupported length and synchronous operations
fail explicitly. Disposing either the response or stream cancels the reader.

The buffered response path returns a concrete `System.IO.MemoryStream`, preserving
the desktop/browser distinction without changing `HttpContent.ReadAsStreamAsync`'s
declared return type. The CoreLib companion supplies only the bounded memory-read
seams required when package-neutral possible-type analysis retains both concrete
results at a virtual stream call site.

The real-Chromium acceptance fixture withholds its second server chunk until the
managed application has read and reported the first. This handshake proves that
the response task and reads complete progressively rather than after hidden whole-
body buffering and covers end-of-stream behavior. Cancellation, disposal, synchronous-read
rejection, capability metadata, and truncated responses stay outside the routine
browser gate so dependency expansion cannot hide a hang behind a feature matrix.

Request content remains buffered. A content object without the companion's buffered
byte payload fails explicitly with `PlatformNotSupportedException`; it is never sent
as an empty body. Any future request-streaming extension must feature-detect the
required browser support and configure Fetch explicitly before changing that
boundary.

## Verification

Use controlled servers and real Chromium for browser-bound behavior. The routine
buffered gate is a representative same-origin GET/POST vertical slice that proves
status, content, response headers, and outbound request data. Keep the broader
method, content, CORS, browser policy, cancellation, timeout, disposal, failure,
and concurrency matrix in focused C# tests; browser-only policy cases should verify
delegation to the browser rather than attempt to diagnose browser policy.

Compare shared behavior with desktop `HttpClient` and record intentional browser
differences explicitly. Keep the representative buffered gate separate from the
focused response-streaming vertical slice so neither routine browser fixture grows
into an unbounded feature matrix.

The HTTP JSON Chromium harness uses the DevTools protocol to wait on real wall-clock
page completion and to release the delayed streaming chunk after observing the
managed first-read marker. It is the streaming-progress authority because Chromium
virtual-time budgets pause active-time timers while network work is pending. The
representative buffered gate can use virtual time because streaming-progress
behavior is not part of its contract.

Run the buffered transport slice and HTTP JSON vertical slice with:

```powershell
dotnet run --no-build --project Overrides/FrontEndCSharp.Overrides.System.Net.Http/Tests/FrontEndCSharp.Overrides.HttpClientBrowserSmoke/FrontEndCSharp.Overrides.HttpClientBrowserSmoke.csproj -- "C:\Program Files\Google\Chrome\Application\chrome.exe"
dotnet run --no-build --project Overrides/FrontEndCSharp.Overrides.System.Net.Http/Tests/FrontEndCSharp.Overrides.HttpStreamingBrowserSmoke/FrontEndCSharp.Overrides.HttpStreamingBrowserSmoke.csproj -- "C:\Program Files\Google\Chrome\Application\chrome.exe"
dotnet run --no-build --project Overrides/FrontEndCSharp.Overrides.System.Net.Http.Json/Tests/FrontEndCSharp.Overrides.HttpJsonBrowserSmoke/FrontEndCSharp.Overrides.HttpJsonBrowserSmoke.csproj -- "C:\Program Files\Google\Chrome\Application\chrome.exe"
```
