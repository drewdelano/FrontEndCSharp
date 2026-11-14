using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Compiler.Performance;

internal sealed class CompilationPerformance : IDisposable
{
    private static CompilationPerformance current;
    private readonly string outputPath;
    private readonly Dictionary<string, PhaseMeasurement> phases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> metrics = new(StringComparer.Ordinal);
    private readonly DateTime startedUtc = DateTime.UtcNow;
    private bool disposed;

    private CompilationPerformance(string outputPath)
    {
        this.outputPath = Path.GetFullPath(outputPath);
        if (current != null)
        {
            throw new InvalidOperationException("A compiler performance session is already active.");
        }
        current = this;
    }

    internal static CompilationPerformance Start(string outputPath)
        => outputPath == null ? null : new CompilationPerformance(outputPath);

    internal static IDisposable Measure(string name)
        => current == null ? EmptyScope.Instance : new MeasurementScope(current, name);

    internal static T Measure<T>(string name, Func<T> action)
    {
        using (Measure(name))
        {
            return action();
        }
    }

    internal static void SetMetric(string name, long value)
    {
        if (current != null)
        {
            current.metrics[name] = value;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        current = null;
        var report = new PerformanceReport
        {
            FormatVersion = 1,
            StartedUtc = startedUtc,
            Phases = phases.Values.ToArray(),
            Metrics = metrics,
        };
        var directory = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonConvert.SerializeObject(report, Formatting.Indented));
            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private void Add(string name, long elapsedTicks, long allocatedBytes)
    {
        if (!phases.TryGetValue(name, out var phase))
        {
            phase = new PhaseMeasurement { Name = name };
            phases.Add(name, phase);
        }
        phase.InvocationCount++;
        phase.ElapsedMilliseconds += elapsedTicks * 1000d / Stopwatch.Frequency;
        phase.AllocatedBytes += allocatedBytes;
    }

    private sealed class MeasurementScope : IDisposable
    {
        private readonly CompilationPerformance owner;
        private readonly string name;
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly long allocated = GC.GetTotalAllocatedBytes(precise: false);

        public MeasurementScope(CompilationPerformance owner, string name)
        {
            this.owner = owner;
            this.name = name;
        }

        public void Dispose() => owner.Add(
            name,
            Stopwatch.GetTimestamp() - started,
            Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - allocated));
    }

    private sealed class EmptyScope : IDisposable
    {
        internal static readonly EmptyScope Instance = new();
        public void Dispose() { }
    }

    private sealed class PerformanceReport
    {
        public int FormatVersion { get; init; }
        public DateTime StartedUtc { get; init; }
        public IReadOnlyList<PhaseMeasurement> Phases { get; init; }
        public IReadOnlyDictionary<string, long> Metrics { get; init; }
    }

    private sealed class PhaseMeasurement
    {
        public string Name { get; init; }
        public int InvocationCount { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public long AllocatedBytes { get; set; }
    }
}
