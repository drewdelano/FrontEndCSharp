namespace Compiler.Tests;

public sealed class TaskDelayIntegrationTests : IntegrationTestBase
{
    [Test]
    public void TaskDelaySupportsTimeSpanZeroPositiveInfiniteAndBounds()
    {
        var output = CompileAndRun(
            """
            var zero = Task.Delay(TimeSpan.Zero);
            Console.WriteLine(zero.IsCompleted);
            await zero;

            await Task.Delay(TimeSpan.FromMilliseconds(1));
            Console.WriteLine("positive");

            var infinite = Task.Delay(Timeout.InfiniteTimeSpan);
            Console.WriteLine(infinite.IsCompleted);

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(-2));
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("negative");
            }

            try
            {
                await Task.Delay(TimeSpan.FromTicks(4294967295L * TimeSpan.TicksPerMillisecond));
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("too large");
            }
            """);

        Assert.Equal("True\npositive\nFalse\nnegative\ntoo large", output);
    }

    [Test]
    public void TaskDelaySupportsCancellationBeforeAndDuringDelay()
    {
        var output = CompileAndRun(
            """
            var preCanceledSource = new CancellationTokenSource();
            preCanceledSource.Cancel();
            var preCanceled = Task.Delay(1, preCanceledSource.Token);
            Console.WriteLine(preCanceled.IsCanceled);
            try
            {
                await preCanceled;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("pre-canceled");
            }

            var integerSource = new CancellationTokenSource();
            var integerDelay = Task.Delay(Timeout.Infinite, integerSource.Token);
            integerSource.Cancel();
            Console.WriteLine(integerDelay.IsCanceled);
            try
            {
                await integerDelay;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("integer canceled");
            }

            var timeSpanSource = new CancellationTokenSource();
            var timeSpanDelay = Task.Delay(TimeSpan.FromSeconds(1), timeSpanSource.Token);
            timeSpanSource.Cancel();
            Console.WriteLine(timeSpanDelay.IsCanceled);
            try
            {
                await timeSpanDelay;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("timespan canceled");
            }

            await Task.Delay(1, CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(1), CancellationToken.None);
            Console.WriteLine("uncanceled");
            """);

        Assert.Equal(
            "True\npre-canceled\nTrue\ninteger canceled\nTrue\ntimespan canceled\nuncanceled",
            output);
    }
}
