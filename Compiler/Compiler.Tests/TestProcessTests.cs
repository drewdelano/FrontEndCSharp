using System.Diagnostics;

namespace Compiler.Tests;

public sealed class TestProcessTests
{
    [Test]
    public void DrainsPipesWhenTheThreadPoolIsOccupied()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var fixture = Path.Combine(RuntimeOverrideToolingTests.RepositoryRoot, "Compiler", "Compiler.Tests",
            "Fixtures", "ProcessRunnerFixture", "bin", configuration, "net8.0", "ProcessRunnerFixture.dll");
        var (output, error) = TestProcess.Run("dotnet", AppContext.BaseDirectory, TimeSpan.FromSeconds(10), fixture);
        Assert.Contains("Both pipes drained", output);
        Assert.Empty(error);
    }

    [Test]
    public void DrainsBothPipesBeforeWaitingForExit()
    {
        var (output, error) = TestProcess.Run("node", AppContext.BaseDirectory, TimeSpan.FromSeconds(10),
            "--eval", "const fs = require('node:fs'); fs.writeFileSync(1, 'o'.repeat(262144)); fs.writeFileSync(2, 'e'.repeat(262144));");
        Assert.Equal(new string('o', 262144), output);
        Assert.Equal(new string('e', 262144), error);
    }

    [Test]
    public void ReportsExitCodeAndBothDiagnosticStreams()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TestProcess.Run(
            "node", AppContext.BaseDirectory, TimeSpan.FromSeconds(10),
            "--eval", "console.log('build phase'); console.error('compiler failure'); process.exitCode = 47;"));
        Assert.Contains("code 47", exception.Message);
        Assert.Contains("build phase", exception.Message);
        Assert.Contains("compiler failure", exception.Message);
    }

    [Test]
    public void TimeoutTerminatesOwnedChildProcess()
    {
        var pidFile = Path.Combine(Path.GetTempPath(), "FrontEndCSharp-Process-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Throws<TimeoutException>(() => TestProcess.Run(
                "node", AppContext.BaseDirectory, TimeSpan.FromSeconds(2), "--eval",
                "const child = require('node:child_process').spawn(process.execPath, ['--eval', 'setInterval(() => {}, 1000)']);"
                + "require('node:fs').writeFileSync(process.argv[1], String(child.pid)); setInterval(() => {}, 1000);",
                pidFile));
            Assert.True(File.Exists(pidFile), "The fixture must spawn a child before the timeout.");
            var childId = int.Parse(File.ReadAllText(pidFile));
            Process? child;
            try { child = Process.GetProcessById(childId); }
            catch (ArgumentException) { return; }
            using (child) { Assert.True(child.WaitForExit(5_000), "The owned child survived timeout cleanup."); }
        }
        finally { File.Delete(pidFile); }
    }
}
