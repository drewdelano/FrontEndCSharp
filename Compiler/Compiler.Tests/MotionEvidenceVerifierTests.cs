using FrontEndCSharp.Compiler.Integration;

namespace Compiler.Tests;

public sealed class MotionEvidenceVerifierTests
{
    [Test]
    public void RequiresChronologicalComputedEntranceAndHoverValues()
    {
        var result = MotionEvidenceVerifier.Verify(CreateEvidence());

        Assert.True(result.PrefersReducedMotion);
        Assert.Equal(-4, result.HoverSettled.Y);
    }

    [Test]
    public void RejectsFinalStylesWithoutTransition()
    {
        var evidence = CreateEvidence();
        evidence = new MotionEvidence
        {
            PrefersReducedMotion = evidence.PrefersReducedMotion,
            CssOrbitAnimation = evidence.CssOrbitAnimation,
            Samples = new[] { Sample(600, 1, 0) },
        };

        var exception = Assert.Throws<InvalidDataException>(() =>
            MotionEvidenceVerifier.Verify(evidence));
        Assert.Contains("initial", exception.Message);
    }

    [Test]
    public void RejectsInstantaneousAndAbsentHoverMotion()
    {
        var evidence = CreateEvidence();
        var instantaneous = new MotionEvidence
        {
            Samples = evidence.Samples.Where((_, index) => index != 1).ToArray(),
        };
        var inFlightException = Assert.Throws<InvalidDataException>(() =>
            MotionEvidenceVerifier.Verify(instantaneous));
        Assert.Contains("in-flight", inFlightException.Message);

        var withoutHover = new MotionEvidence
        {
            Samples = evidence.Samples.Where(sample => sample.Phase != "hover").ToArray(),
        };
        var hoverException = Assert.Throws<InvalidDataException>(() =>
            MotionEvidenceVerifier.Verify(withoutHover));
        Assert.Contains("hover", hoverException.Message);
    }

    private static MotionEvidence CreateEvidence() => new()
    {
        PrefersReducedMotion = true,
        CssOrbitAnimation = new CssOrbitAnimation { Duration = "1e-05s" },
        Samples = new[]
        {
            Sample(0, 0, 18),
            Sample(100, 0.5, 9),
            Sample(600, 1, 0),
            Sample(700, 1, 0, "hover", -2),
            Sample(1000, 1, 0, "hover", -4),
        },
    };

    private static MotionSample Sample(
        double time,
        double opacity,
        double y,
        string phase = "entrance",
        double taskY = 0) => new()
    {
        Phase = phase,
        Focus = new MotionValue { Time = time, Opacity = opacity, Y = y },
        Task = new MotionValue { Time = time, Opacity = 1, Y = taskY },
    };
}
