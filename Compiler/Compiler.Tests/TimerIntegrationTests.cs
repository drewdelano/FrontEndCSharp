namespace Compiler.Tests;

public sealed class TimerIntegrationTests : IntegrationTestBase
{
    [Test]
    public void TimerSupportsOneShotPeriodicChangeStateAndDispose()
    {
        var output = CompileAndRun(
            """
            var state = new TimerState();
            var timer = new Timer(Callback, state, 1, Timeout.Infinite);
            state.Timer = timer;
            for (var attempt = 0; attempt < 10 && state.Count < 3; attempt++)
            {
                await Task.Delay(10);
            }
            Console.WriteLine(state.StateMatched);
            Console.WriteLine(state.Count);
            var countAtDispose = state.Count;
            await Task.Delay(10);
            Console.WriteLine(state.Count == countAtDispose);

            try
            {
                timer.Change(1, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("disposed change");
            }

            static void Callback(object? value)
            {
                var state = (TimerState)value!;
                state.StateMatched = true;
                state.Count++;
                if (state.Count == 1)
                {
                    state.Timer!.Change(1, 2);
                }
                else if (state.Count == 3)
                {
                    state.Timer!.Dispose();
                }
            }

            sealed class TimerState
            {
                public int Count;
                public bool StateMatched;
                public Timer? Timer;
            }
            """);

        Assert.Equal("True\n3\nTrue\ndisposed change", output);
    }

    [Test]
    public void TimerValidatesCallbacksTimeoutsAndDisposedChanges()
    {
        var output = CompileAndRun(
            """
            static void Callback(object? _) { }

            try
            {
                _ = new Timer(null!, null, 0, Timeout.Infinite);
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine("null callback");
            }

            try
            {
                _ = new Timer(Callback, null, -2, Timeout.Infinite);
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("invalid constructor timeout");
            }

            var timer = new Timer(Callback, null, Timeout.Infinite, Timeout.Infinite);
            try
            {
                timer.Change(0, -2);
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("invalid change timeout");
            }

            timer.Dispose();
            timer.Dispose();
            try
            {
                timer.Change(0, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("disposed change");
            }
            """);

        Assert.Equal(
            "null callback\ninvalid constructor timeout\ninvalid change timeout\ndisposed change",
            output);
    }
}
