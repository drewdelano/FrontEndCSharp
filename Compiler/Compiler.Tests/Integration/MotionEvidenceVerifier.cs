using System.Text.Json.Serialization;

namespace FrontEndCSharp.Compiler.Integration;

public sealed class MotionValue
{
    [JsonPropertyName("time")]
    public double Time { get; init; }

    [JsonPropertyName("opacity")]
    public double Opacity { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }
}

public sealed class MotionSample
{
    [JsonPropertyName("phase")]
    public string Phase { get; init; } = string.Empty;

    [JsonPropertyName("focus")]
    public MotionValue? Focus { get; init; }

    [JsonPropertyName("task")]
    public MotionValue? Task { get; init; }
}

public sealed class CssOrbitAnimation
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("duration")]
    public string Duration { get; init; } = string.Empty;

    [JsonPropertyName("iterationCount")]
    public string IterationCount { get; init; } = string.Empty;
}

public sealed class MotionEvidence
{
    [JsonPropertyName("samples")]
    public IReadOnlyList<MotionSample> Samples { get; init; } = Array.Empty<MotionSample>();

    [JsonPropertyName("prefersReducedMotion")]
    public bool PrefersReducedMotion { get; init; }

    [JsonPropertyName("cssOrbitAnimation")]
    public CssOrbitAnimation? CssOrbitAnimation { get; init; }
}

public sealed class MotionVerification
{
    public required MotionValue Initial { get; init; }
    public required MotionValue InFlight { get; init; }
    public required MotionValue Settled { get; init; }
    public required MotionValue HoverFlight { get; init; }
    public required MotionValue HoverSettled { get; init; }
    public bool PrefersReducedMotion { get; init; }
    public CssOrbitAnimation? CssOrbitAnimation { get; init; }
}

public static class MotionEvidenceVerifier
{
    public static MotionVerification Verify(MotionEvidence evidence)
    {
        var focus = evidence.Samples
            .Select(sample => sample.Focus)
            .Where(value => value != null)
            .Cast<MotionValue>()
            .ToArray();
        var initial = focus.FirstOrDefault(value =>
            value.Opacity < 0.01 && Math.Abs(value.Y - 18) < 0.1)
            ?? throw new InvalidDataException(
                "Motion focus card never exposed its initial opacity/translation.");
        var inFlight = focus.FirstOrDefault(value =>
            value.Time > initial.Time
            && value.Opacity > 0.02
            && value.Opacity < 0.98
            && value.Y > 0.1
            && value.Y < 17.9)
            ?? throw new InvalidDataException(
                "Motion focus card never exposed an in-flight computed transition.");
        var settled = focus.FirstOrDefault(value =>
            value.Time > inFlight.Time
            && value.Opacity > 0.999
            && Math.Abs(value.Y) < 0.05)
            ?? throw new InvalidDataException(
                "Motion focus card did not settle at the requested computed values.");
        var hover = evidence.Samples
            .Where(sample => sample.Phase == "hover")
            .Select(sample => sample.Task)
            .Where(value => value != null)
            .Cast<MotionValue>()
            .ToArray();
        var hoverFlight = hover.FirstOrDefault(value => value.Y < -0.1 && value.Y > -3.9)
            ?? throw new InvalidDataException(
                "Motion task card never exposed an in-flight hover translation.");
        var hoverSettled = hover.FirstOrDefault(value =>
            value.Time > hoverFlight.Time && Math.Abs(value.Y + 4) < 0.1)
            ?? throw new InvalidDataException(
                "Motion task card did not settle at its requested hover translation.");
        return new MotionVerification
        {
            Initial = initial,
            InFlight = inFlight,
            Settled = settled,
            HoverFlight = hoverFlight,
            HoverSettled = hoverSettled,
            PrefersReducedMotion = evidence.PrefersReducedMotion,
            CssOrbitAnimation = evidence.CssOrbitAnimation,
        };
    }
}
