using Interop.JS.Browser;

var endpoint = Document.Body.GetAttribute("data-http-stream-endpoint");
if (string.IsNullOrWhiteSpace(endpoint))
{
    Document.Body.SetAttribute("data-fecs-status", "missing-endpoint");
    return;
}

using var client = new HttpClient();
using var response = await client.GetAsync(
    endpoint,
    HttpCompletionOption.ResponseHeadersRead);
using var body = await response.Content.ReadAsStreamAsync();

var firstChunk = new byte[5];
var firstChunkCount = await ReadExactlyAsync(body, firstChunk);
var firstChunkVerified = firstChunkCount == firstChunk.Length
    && firstChunk[0] == (byte)'f'
    && firstChunk[1] == (byte)'i'
    && firstChunk[2] == (byte)'r'
    && firstChunk[3] == (byte)'s'
    && firstChunk[4] == (byte)'t';
Document.Body.SetAttribute(
    "data-fecs-http-stream-first",
    firstChunkVerified ? "first" : "failed");

var secondChunk = new byte[6];
var secondChunkCount = await ReadExactlyAsync(body, secondChunk);
var endOfStream = await body.ReadAsync(new byte[1], 0, 1) == 0;
var secondChunkVerified = secondChunkCount == secondChunk.Length
    && secondChunk[0] == (byte)'s'
    && secondChunk[1] == (byte)'e'
    && secondChunk[2] == (byte)'c'
    && secondChunk[3] == (byte)'o'
    && secondChunk[4] == (byte)'n'
    && secondChunk[5] == (byte)'d';

Document.Body.SetAttribute(
    "data-fecs-http-stream-complete",
    secondChunkVerified && endOfStream ? "verified" : "failed");
Document.Body.SetAttribute(
    "data-fecs-status",
    firstChunkVerified && secondChunkVerified && endOfStream ? "ready" : "failed");

static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer)
{
    var offset = 0;
    while (offset < buffer.Length)
    {
        var count = await stream.ReadAsync(buffer, offset, buffer.Length - offset);
        if (count == 0)
        {
            break;
        }
        offset += count;
    }
    return offset;
}
