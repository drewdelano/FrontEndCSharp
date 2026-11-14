using System.Text.Json;

namespace FrontEndCSharp.Compiler.Integration;

internal static class InterpreterPerformance
{
    public static void Run()
    {
        var scenarioPath = Path.Combine(
            RepositoryLayout.Root,
            "Compiler",
            "Compiler.Tests",
            "JsRuntime",
            "interpreter-performance.mjs");
        var result = BoundedProcess.Run(
            "node",
            RepositoryLayout.Root,
            TimeSpan.FromSeconds(30),
            "--expose-gc",
            scenarioPath);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Require(root.GetProperty("exposeGc").GetBoolean(),
            "The interpreter benchmark did not run with an exposed garbage collector.");
        Require(root.GetProperty("iterationCount").GetInt32() == 20_000,
            "The interpreter benchmark changed its iteration count.");
        var measurements = root.GetProperty("results").EnumerateArray().ToArray();
        Require(measurements.Length == 2,
            "The interpreter benchmark must report disabled and idle-debugger modes.");
        var expectedModes = new[] { "debugger-disabled", "debugger-enabled-idle" };
        for (var index = 0; index < measurements.Length; index++)
        {
            var measurement = measurements[index];
            Require(
                measurement.GetProperty("mode").GetString() == expectedModes[index],
                $"Interpreter benchmark mode {index} is not '{expectedModes[index]}'.");
            Require(measurement.GetProperty("samples").GetInt32() == 7,
                $"Interpreter benchmark mode '{expectedModes[index]}' did not report seven samples.");
            Require(measurement.GetProperty("instructionsPerSample").GetInt32() == 220_007,
                $"Interpreter benchmark mode '{expectedModes[index]}' changed its instruction count.");
            Require(measurement.GetProperty("medianMilliseconds").GetDouble() > 0,
                $"Interpreter benchmark mode '{expectedModes[index]}' reported no elapsed time.");
        }
        Console.WriteLine(result.StandardOutput.TrimEnd());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
