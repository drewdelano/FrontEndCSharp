using System.Globalization;

namespace Compiler.Tests;

public sealed class CurrentTimeIntegrationTests : IntegrationTestBase
{
    [Test]
    public void CurrentDateAndTimePropertiesTrackTheHostClock()
    {
        var startedUtc = DateTime.UtcNow;
        var output = CompileAndRun(
            """
            using System.Globalization;

            var localNow = DateTime.Now;
            var today = DateTime.Today;
            var utcNow = DateTime.UtcNow;
            var offsetNow = DateTimeOffset.Now;
            var offsetUtcNow = DateTimeOffset.UtcNow;
            Console.WriteLine(localNow.ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine((int)localNow.Kind);
            Console.WriteLine(today.ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine((int)today.Kind);
            Console.WriteLine(utcNow.ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine((int)utcNow.Kind);
            Console.WriteLine(offsetNow.ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine(offsetUtcNow.ToString("O", CultureInfo.InvariantCulture));
            """);
        var finishedUtc = DateTime.UtcNow;

        var lines = output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var localNow = DateTime.ParseExact(
            lines[0], "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var today = DateTime.ParseExact(
            lines[2], "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var utcNow = DateTime.ParseExact(
            lines[4], "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var offsetNow = DateTimeOffset.ParseExact(lines[6], "O", CultureInfo.InvariantCulture);
        var offsetUtcNow = DateTimeOffset.ParseExact(lines[7], "O", CultureInfo.InvariantCulture);

        Assert.Equal("2", lines[1]);
        Assert.Equal("2", lines[3]);
        Assert.Equal("1", lines[5]);
        Assert.True(
            today == startedUtc.ToLocalTime().Date || today == finishedUtc.ToLocalTime().Date,
            $"Today '{today:O}' was outside the observed local-date window.");
        Assert.Equal(TimeSpan.Zero, today.TimeOfDay);
        Assert.InRange(localNow.ToUniversalTime(), startedUtc, finishedUtc);
        Assert.InRange(utcNow, startedUtc, finishedUtc);
        Assert.InRange(offsetNow.UtcDateTime, startedUtc, finishedUtc);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(offsetNow.UtcDateTime), offsetNow.Offset);
        Assert.InRange(offsetUtcNow.UtcDateTime, startedUtc, finishedUtc);
        Assert.Equal(TimeSpan.Zero, offsetUtcNow.Offset);
    }
}
