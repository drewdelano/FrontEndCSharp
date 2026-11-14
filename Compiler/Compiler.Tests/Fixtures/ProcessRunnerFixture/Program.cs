using Compiler.Tests;

// Isolate pool limits from the test host and all other parallel tests.
if (!ThreadPool.SetMinThreads(1, 1) || !ThreadPool.SetMaxThreads(1, 1))
{
    throw new InvalidOperationException("Could not constrain the fixture thread pool.");
}
using var occupied = new ManualResetEventSlim();
using var release = new ManualResetEventSlim();
using var released = new ManualResetEventSlim();
ThreadPool.QueueUserWorkItem(_ =>
{
    occupied.Set();
    release.Wait();
    released.Set();
});
try
{
    if (!occupied.Wait(TimeSpan.FromSeconds(5)))
    {
        throw new TimeoutException("The fixture worker did not start.");
    }
    var (output, error) = TestProcess.Run("node", AppContext.BaseDirectory, TimeSpan.FromSeconds(5),
        "--eval", "const fs = require('node:fs'); fs.writeFileSync(1, 'o'.repeat(262144)); fs.writeFileSync(2, 'e'.repeat(262144));");
    if (output != new string('o', 262144) || error != new string('e', 262144))
    {
        throw new InvalidOperationException("Output was lost while the pool was occupied.");
    }
    Console.WriteLine("Both pipes drained with the only pool worker occupied.");
}
finally
{
    release.Set();
    if (!released.Wait(TimeSpan.FromSeconds(5)))
    {
        throw new TimeoutException("The fixture worker did not stop.");
    }
}
