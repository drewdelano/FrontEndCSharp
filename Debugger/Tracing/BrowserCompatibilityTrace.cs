using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace FrontEndCSharp.VisualStudio;

internal static class BrowserCompatibilityTrace
{
    private static readonly ConcurrentQueue<string> Pending = new();
    private static int flushScheduled;

    internal static void Write(string eventName, string? detail = null)
    {
        try
        {
            var line = string.Join(
                "\t",
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                eventName,
                detail ?? string.Empty);
            Pending.Enqueue(line);
            if (System.Threading.Interlocked.Exchange(ref flushScheduled, 1) == 0)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(_ => Flush());
            }
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is NotSupportedException)
        {
            Trace.TraceWarning(
                "FrontEndCSharp browser-composition trace failed: {0}",
                exception.GetType().Name);
        }
    }

    private static void Flush()
    {
        try
        {
            var lines = new StringBuilder();
            while (Pending.TryDequeue(out var line))
            {
                lines.AppendLine(line);
            }
            if (lines.Length > 0)
            {
                var directory = Path.Combine(Path.GetTempPath(), "FrontEndCSharp");
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    Path.Combine(directory, "browser-composition.log"),
                    lines.ToString(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is NotSupportedException)
        {
            Trace.TraceWarning(
                "FrontEndCSharp browser-composition trace failed: {0}",
                exception.GetType().Name);
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref flushScheduled, 0);
            if (!Pending.IsEmpty
                && System.Threading.Interlocked.Exchange(ref flushScheduled, 1) == 0)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(_ => Flush());
            }
        }
    }
}
