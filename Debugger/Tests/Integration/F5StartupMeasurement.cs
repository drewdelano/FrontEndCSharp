using System.Globalization;
using System.Text.Json;

namespace FrontEndCSharp.Debugger.Integration;

internal static class F5StartupMeasurement
{
    private static readonly string[] MilestoneNames =
    [
        "start-command-observed",
        "standard-f5-build-complete",
        "aspnet-host-listening",
        "browser-navigation-requested",
        "runtime-context-selection-started",
        "runtime-context-selection-complete",
        "runtime-configuration-started",
        "runtime-configuration-complete",
    ];

    public static void Run(IReadOnlyList<string> arguments)
    {
        var options = MeasurementOptions.Parse(arguments);
        if (!File.Exists(options.TracePath))
        {
            throw new FileNotFoundException("The F5 trace does not exist.", options.TracePath);
        }
        var events = File.ReadLines(options.TracePath)
            .Select(ParseEvent)
            .Where(item => item != null)
            .Cast<TraceEvent>()
            .ToArray();
        var start = events.LastOrDefault(item => item.Name == "start-command-observed")
            ?? throw new InvalidDataException(
                "The trace has no standard F5 start marker. Run F5 once and retry.");
        var runEvents = events.Where(item => item.Timestamp >= start.Timestamp).ToArray();
        var milestones = MilestoneNames
            .Select(name => FindMilestone(runEvents, start, name))
            .Where(item => item != null)
            .Cast<StartupMilestone>()
            .Concat(runEvents
                .Where(item => item.Name == "proxy-launch-delegate-complete")
                .Select(item => CreateMilestone(
                    "js-debug-launch-complete-" + item.Detail,
                    item,
                    start)))
            .OrderBy(item => item.Timestamp)
            .ToArray();
        var configuration = milestones.FirstOrDefault(
            item => item.Event == "runtime-configuration-complete");
        var build = milestones.FirstOrDefault(
            item => item.Event == "standard-f5-build-complete");
        double? elapsed = configuration != null && build != null
            ? Math.Round((configuration.Timestamp - build.Timestamp).TotalMilliseconds, 1)
            : null;
        var result = new StartupMeasurementResult
        {
            RunKind = options.RunKind,
            MeasuredAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            BuildToManagedConfigurationMilliseconds = elapsed,
            BrowserRefreshInjectionFailures = runEvents.Count(item =>
                item.Name == "proxy-transform"
                && item.Detail.StartsWith(
                    "browser-refresh-injection-failed",
                    StringComparison.Ordinal)),
            OrdinaryCoreClrModuleLoadLines = CountCoreClrModuleLoads(
                options.VisualStudioOutputPath),
            Milestones = milestones,
        };
        var indentedOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        if (options.BaselinePath != null)
        {
            var baselinePath = Path.GetFullPath(options.BaselinePath);
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.AppendAllText(
                baselinePath,
                JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }) + Environment.NewLine);
        }
        Console.WriteLine(JsonSerializer.Serialize(result, indentedOptions));
        if (configuration == null || build == null)
        {
            throw new InvalidDataException(
                "The F5 trace is incomplete; build completion and managed runtime configuration are required.");
        }
        if (options.AcceptanceMilliseconds > 0
            && elapsed > options.AcceptanceMilliseconds)
        {
            throw new InvalidDataException(
                $"F5 startup took {elapsed} ms from build completion to managed configuration; "
                + $"acceptance limit is {options.AcceptanceMilliseconds} ms.");
        }
    }

    private static TraceEvent? ParseEvent(string line)
    {
        var parts = line.Split('\t', 3);
        if (parts.Length < 2
            || !DateTimeOffset.TryParse(
                parts[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp))
        {
            return null;
        }
        return new TraceEvent
        {
            Timestamp = timestamp,
            Name = parts[1],
            Detail = parts.Length == 3 ? parts[2] : string.Empty,
        };
    }

    private static StartupMilestone? FindMilestone(
        IReadOnlyList<TraceEvent> events,
        TraceEvent start,
        string name)
    {
        var match = events.FirstOrDefault(item =>
            item.Name == name
            || (item.Name == "proxy-transform" && item.Detail == name));
        return match == null ? null : CreateMilestone(name, match, start);
    }

    private static StartupMilestone CreateMilestone(
        string name,
        TraceEvent item,
        TraceEvent start) => new()
        {
            Event = name,
            Timestamp = item.Timestamp,
            SinceStartMilliseconds = Math.Round(
                (item.Timestamp - start.Timestamp).TotalMilliseconds,
                1),
            Detail = item.Detail,
        };

    private static int CountCoreClrModuleLoads(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return 0;
        }
        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException(
                "The Visual Studio output capture does not exist.",
                outputPath);
        }
        return File.ReadLines(outputPath).Count(line =>
            line.Contains("Loaded", StringComparison.Ordinal)
            && line.Contains(".dll", StringComparison.OrdinalIgnoreCase)
            && (line.Contains("System.", StringComparison.Ordinal)
                || line.Contains("Microsoft.", StringComparison.Ordinal)));
    }

    private sealed class MeasurementOptions
    {
        public required string TracePath { get; init; }
        public required string RunKind { get; init; }
        public string? BaselinePath { get; init; }
        public int AcceptanceMilliseconds { get; init; }
        public string? VisualStudioOutputPath { get; init; }

        public static MeasurementOptions Parse(IReadOnlyList<string> arguments)
        {
            if (arguments.Count < 2 || arguments[1] is not ("cold" or "warm"))
            {
                throw new ArgumentException(
                    "measure-f5-startup requires <trace-path> <cold|warm> "
                    + "[--baseline <path>] [--acceptance-ms <milliseconds>] "
                    + "[--vs-output <path>].");
            }
            string? baseline = null;
            string? visualStudioOutput = null;
            var acceptance = 0;
            for (var index = 2; index < arguments.Count; index += 2)
            {
                if (index + 1 >= arguments.Count)
                {
                    throw new ArgumentException($"Option '{arguments[index]}' requires a value.");
                }
                switch (arguments[index])
                {
                    case "--baseline":
                        baseline = arguments[index + 1];
                        break;
                    case "--acceptance-ms" when int.TryParse(
                        arguments[index + 1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out acceptance):
                        break;
                    case "--vs-output":
                        visualStudioOutput = arguments[index + 1];
                        break;
                    default:
                        throw new ArgumentException(
                            $"Unknown or invalid option '{arguments[index]}'.");
                }
            }
            return new MeasurementOptions
            {
                TracePath = Path.GetFullPath(arguments[0]),
                RunKind = arguments[1],
                BaselinePath = baseline,
                AcceptanceMilliseconds = acceptance,
                VisualStudioOutputPath = visualStudioOutput,
            };
        }
    }

    private sealed class TraceEvent
    {
        public DateTimeOffset Timestamp { get; init; }
        public required string Name { get; init; }
        public required string Detail { get; init; }
    }

    private sealed class StartupMeasurementResult
    {
        public required string RunKind { get; init; }
        public required string MeasuredAtUtc { get; init; }
        public double? BuildToManagedConfigurationMilliseconds { get; init; }
        public int BrowserRefreshInjectionFailures { get; init; }
        public int OrdinaryCoreClrModuleLoadLines { get; init; }
        public required IReadOnlyList<StartupMilestone> Milestones { get; init; }
    }

    private sealed class StartupMilestone
    {
        public required string Event { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public double SinceStartMilliseconds { get; init; }
        public required string Detail { get; init; }
    }
}
