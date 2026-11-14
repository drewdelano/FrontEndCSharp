using System.Text;
using FrontEndCSharp.Debugging;

namespace Debugger.Tests;

public sealed class DapTransformStreamTests
{
    [Test]
    public async Task WriteStreamBuffersFragmentsAndPreservesOrder()
    {
        var first = Frame("{\"seq\":1,\"type\":\"request\",\"command\":\"initialize\"}");
        var second = Frame("{\"seq\":2,\"type\":\"request\",\"command\":\"launch\"}");
        var bytes = first.Concat(second).ToArray();
        using var destination = new MemoryStream();
        using var stream = new DapTransformWriteStream(destination, payload => payload, leaveOpen: true);

        await stream.WriteAsync(bytes, 0, 11);
        Assert.Empty(destination.ToArray());
        await stream.WriteAsync(bytes, 11, bytes.Length - 11);

        Assert.Equal(bytes, destination.ToArray());
    }

    [Test]
    public void WriteStreamCanInjectAProxyOwnedRequestWithoutClientTransformation()
    {
        using var destination = new MemoryStream();
        using var stream = new DapTransformWriteStream(
            destination,
            payload => payload.Replace("client", "transformed", StringComparison.Ordinal),
            leaveOpen: true);
        const string request =
            "{\"seq\":1500000000,\"type\":\"request\",\"command\":\"evaluate\",\"arguments\":{\"expression\":\"client\"}}";

        stream.InjectPayload(request);

        Assert.Equal(Frame(request), destination.ToArray());
    }

    [Test]
    public async Task ReadStreamHandlesFragmentedInnerReadsAndSmallConsumerBuffers()
    {
        var first = Frame("{\"seq\":7,\"type\":\"event\",\"event\":\"initialized\"}");
        var second = Frame("{\"seq\":8,\"type\":\"event\",\"event\":\"output\"}");
        var bytes = first.Concat(second).ToArray();
        using var source = new ChunkedReadStream(bytes, maximumChunkSize: 5);
        using var stream = new DapTransformReadStream(source, payload => payload);
        using var received = new MemoryStream();
        var buffer = new byte[7];

        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
        {
            received.Write(buffer, 0, read);
        }

        Assert.Equal(bytes, received.ToArray());
    }

    [Test]
    public void ReadStreamRejectsTruncatedFrame()
    {
        var frame = Frame("{\"seq\":9,\"type\":\"event\",\"event\":\"stopped\"}");
        using var source = new MemoryStream(frame.Take(frame.Length - 2).ToArray());
        using var stream = new DapTransformReadStream(source, payload => payload);
        var buffer = new byte[frame.Length];

        var exception = Assert.Throws<EndOfStreamException>(() =>
            stream.Read(buffer, 0, buffer.Length));

        Assert.Contains("middle of a frame", exception.Message);
    }

    [Test]
    public async Task ReadStreamCanWithholdAndExpandProtocolFrames()
    {
        var withheld = Frame("{\"seq\":1,\"type\":\"event\",\"event\":\"withheld\"}");
        var expanded = Frame("{\"seq\":2,\"type\":\"event\",\"event\":\"expanded\"}");
        using var source = new MemoryStream(withheld.Concat(expanded).ToArray());
        using var stream = new DapTransformReadStream(
            source,
            payload => payload.Contains("withheld", StringComparison.Ordinal)
                ? Array.Empty<string>()
                : new[]
                {
                    payload.Replace("expanded", "first", StringComparison.Ordinal),
                    payload.Replace("expanded", "second", StringComparison.Ordinal)
                });
        using var received = new MemoryStream();
        var buffer = new byte[17];

        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
        {
            received.Write(buffer, 0, read);
        }

        Assert.Equal(
            Frame("{\"seq\":2,\"type\":\"event\",\"event\":\"first\"}")
                .Concat(Frame("{\"seq\":2,\"type\":\"event\",\"event\":\"second\"}")),
            received.ToArray());
    }

    private static byte[] Frame(string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        return Encoding.ASCII.GetBytes($"Content-Length: {payloadBytes.Length}\r\n\r\n")
            .Concat(payloadBytes)
            .ToArray();
    }

    private sealed class ChunkedReadStream : MemoryStream
    {
        private readonly int maximumChunkSize;

        public ChunkedReadStream(byte[] bytes, int maximumChunkSize)
            : base(bytes)
        {
            this.maximumChunkSize = maximumChunkSize;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            base.Read(buffer, offset, Math.Min(count, maximumChunkSize));

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            base.ReadAsync(buffer, offset, Math.Min(count, maximumChunkSize), cancellationToken);
    }
}
