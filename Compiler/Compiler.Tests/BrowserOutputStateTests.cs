using FrontEndCSharp.Packaging;

namespace Compiler.Tests;

public sealed class BrowserOutputStateTests
{
    [Test]
    public void FingerprintRequiresUnchangedInputsConfigurationAndPublishedOutputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "FrontEndCSharp-State-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var input = Path.Combine(root, "input.dll");
            var configuration = Path.Combine(root, "configuration");
            var inputs = Path.Combine(root, "inputs");
            var output = Path.Combine(root, "index.js");
            var outputs = Path.Combine(root, "outputs");
            var state = Path.Combine(root, "state");
            var candidate = Path.Combine(root, "candidate");
            File.WriteAllText(input, "one");
            File.WriteAllText(configuration, "Debug");
            File.WriteAllText(inputs, input);
            File.WriteAllText(outputs, output);

            Assert.False(BrowserOutputState.Check(inputs, configuration, outputs, state, candidate));
            File.WriteAllText(output, "published");
            BrowserOutputState.Commit(candidate, outputs, state);
            Assert.True(BrowserOutputState.Check(inputs, configuration, outputs, state, candidate));

            File.WriteAllText(input, "two");
            Assert.False(BrowserOutputState.Check(inputs, configuration, outputs, state, candidate));
            File.WriteAllText(input, "one");
            File.WriteAllText(configuration, "Release");
            Assert.False(BrowserOutputState.Check(inputs, configuration, outputs, state, candidate));
            File.WriteAllText(configuration, "Debug");
            File.Delete(output);
            Assert.False(BrowserOutputState.Check(inputs, configuration, outputs, state, candidate));
            var committedState = File.ReadAllText(state);
            Assert.Throws<FileNotFoundException>(() =>
                BrowserOutputState.Commit(candidate, outputs, state));
            Assert.Equal(committedState, File.ReadAllText(state));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
