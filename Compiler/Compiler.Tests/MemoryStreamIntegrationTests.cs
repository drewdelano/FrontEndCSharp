namespace Compiler.Tests;

public sealed class MemoryStreamIntegrationTests : IntegrationTestBase
{
    [Test]
    public void MemoryStreamSupportsResizableReadWriteSeekLengthAndBufferAccess()
    {
        var output = CompileAndRun(
            """
            var stream = new MemoryStream();
            Console.WriteLine(stream.CanRead && stream.CanWrite && stream.CanSeek);
            stream.Capacity = 10;
            stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
            Console.WriteLine($"{stream.Length}:{stream.Position}:{stream.Capacity}");

            stream.Position = 1;
            var read = new byte[3];
            Console.WriteLine(stream.Read(read, 1, 2));
            Console.WriteLine($"{read[0]}:{read[1]}:{read[2]}");
            Console.WriteLine(stream.ReadByte());

            stream.Position = 5;
            stream.WriteByte(9);
            Console.WriteLine($"{stream.Length}:{stream.Position}");
            Console.WriteLine(stream.Seek(-1, SeekOrigin.End));
            Console.WriteLine(stream.ReadByte());

            stream.SetLength(8);
            var copy = stream.ToArray();
            Console.WriteLine($"{copy.Length}:{copy[3]}:{copy[4]}:{copy[5]}:{copy[7]}");
            Console.WriteLine(stream.GetBuffer().Length);
            Console.WriteLine(stream.TryGetBuffer(out var segment));
            Console.WriteLine($"{segment.Offset}:{segment.Count}:{segment.Array![5]}");

            stream.Dispose();
            Console.WriteLine(stream.CanRead || stream.CanWrite || stream.CanSeek);
            Console.WriteLine(stream.ToArray().Length);
            try
            {
                _ = stream.Length;
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("disposed length");
            }
            try
            {
                stream.WriteByte(1);
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("disposed write");
            }
            """);

        Assert.Equal(
            "True\n3:3:10\n2\n0:2:3\n-1\n6:6\n5\n9\n8:0:0:9:0\n10\nTrue\n0:8:9\nFalse\n8\ndisposed length\ndisposed write",
            output);
    }

    [Test]
    public void MemoryStreamSupportsFixedWritableBuffersAndExposureRules()
    {
        var output = CompileAndRun(
            """
            var bytes = new byte[] { 10, 20, 30 };
            var stream = new MemoryStream(bytes);
            Console.WriteLine($"{stream.Length}:{stream.Capacity}:{stream.Position}");
            stream.Position = 1;
            stream.WriteByte(99);
            Console.WriteLine(bytes[1]);
            stream.Capacity = 3;

            try
            {
                _ = stream.GetBuffer();
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("hidden buffer");
            }
            Console.WriteLine(stream.TryGetBuffer(out _));

            stream.Position = 3;
            try
            {
                stream.WriteByte(1);
            }
            catch (NotSupportedException)
            {
                Console.WriteLine("fixed write");
            }
            try
            {
                stream.SetLength(4);
            }
            catch (NotSupportedException)
            {
                Console.WriteLine("fixed length");
            }
            var copy = stream.ToArray();
            Console.WriteLine($"{copy[0]},{copy[1]},{copy[2]}");
            """);

        Assert.Equal(
            "3:3:0\n99\nhidden buffer\nFalse\nfixed write\nfixed length\n10,99,30",
            output);
    }

    [Test]
    public void MemoryStreamValidatesBoundsAndSupportsByteArrayAsyncOperations()
    {
        var output = CompileAndRun(
            """
            var stream = new MemoryStream();
            try
            {
                stream.Read(null!, 0, 0);
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine("null buffer");
            }
            try
            {
                stream.Write(new byte[1], -1, 1);
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("negative offset");
            }
            try
            {
                stream.Read(new byte[1], 1, 1);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("invalid range");
            }
            try
            {
                stream.Position = -1;
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("negative position");
            }
            try
            {
                stream.Seek(-1, SeekOrigin.Begin);
            }
            catch (IOException)
            {
                Console.WriteLine("before origin");
            }

            await stream.WriteAsync(new byte[] { 4, 5, 6 }, 0, 3, CancellationToken.None);
            stream.Position = 0;
            var destination = new byte[3];
            Console.WriteLine(await stream.ReadAsync(destination, 0, 3, CancellationToken.None));
            Console.WriteLine($"{destination[0]},{destination[1]},{destination[2]}");

            var canceled = new CancellationToken(true);
            Console.WriteLine((await ObserveCancellation(stream.ReadAsync(destination, 0, 1, canceled))));
            Console.WriteLine((await ObserveCancellation(stream.WriteAsync(destination, 0, 1, canceled))));

            static async Task<string> ObserveCancellation(Task task)
            {
                try
                {
                    await task;
                    return "completed";
                }
                catch (TaskCanceledException)
                {
                    return "canceled";
                }
            }
            """);

        Assert.Equal(
            "null buffer\nnegative offset\ninvalid range\nnegative position\nbefore origin\n3\n4,5,6\ncanceled\ncanceled",
            output);
    }
}
