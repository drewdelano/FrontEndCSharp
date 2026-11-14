using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FrontEndCSharp.Debugging;

public sealed class DapTransformWriteStream : Stream
{
    private readonly Stream inner;
    private readonly DapFrameTransformer transformer;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private readonly bool leaveOpen;

    public DapTransformWriteStream(
        Stream inner,
        Func<string, string> transform,
        bool leaveOpen = false)
        : this(
            inner,
            payload => new[]
            {
                (transform ?? throw new ArgumentNullException(nameof(transform)))(payload)
            },
            leaveOpen)
    {
    }

    public DapTransformWriteStream(
        Stream inner,
        Func<string, IReadOnlyList<string>> transform,
        bool leaveOpen = false)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (!inner.CanWrite)
        {
            throw new ArgumentException("The inner DAP stream must be writable.", nameof(inner));
        }
        transformer = new DapFrameTransformer(transform);
        this.leaveOpen = leaveOpen;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        gate.Wait();
        try
        {
            inner.Flush();
        }
        finally
        {
            gate.Release();
        }
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        gate.Wait();
        try
        {
            foreach (var frame in transformer.Push(buffer, offset, count))
            {
                inner.Write(frame, 0, frame.Length);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var frame in transformer.Push(buffer, offset, count))
            {
                await inner.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public void InjectPayload(string payload)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }
        var frame = EncodeFrame(payload);
        gate.Wait();
        try
        {
            inner.Write(frame, 0, frame.Length);
            inner.Flush();
        }
        finally
        {
            gate.Release();
        }
    }

    private static byte[] EncodeFrame(string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        return Encoding.ASCII.GetBytes(
                "Content-Length: "
                + payloadBytes.Length.ToString(CultureInfo.InvariantCulture)
                + "\r\n\r\n")
            .Concat(payloadBytes)
            .ToArray();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            gate.Dispose();
            if (!leaveOpen)
            {
                inner.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

public sealed class DapTransformReadStream : Stream
{
    private const int ReadBufferSize = 16 * 1024;
    private readonly Stream inner;
    private readonly DapFrameTransformer transformer;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private readonly Queue<byte[]> ready = new Queue<byte[]>();
    private readonly bool leaveOpen;
    private byte[]? current;
    private int currentOffset;

    public DapTransformReadStream(
        Stream inner,
        Func<string, string> transform,
        bool leaveOpen = false)
        : this(
            inner,
            payload => new[]
            {
                (transform ?? throw new ArgumentNullException(nameof(transform)))(payload)
            },
            leaveOpen)
    {
    }

    public DapTransformReadStream(
        Stream inner,
        Func<string, IReadOnlyList<string>> transform,
        bool leaveOpen = false)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (!inner.CanRead)
        {
            throw new ArgumentException("The inner DAP stream must be readable.", nameof(inner));
        }
        transformer = new DapFrameTransformer(transform);
        this.leaveOpen = leaveOpen;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateReadArguments(buffer, offset, count);
        if (count == 0)
        {
            return 0;
        }

        gate.Wait();
        try
        {
            while (!EnsureCurrentFrame())
            {
                var input = new byte[ReadBufferSize];
                var read = inner.Read(input, 0, input.Length);
                if (read == 0)
                {
                    EnsureCompleteEndOfStream();
                    return 0;
                }
                Enqueue(transformer.Push(input, 0, read));
            }
            return CopyCurrent(buffer, offset, count);
        }
        finally
        {
            gate.Release();
        }
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ValidateReadArguments(buffer, offset, count);
        if (count == 0)
        {
            return 0;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (!EnsureCurrentFrame())
            {
                var input = new byte[ReadBufferSize];
                var read = await inner.ReadAsync(input, 0, input.Length, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    EnsureCompleteEndOfStream();
                    return 0;
                }
                Enqueue(transformer.Push(input, 0, read));
            }
            return CopyCurrent(buffer, offset, count);
        }
        finally
        {
            gate.Release();
        }
    }

    public override void Flush()
    {
    }

    private void Enqueue(IReadOnlyList<byte[]> frames)
    {
        foreach (var frame in frames)
        {
            ready.Enqueue(frame);
        }
    }

    private bool EnsureCurrentFrame()
    {
        if (current !=  null && currentOffset < current.Length)
        {
            return true;
        }
        if (ready.Count == 0)
        {
            current = null;
            currentOffset = 0;
            return false;
        }
        current = ready.Dequeue();
        currentOffset = 0;
        return true;
    }

    private int CopyCurrent(byte[] buffer, int offset, int count)
    {
        var frame = current ?? throw new InvalidOperationException("No transformed DAP frame is available.");
        var copied = Math.Min(count, frame.Length - currentOffset);
        Buffer.BlockCopy(frame, currentOffset, buffer, offset, copied);
        currentOffset += copied;
        return copied;
    }

    private void EnsureCompleteEndOfStream()
    {
        if (transformer.PendingByteCount != 0)
        {
            throw new EndOfStreamException("The DAP output stream ended in the middle of a frame.");
        }
    }

    private static void ValidateReadArguments(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            gate.Dispose();
            if (!leaveOpen)
            {
                inner.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
