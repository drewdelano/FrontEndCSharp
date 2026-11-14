using FrontEndCSharp.RuntimeOverrides;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http;

[RuntimeOverrideImplementationType]
internal sealed class BrowserHttpReadStream : Stream
{
    internal BrowserHttpReadStream()
    {
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

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new PlatformNotSupportedException(
            "Synchronous reads are not supported for browser response streams.");

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
