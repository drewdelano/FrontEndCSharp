using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FrontEndCSharp.Debugging;

/// <summary>
/// Incrementally frames a Debug Adapter Protocol byte stream and permits a
/// payload-only transformation without interpreting or dropping unknown DAP
/// and Visual Studio custom-protocol messages.
/// </summary>
public sealed class DapFrameTransformer
{
    private const int MaximumHeaderBytes = 64 * 1024;
    private const int MaximumPayloadBytes = 64 * 1024 * 1024;
    private static readonly byte[] HeaderTerminator = { 13, 10, 13, 10 };
    private readonly Func<string, IReadOnlyList<string>> transform;
    private readonly List<byte> pending = new List<byte>();

    public DapFrameTransformer(Func<string, string> transform)
        : this(payload => new[]
        {
            (transform ?? throw new ArgumentNullException(nameof(transform)))(payload)
        })
    {
    }

    public DapFrameTransformer(Func<string, IReadOnlyList<string>> transform)
    {
        this.transform = transform ?? throw new ArgumentNullException(nameof(transform));
    }

    public int PendingByteCount => pending.Count;

    public IReadOnlyList<byte[]> Push(byte[] bytes, int offset, int count)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }
        if (offset < 0 || count < 0 || offset > bytes.Length - count)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        for (var index = offset; index < offset + count; index++)
        {
            pending.Add(bytes[index]);
        }

        var output = new List<byte[]>();
        while (TryTakeFrame(out var frames))
        {
            output.AddRange(frames);
        }
        return output;
    }

    private bool TryTakeFrame(out IReadOnlyList<byte[]> frames)
    {
        frames = Array.Empty<byte[]>();
        var headerEnd = FindHeaderEnd();
        if (headerEnd < 0)
        {
            if (pending.Count > MaximumHeaderBytes)
            {
                throw new InvalidDataException("DAP headers exceeded the 64 KiB safety limit.");
            }
            return false;
        }

        var headerBytes = pending.Take(headerEnd).ToArray();
        var headerText = Encoding.ASCII.GetString(headerBytes);
        var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
        var contentLengthLines = lines
            .Select((line, index) => new { Line = line, Index = index })
            .Where(item => item.Line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (contentLengthLines.Length != 1)
        {
            throw new InvalidDataException("A DAP frame must contain exactly one Content-Length header.");
        }

        var lengthText = contentLengthLines[0].Line.Substring("Content-Length:".Length).Trim();
        if (!int.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var payloadLength)
            || payloadLength < 0
            || payloadLength > MaximumPayloadBytes)
        {
            throw new InvalidDataException("The DAP Content-Length header is invalid or exceeds 64 MiB.");
        }

        var frameLength = headerEnd + HeaderTerminator.Length + payloadLength;
        if (pending.Count < frameLength)
        {
            return false;
        }

        var originalFrame = pending.Take(frameLength).ToArray();
        var payloadOffset = headerEnd + HeaderTerminator.Length;
        var payload = Encoding.UTF8.GetString(originalFrame, payloadOffset, payloadLength);
        var transformedPayloads = transform(payload)
            ?? throw new InvalidDataException("A DAP payload transform returned null.");
        if (transformedPayloads.Any(item => item == null))
        {
            throw new InvalidDataException("A DAP payload transform returned a null payload.");
        }
        pending.RemoveRange(0, frameLength);

        if (transformedPayloads.Count == 1
            && string.Equals(payload, transformedPayloads[0], StringComparison.Ordinal))
        {
            frames = new[] { originalFrame };
            return true;
        }

        frames = transformedPayloads.Select(transformedPayload => BuildFrame(
            lines,
            contentLengthLines[0].Index,
            transformedPayload)).ToArray();
        return true;
    }

    private static byte[] BuildFrame(
        string[] originalHeaderLines,
        int contentLengthLineIndex,
        string transformedPayload)
    {
        var transformedBytes = Encoding.UTF8.GetBytes(transformedPayload);
        var lines = (string[])originalHeaderLines.Clone();
        lines[contentLengthLineIndex] =
            "Content-Length: " + transformedBytes.Length.ToString(CultureInfo.InvariantCulture);
        var transformedHeader = Encoding.ASCII.GetBytes(string.Join("\r\n", lines));
        var frame = new byte[transformedHeader.Length + HeaderTerminator.Length + transformedBytes.Length];
        Buffer.BlockCopy(transformedHeader, 0, frame, 0, transformedHeader.Length);
        Buffer.BlockCopy(HeaderTerminator, 0, frame, transformedHeader.Length, HeaderTerminator.Length);
        Buffer.BlockCopy(
            transformedBytes,
            0,
            frame,
            transformedHeader.Length + HeaderTerminator.Length,
            transformedBytes.Length);
        return frame;
    }

    private int FindHeaderEnd()
    {
        for (var index = 0; index <= pending.Count - HeaderTerminator.Length; index++)
        {
            if (pending[index] == HeaderTerminator[0]
                && pending[index + 1] == HeaderTerminator[1]
                && pending[index + 2] == HeaderTerminator[2]
                && pending[index + 3] == HeaderTerminator[3])
            {
                return index;
            }
        }
        return -1;
    }
}
