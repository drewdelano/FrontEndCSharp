using System.Text;
using FrontEndCSharp.Debugging;

namespace Debugger.Tests;

public sealed class DapFrameTransformerTests
{
    [Test]
    public void ReassemblesFragmentedFrameWithoutChangingBytes()
    {
        var frame = Frame("{\"seq\":1,\"type\":\"request\",\"command\":\"initialize\"}");
        var transformer = new DapFrameTransformer(payload => payload);

        Assert.Empty(transformer.Push(frame, 0, 7));
        Assert.Empty(transformer.Push(frame, 7, 19));
        var output = transformer.Push(frame, 26, frame.Length - 26);

        Assert.Single(output);
        Assert.Equal(frame, output[0]);
    }

    [Test]
    public void EmitsEveryCompleteFrameFromOneChunk()
    {
        var first = Frame("{\"seq\":1,\"type\":\"event\",\"event\":\"initialized\"}");
        var second = Frame("{\"seq\":2,\"type\":\"event\",\"event\":\"output\"}");
        var input = first.Concat(second).ToArray();
        var transformer = new DapFrameTransformer(payload => payload);

        var output = transformer.Push(input, 0, input.Length);

        Assert.Equal(2, output.Count);
        Assert.Equal(first, output[0]);
        Assert.Equal(second, output[1]);
    }

    [Test]
    public void RewritesPayloadAndContentLengthWhilePreservingOtherHeaders()
    {
        const string original = "{\"seq\":3,\"type\":\"request\",\"command\":\"setBreakpoints\"}";
        const string rewritten = "{\"seq\":3,\"type\":\"request\",\"command\":\"setBreakpoints\",\"arguments\":{}}";
        var frame = Frame(original, "X-FrontEndCSharp: retained\r\n");
        var transformer = new DapFrameTransformer(payload =>
            payload == original ? rewritten : payload);

        var output = Assert.Single(transformer.Push(frame, 0, frame.Length));
        var text = Encoding.UTF8.GetString(output);

        Assert.Contains("X-FrontEndCSharp: retained\r\n", text);
        Assert.Contains($"Content-Length: {Encoding.UTF8.GetByteCount(rewritten)}\r\n", text);
        Assert.EndsWith(rewritten, text, StringComparison.Ordinal);
    }

    [Test]
    public void PreservesUnknownCustomProtocolMessage()
    {
        var frame = Frame(
            "{\"seq\":41,\"type\":\"request\",\"command\":\"visualstudio.custom.unknown\","
            + "\"arguments\":{\"opaque\":\"keep me\"}}");
        var transformer = new DapFrameTransformer(payload => payload);

        var output = Assert.Single(transformer.Push(frame, 0, frame.Length));

        Assert.Equal(frame, output);
    }

    [Test]
    public void RejectsDuplicateContentLengthHeaders()
    {
        var bytes = Encoding.ASCII.GetBytes(
            "Content-Length: 2\r\nContent-Length: 2\r\n\r\n{}");
        var transformer = new DapFrameTransformer(payload => payload);

        var exception = Assert.Throws<InvalidDataException>(() =>
            transformer.Push(bytes, 0, bytes.Length));

        Assert.Contains("exactly one Content-Length", exception.Message);
    }

    private static byte[] Frame(string payload, string additionalHeaders = "")
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var headerBytes = Encoding.ASCII.GetBytes(
            $"Content-Length: {payloadBytes.Length}\r\n{additionalHeaders}\r\n");
        return headerBytes.Concat(payloadBytes).ToArray();
    }
}
