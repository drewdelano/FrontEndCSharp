using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.Unicode;
using System.Text;
using System.Reflection;

namespace Compiler.Tests;

public class IntegrationTestBase
{
    public static string CompileAndRunStatic(string csharpCode)
    {
        return new IntegrationTestBase().CompileAndRun(csharpCode);
    }

    protected string CompileAndRun(string csharpCode, string configuration = "Debug")
    {
        var testIdentity = configuration == "Debug"
            ? $"typed-runtime-override-dependencies-v3\0{csharpCode}"
            : $"typed-runtime-override-dependencies-v3\0{configuration}\0{csharpCode}";
        var sha1 = string.Join("", SHA1.HashData(Encoding.UTF8.GetBytes(testIdentity)).Select(b => b.ToString("X2")));

        var testOutputDir = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "TestOutput");
        if (!Directory.Exists(testOutputDir))
        {
            Directory.CreateDirectory(testOutputDir);
        }

        var tempDir = Path.Combine(testOutputDir, "FrontEndCSharp_Test_" + sha1);
        var outputDir = Path.Combine(tempDir, $"js_output_{Guid.NewGuid():N}");
        var projectPath = Path.Combine(tempDir, "TestProject.csproj");
        var targetAssemblyPath = Path.Combine(
            tempDir,
            "bin",
            configuration,
            "net8.0",
            "TestProject.dll");
        var consoleOverridesPath = Path.Combine(AppContext.BaseDirectory, "FrontEndCSharp.Overrides.System.Console.fecs.dll");
        var coreOverridesPath = Path.Combine(AppContext.BaseDirectory, "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.dll");
        var cryptographyOverridesPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Security.Cryptography.fecs.dll");
        var expressionOverridesPath = Path.Combine(AppContext.BaseDirectory, "FrontEndCSharp.Overrides.System.Linq.Expressions.fecs.dll");
        var validationOverridesPath = Path.Combine(AppContext.BaseDirectory, "FrontEndCSharp.Overrides.System.ComponentModel.Annotations.fecs.dll");

        if (!File.Exists(targetAssemblyPath))
        {
            Directory.CreateDirectory(tempDir);
            // Create a simple C# project
            var escapedConsoleOverridesPath = System.Security.SecurityElement.Escape(
                consoleOverridesPath);
            var escapedCoreOverridesPath = System.Security.SecurityElement.Escape(
                coreOverridesPath);
            var escapedCryptographyOverridesPath = System.Security.SecurityElement.Escape(
                cryptographyOverridesPath);
            var escapedExpressionOverridesPath = System.Security.SecurityElement.Escape(
                expressionOverridesPath);
            var escapedValidationOverridesPath = System.Security.SecurityElement.Escape(
                validationOverridesPath);
            File.WriteAllText(projectPath, $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""FrontEndCSharp.Overrides.System.Console.fecs"">
      <HintPath>{escapedConsoleOverridesPath}</HintPath>
      <Private>true</Private>
    </Reference>
    <Reference Include=""FrontEndCSharp.Overrides.System.Private.CoreLib.fecs"">
      <HintPath>{escapedCoreOverridesPath}</HintPath>
      <Private>true</Private>
    </Reference>
    <Reference Include=""FrontEndCSharp.Overrides.System.Security.Cryptography.fecs"">
      <HintPath>{escapedCryptographyOverridesPath}</HintPath>
      <Private>true</Private>
    </Reference>
    <Reference Include=""FrontEndCSharp.Overrides.System.Linq.Expressions.fecs"">
      <HintPath>{escapedExpressionOverridesPath}</HintPath>
      <Private>true</Private>
    </Reference>
    <Reference Include=""FrontEndCSharp.Overrides.System.ComponentModel.Annotations.fecs"">
      <HintPath>{escapedValidationOverridesPath}</HintPath>
      <Private>true</Private>
    </Reference>
  </ItemGroup>
</Project>

".TrimStart('\n', '\r'));

            // Write the C# code
            var programPath = Path.Combine(tempDir, "Program.cs");
            File.WriteAllText(programPath, csharpCode);

            // Build the C# project
            // DO NOT increase this value!  The tests are run against very small programs that should finish in under 1 second.
            // 10 seconds is a compromise to make sure we should never time out in legitimate scenarios.
            TestProcess.Run("dotnet", tempDir, TimeSpan.FromSeconds(10),
                "build", projectPath, "--configuration", configuration);
        }

        var targetAssemblyDirectory = Path.GetDirectoryName(targetAssemblyPath)!;
        File.Copy(consoleOverridesPath, Path.Combine(targetAssemblyDirectory, "FrontEndCSharp.Overrides.System.Console.fecs.dll"), true);
        File.Copy(coreOverridesPath, Path.Combine(targetAssemblyDirectory, "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.dll"), true);
        File.Copy(
            cryptographyOverridesPath,
            Path.Combine(targetAssemblyDirectory, "FrontEndCSharp.Overrides.System.Security.Cryptography.fecs.dll"),
            true);
        File.Copy(expressionOverridesPath, Path.Combine(targetAssemblyDirectory, "FrontEndCSharp.Overrides.System.Linq.Expressions.fecs.dll"), true);
        File.Copy(validationOverridesPath, Path.Combine(targetAssemblyDirectory, "FrontEndCSharp.Overrides.System.ComponentModel.Annotations.fecs.dll"), true);

        Directory.CreateDirectory(outputDir);
        try
        {
            // A process provides a killable compiler bound and keeps diagnostics local
            // to this test. Five seconds is the slow bar and the repository-wide
            // NUnit policy fails the case at ten seconds.
            TestProcess.Run("dotnet", tempDir, TimeSpan.FromSeconds(10),
                typeof(Compiler.Program).Assembly.Location, "--no-bundle",
                projectPath, outputDir, targetAssemblyPath);

            // Run the generated JavaScript with Node.js
            var indexPath = Path.Combine(outputDir, "index.js");
            // DO NOT increase this value!  The tests are run against very small programs that should run in under 1 second.
            // 10 seconds is a compromise to make sure we should never time out in legitimate scenarios.
            var (output, errors) = TestProcess.Run("node", outputDir, TimeSpan.FromSeconds(10), indexPath);

            // Treat any stderr output (including warnings) as an error
            if (!string.IsNullOrEmpty(errors.Trim()) || string.IsNullOrEmpty(output.Trim()))
            {
                throw new Exception($"JavaScript runtime error:\nSTDERR: {errors}\nSTDOUT: {output}");
            }

            return output.Trim();
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    protected static string CompileAndRunBuiltProject(
        string projectPath,
        string assemblyPath,
        TimeSpan? compilationTimeout = null)
    {
        var outputDir = Path.Combine(
            Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!,
            "TestOutput",
            "AssemblyCollision");
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }
        Directory.CreateDirectory(outputDir);

        TestProcess.Run("dotnet", outputDir, compilationTimeout ?? TimeSpan.FromSeconds(10),
            typeof(Compiler.Program).Assembly.Location, "--no-bundle",
            projectPath, outputDir, assemblyPath);
        var (output, error) = TestProcess.Run("node", outputDir, TimeSpan.FromSeconds(10),
            Path.Combine(outputDir, "index.js"));
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new Exception($"Assembly-collision fixture failed.\n{error}");
        }
        return output.Trim();
    }

    protected static string GetSourcePath(
        [System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        sourcePath;
}

public sealed class BasicIntegrationTests : IntegrationTestBase
{
    [Test]
    public void DuplicateNamespaceQualifiedTypeNamesFailCompilation()
    {
        var sourcePath = GetSourcePath();
        var projectPath = Path.Combine(
            Path.GetDirectoryName(sourcePath)!,
            "Fixtures",
            "AssemblyCollision",
            "Entry",
            "AssemblyCollision.Entry.csproj");
        var assemblyPath = Path.Combine(
            Path.GetDirectoryName(projectPath)!,
            "bin",
            "Debug",
            "net8.0",
            "AssemblyCollision.Entry.dll");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CompileAndRunBuiltProject(projectPath, assemblyPath));

        Assert.Contains("Managed type name collision 'Shared.Model' is not supported", exception.Message);
        Assert.Contains("AssemblyCollision.First", exception.Message);
        Assert.Contains("AssemblyCollision.Second", exception.Message);
    }

    [Test]
    public void SimpleHelloWorld()
    {
        var csharp = @"
Console.WriteLine(""Hello, World!"");
";
        try
        {
            var output = CompileAndRun(csharp);
            Assert.Equal("Hello, World!", output);
        }
        catch (Exception ex) when (ex.Message.Contains("Generated code:"))
        {
            // Show the generated code for debugging
            throw;
        }
    }

    [Test]
    public void ManagedStringLengthAndIndexingPreserveIsolatedSurrogates()
    {
        var output = CompileAndRun(
            """
            var value = "\uD800A\uDC00😀";
            Console.WriteLine(value.Length == 5);
            Console.WriteLine(value[0] == '\uD800');
            Console.WriteLine(value[1] == 'A');
            Console.WriteLine(value[2] == '\uDC00');
            Console.WriteLine(value[3] == '\uD83D');
            Console.WriteLine(value[4] == '\uDE00');
            try
            {
                Console.WriteLine(value[5]);
            }
            catch (IndexOutOfRangeException)
            {
                Console.WriteLine("IndexOutOfRangeException");
            }
            """);

        Assert.Equal("True\nTrue\nTrue\nTrue\nTrue\nTrue\nIndexOutOfRangeException", output);
    }

    [Test]
    public void SimpleVariableAndPrint()
    {
        var csharp = @"
var message = ""Test Message"";
Console.WriteLine(message);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Test Message", output);
    }

    [Test]
    public void MultipleLines()
    {
        var csharp = @"
Console.WriteLine(""Line 1"");
Console.WriteLine(""Line 2"");
Console.WriteLine(""Line 3"");
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Line 1\nLine 2\nLine 3", output);
    }

}

public sealed class AsyncIntegrationTests : IntegrationTestBase
{
    // Keep the four compiler-heavy async-iterator gates on NUnit's exclusive queue.
    // They remain below the ceiling in isolation but can cross it under CPU contention.
    [Test]
    [NonParallelizable]
    public void AsyncIteratorsYieldValuesInOrder()
    {
        var csharp = @"
using System.Collections.Generic;

static async IAsyncEnumerable<int> GetValues()
{
    yield return 1;
    await Task.Delay(1);
    yield return 2;
    await Task.Yield();
    yield return 3;
}

await foreach (var value in GetValues())
{
    Console.WriteLine(value);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("1\n2\n3", output);
    }

    [Test]
    [NonParallelizable]
    public void AsyncIteratorBreakRunsFinallyAndDisposesEarly()
    {
        var csharp = @"
using System.Collections.Generic;

var disposed = false;

async IAsyncEnumerable<int> GetValues()
{
    try
    {
        yield return 1;
        await Task.Delay(1);
        yield return 2;
    }
    finally
    {
        disposed = true;
        Console.WriteLine(""finally"");
    }
}

await foreach (var value in GetValues())
{
    Console.WriteLine(value);
    break;
}

Console.WriteLine(disposed);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("1\nfinally\nTrue", output);
    }

    [Test]
    [NonParallelizable]
    public void AsyncIteratorExceptionsReachAwaitForeachConsumer()
    {
        var csharp = @"
using System.Collections.Generic;

static async IAsyncEnumerable<int> GetValues()
{
    yield return 1;
    await Task.Delay(1);
    throw new InvalidOperationException(""iterator boom"");
}

try
{
    await foreach (var value in GetValues())
    {
        Console.WriteLine(value);
    }
}
catch (InvalidOperationException exception)
{
    Console.WriteLine(exception.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("1\niterator boom", output);
    }

    [Test]
    [NonParallelizable]
    public void AsyncIteratorEnumeratorCancellationTokenFlowsWithoutLinking()
    {
        var csharp = @"
using System.Collections.Generic;
using System.Runtime.CompilerServices;

static async IAsyncEnumerable<int> GetValues(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await Task.Yield();
    yield return cancellationToken.IsCancellationRequested ? 1 : 0;
}

var cancellationToken = new CancellationToken(canceled: true);
var enumerator = GetValues().GetAsyncEnumerator(cancellationToken);
try
{
    if (await enumerator.MoveNextAsync())
    {
        Console.WriteLine(enumerator.Current == 1);
    }
}
finally
{
    await enumerator.DisposeAsync();
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("True", output);
    }

    [Test]
    public void AsyncTaskDelayResumesStateMachine()
    {
        var csharp = @"
Console.WriteLine(""Before"");
Console.WriteLine(args.Length);
await Task.Delay(1);
Console.WriteLine(""After"");
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Before\n0\nAfter", output);
    }

    [Test]
    public void OptimizedAsyncStateMachinesResumeWithoutSetStateMachineAdaptation()
    {
        var csharp = @"
static async Task<int> CompleteAsync()
{
    await Task.Delay(1);
    await Task.Yield();
    return 42;
}

Console.WriteLine(await CompleteAsync());
";
        var output = CompileAndRun(csharp, "Release");
        Assert.Equal("42", output);
    }

    [Test]
    public void AsyncTaskDelayRejectsInvalidMilliseconds()
    {
        var csharp = @"
try
{
    await Task.Delay(-2);
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine(""invalid delay"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("invalid delay", output);
    }

    [Test]
    public void AsyncTaskDelayZeroCompletesSynchronously()
    {
        var csharp = @"
var delay = Task.Delay(0);
Console.WriteLine(delay.GetAwaiter().IsCompleted);
await delay;
Console.WriteLine(""complete"");
";
        var output = CompileAndRun(csharp);
        Assert.Equal("True\ncomplete", output);
    }

    [Test]
    public void ManagedFunctionPointerInvokesCompilerResolvedMethod()
    {
        var csharp = @"
static int Double(int value) => value * 2;
static void WriteSum(int left, int right) => Console.WriteLine(left + right);

unsafe
{
    delegate* managed<int, int> function = &Double;
    Console.WriteLine(function(21));

    delegate* managed<int, int, void> action = &WriteSum;
    action(2, 3);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("42\n5", output.Replace("\r\n", "\n"));
    }

    [Test]
    public void UnmanagedFunctionPointerFailsCompilationClosed()
    {
        var csharp = @"
using System.Runtime.InteropServices;

[UnmanagedCallersOnly]
static int Double(int value) => value * 2;

unsafe
{
    delegate* unmanaged<int, int> function = &Double;
    Console.WriteLine(function(21));
}
";
        var exception = Assert.Throws<InvalidOperationException>(() => CompileAndRun(csharp));
        Assert.Contains("Unmanaged function pointers are not supported", exception.Message);
    }

    [Test]
    public void AsyncTaskYieldResumesStateMachine()
    {
        var csharp = @"
Console.WriteLine(""Before yield"");
await Task.Yield();
Console.WriteLine(""After yield"");
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Before yield\nAfter yield", output);
    }

    [Test]
    public void AsyncTaskSafeAwaiterResumesStateMachine()
    {
        var csharp = @"
using System.Runtime.CompilerServices;

await new SafeAwaitable();
Console.WriteLine(""Safe continuation"");

readonly struct SafeAwaitable
{
    public SafeAwaiter GetAwaiter() => new();
}

readonly struct SafeAwaiter : INotifyCompletion
{
    public bool IsCompleted => false;

    public void OnCompleted(Action continuation) => continuation();

    public void GetResult()
    {
    }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Safe continuation", output);
    }

    [Test]
    public void AsyncTaskConfigureAwaitPreservesCompletionAndResult()
    {
        var csharp = @"
static async Task<int> GetAnswer()
{
    await Task.Delay(1).ConfigureAwait(false);
    return 42;
}

await Task.Delay(1).ConfigureAwait(true);
Console.WriteLine(await GetAnswer().ConfigureAwait(false));
";
        var output = CompileAndRun(csharp);
        Assert.Equal("42", output);
    }

    [Test]
    public void AsyncTaskConfigureAwaitOptionsHonorSupportedFlagsAndValidation()
    {
        var csharp = @"
using System.Threading.Tasks;

var forcedAwaiter = Task.CompletedTask
    .ConfigureAwait(ConfigureAwaitOptions.ForceYielding)
    .GetAwaiter();
Console.WriteLine(forcedAwaiter.IsCompleted);

await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
Console.WriteLine(""yielded"");

await Task.FromException(new InvalidOperationException(""suppressed""))
    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
Console.WriteLine(""suppressed"");

Console.WriteLine(await Task.FromResult(42).ConfigureAwait(ConfigureAwaitOptions.None));

try
{
    Task.CompletedTask.ConfigureAwait((ConfigureAwaitOptions)8);
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine(""invalid"");
}

try
{
    Task.FromResult(1).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine(""generic suppress rejected"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "False\nyielded\nsuppressed\n42\ninvalid\ngeneric suppress rejected",
            output);
    }

    [Test]
    public void TaskFactoriesExposeSupportedCompletionStateAndResult()
    {
        var csharp = @"
var completed = Task.CompletedTask;
Console.WriteLine((int)completed.Status);
Console.WriteLine(completed.IsCompleted);
Console.WriteLine(completed.IsCompletedSuccessfully);
Console.WriteLine(completed.IsFaulted);
Console.WriteLine(completed.IsCanceled);

var result = Task.FromResult(42);
Console.WriteLine(result.Result);

var faulted = Task.FromException(new InvalidOperationException(""boom""));
Console.WriteLine((int)faulted.Status);
Console.WriteLine(faulted.IsFaulted);
try
{
    await faulted;
}
catch (InvalidOperationException exception)
{
    Console.WriteLine(exception.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("5\nTrue\nTrue\nFalse\nFalse\n42\n7\nTrue\nboom", output);
    }

    [Test]
    public void TaskFactoriesRejectNullArgumentsWithManagedExceptions()
    {
        var csharp = @"
try
{
    Task.FromException(null!);
}
catch (ArgumentNullException)
{
    Console.WriteLine(""from exception"");
}

try
{
    Task.FromException<int>(null!);
}
catch (ArgumentNullException)
{
    Console.WriteLine(""generic from exception"");
}

try
{
    Task.Run((Action)null!);
}
catch (ArgumentNullException)
{
    Console.WriteLine(""run action"");
}

try
{
    Task.Run((Func<Task>)null!);
}
catch (ArgumentNullException)
{
    Console.WriteLine(""run task"");
}

try
{
    Task.Run((Func<int>)null!);
}
catch (ArgumentNullException)
{
    Console.WriteLine(""run result"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "from exception\ngeneric from exception\nrun action\nrun task\nrun result",
            output);
    }

    [Test]
    public void FaultedTaskInspectionAndResultExposeManagedAggregateException()
    {
        var csharp = @"
        var original = new InvalidOperationException(""boom"");
var faulted = Task.FromException<int>(original);
var aggregate = faulted.Exception;

Console.WriteLine(aggregate is AggregateException);
Console.WriteLine(aggregate!.InnerException is InvalidOperationException);

try
{
    _ = faulted.Result;
}
catch (AggregateException exception)
{
    Console.WriteLine(exception.InnerException is InvalidOperationException);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("True\nTrue\nTrue", output);
    }

    [Test]
    public void RuntimeBoundaryExceptionResultRetainsConcreteDispatchType()
    {
        var csharp = @"
var faulted = Task.FromException<int>(new InvalidOperationException(""boom""));
Console.WriteLine(faulted.Exception!.InnerException!.Message);
";
        Assert.Equal("boom", CompileAndRun(csharp));
    }

    [Test]
    public void TaskWhenAllAndWhenAnyComposeJavaScriptTasks()
    {
        var csharp = @"
await Task.WhenAll(Task.Delay(1), Task.CompletedTask);
Console.WriteLine(""all complete"");

var results = await Task.WhenAll(Task.FromResult(2), Task.FromResult(3));
Console.WriteLine(results[0] + results[1]);

var winner = await Task.WhenAny(Task.Delay(10), Task.CompletedTask);
Console.WriteLine(winner.IsCompletedSuccessfully);

var genericWinner = await Task.WhenAny(Task.FromResult(7), Task.FromResult(8));
Console.WriteLine(genericWinner.Result);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("all complete\n5\nTrue\n7", output);
    }

    [Test]
    public void TaskWhenAllAndWhenAnyAcceptEnumerables()
    {
        var csharp = @"
IEnumerable<Task<int>> resultTasks = new[] { Task.FromResult(2), Task.FromResult(3) };
var results = await Task.WhenAll(resultTasks);
Console.WriteLine(results[0] + results[1]);

IEnumerable<Task> tasks = new[] { Task.Delay(10), Task.CompletedTask };
var winner = await Task.WhenAny(tasks);
Console.WriteLine(winner == Task.CompletedTask);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("5\nTrue", output);
    }

    [Test]
    public void TaskWhenAllAndWhenAnyEnumerateNonArrayInputs()
    {
        var csharp = @"
using System.Collections;

var results = await Task.WhenAll(new ResultTasks());
Console.WriteLine(results[0] + results[1]);

var genericWinner = await Task.WhenAny(new ResultTasks());
Console.WriteLine(genericWinner.Result);

await Task.WhenAll(new Tasks());
Console.WriteLine(""all complete"");

var winner = await Task.WhenAny(new Tasks());
Console.WriteLine(winner == Task.CompletedTask);

public sealed class ResultTasks : IEnumerable<Task<int>>
{
    public IEnumerator<Task<int>> GetEnumerator()
    {
        yield return Task.FromResult(2);
        yield return Task.FromResult(3);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class Tasks : IEnumerable<Task>
{
    public IEnumerator<Task> GetEnumerator()
    {
        yield return Task.Delay(10);
        yield return Task.CompletedTask;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("5\n2\nall complete\nTrue", output);
    }

    [Test]
    public void TaskWhenAllRetainsEveryFaultAndCombinatorsValidateElements()
    {
        var csharp = @"
var combined = Task.WhenAll(
    Task.FromException(new InvalidOperationException(""first"")),
    Task.FromException(new ArgumentException(""second"")));

Console.WriteLine(combined.Exception is AggregateException);
Console.WriteLine(combined.Exception!.InnerExceptions.Count);
Console.WriteLine(combined.Exception.InnerExceptions[0].Message);
Console.WriteLine(combined.Exception.InnerExceptions[1].Message);

try
{
    Task.WhenAll(Task.CompletedTask, null!);
}
catch (ArgumentException)
{
    Console.WriteLine(""null element"");
}

try
{
    Task.WhenAny(new Task[0]);
}
catch (ArgumentException)
{
    Console.WriteLine(""empty when any"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "True\n2\nfirst\nsecond\nnull element\nempty when any",
            output);
    }

    [Test]
    public void TaskCombinatorsRejectNullArgumentsWithManagedExceptions()
    {
        var csharp = @"
try
{
    Task.WhenAll((Task[])null!);
}
catch (ArgumentNullException)
{
    Console.WriteLine(""when all"");
}

try
{
    Task.WhenAny((Task[])null!);
}
catch (ArgumentNullException)
{
    Console.WriteLine(""when any array"");
}

try
{
    Task.WhenAny(null!, Task.CompletedTask);
}
catch (ArgumentNullException)
{
    Console.WriteLine(""when any first"");
}

try
{
    Task.CompletedTask.ContinueWith((Action<Task>)null!);
}
catch (ArgumentNullException)
{
    Console.WriteLine(""continue action"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "when all\nwhen any array\nwhen any first\ncontinue action",
            output);
    }

    [Test]
    public void TaskRunAndContinueWithUseJavaScriptScheduler()
    {
        var csharp = @"
static void RunAction() => Console.WriteLine(""run action"");
static int GetAnswer() => 42;
static int Increment(Task<int> task) => task.Result + 1;
static void ReportCompletion(Task task) => Console.WriteLine(task.IsCompleted);

await Task.Run(RunAction);
Console.WriteLine(await Task.Run(GetAnswer));

var continuation = Task.FromResult(7).ContinueWith(Increment);
Console.WriteLine(await continuation);

await Task.CompletedTask.ContinueWith(ReportCompletion);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("run action\n42\n8\nTrue", output);
    }

    [Test]
    public void TaskRunAndContinueWithSupportCompilerGeneratedNoncapturingLambdas()
    {
        var csharp = @"
await Task.Run(() => Console.WriteLine(""run action""));
Console.WriteLine(await Task.Run(() => 42));
Console.WriteLine(await Task.FromResult(7).ContinueWith(task => task.Result + 1));
";
        var output = CompileAndRun(csharp);
        Assert.Equal("run action\n42\n8", output);
    }

    [Test]
    public void RuntimeOutputLargerThanTheRedirectedPipeIsFullyCaptured()
    {
        const string line = "0123456789abcdef0123456789abcdef";
        var output = CompileAndRun($"for (var i = 0; i < 2048; i++) Console.WriteLine(\"{line}\");");
        Assert.Equal(string.Join("\n", Enumerable.Repeat(line, 2048)), output);
    }

    [Test]
    public void TaskRunHonorsCancellationTokensAcrossDelegateShapes()
    {
        var csharp = @"
static void RunAction() => Console.WriteLine(""action"");
static Task RunAsync() => Task.CompletedTask;
static int GetResult() => 42;
static Task<int> GetAsyncResult() => Task.FromResult(43);
static void ShouldNotRun() => Console.WriteLine(""unexpected"");

await Task.Run(RunAction, CancellationToken.None);
await Task.Run(RunAsync, CancellationToken.None);
Console.WriteLine(await Task.Run(GetResult, CancellationToken.None));
Console.WriteLine(await Task.Run(GetAsyncResult, CancellationToken.None));

var canceled = new CancellationToken(true);
var canceledAction = Task.Run(ShouldNotRun, canceled);
Console.WriteLine(canceledAction.IsCanceled);
Console.WriteLine(Task.Run(RunAsync, canceled).IsCanceled);
Console.WriteLine(Task.Run(GetResult, canceled).IsCanceled);
Console.WriteLine(Task.Run(GetAsyncResult, canceled).IsCanceled);

try
{
    await canceledAction;
}
catch (TaskCanceledException)
{
    Console.WriteLine(""canceled"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("action\n42\n43\nTrue\nTrue\nTrue\nTrue\ncanceled", output);
    }

    [Test]
    public void ContinueWithSupportsStateCancellationOptionsAndSchedulers()
    {
        var csharp = @"
static void ReportState(Task task, object? state)
{
    Console.WriteLine((string?)state);
    Console.WriteLine(task.IsCompletedSuccessfully);
}
static string FormatState(Task task, object? state) => (string)state!;
static void ReportGenericState(Task<int> task, object? state)
{
    Console.WriteLine((string?)state);
    Console.WriteLine(task.Result);
}
static string FormatGenericState(Task<int> task, object? state) => (string)state!;
static void ShouldNotRun(Task task) => Console.WriteLine(""unexpected"");
static void ReportCanceled(Task task) => Console.WriteLine(task.IsCanceled);
static void ReportSynchronous(Task task) => Console.WriteLine(""synchronous"");
static void ReportDefaultScheduler(Task task) => Console.WriteLine(""default scheduler"");

await Task.CompletedTask.ContinueWith(ReportState, ""action state"");
Console.WriteLine(await Task.CompletedTask.ContinueWith(FormatState, ""function state""));

var genericAntecedent = Task.FromResult(42);
await genericAntecedent.ContinueWith(ReportGenericState, ""generic action state"");
Console.WriteLine(await genericAntecedent.ContinueWith(FormatGenericState, ""generic function state""));

await Task.CompletedTask.ContinueWith(
    ShouldNotRun,
    new CancellationToken(true)).ContinueWith(
        ReportCanceled,
        TaskContinuationOptions.ExecuteSynchronously);

var filtered = Task.CompletedTask.ContinueWith(
    ShouldNotRun,
    TaskContinuationOptions.NotOnRanToCompletion);
Console.WriteLine(filtered.IsCanceled);

Task.CompletedTask.ContinueWith(
    ReportSynchronous,
    TaskContinuationOptions.ExecuteSynchronously);
Console.WriteLine(""after synchronous"");

await Task.CompletedTask.ContinueWith(
    ReportDefaultScheduler,
    TaskScheduler.Default);
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "action state\nTrue\nfunction state\ngeneric action state\n42\ngeneric function state\nTrue\nTrue\nsynchronous\nafter synchronous\ndefault scheduler",
            output);
    }

    [Test]
    public void ContinueWithSupportsFullParameterOverloadShapes()
    {
        var csharp = @"
static void ReportTask(Task task) => Console.WriteLine(""task action"");
static void ReportTaskState(Task task, object? state) => Console.WriteLine((string?)state);
static string FormatTask(Task task) => ""task function"";
static string FormatTaskState(Task task, object? state) => (string)state!;
static void ReportGenericTask(Task<int> task) => Console.WriteLine(""generic action"");
static void ReportGenericTaskState(Task<int> task, object? state) => Console.WriteLine((string?)state);
static string FormatGenericTask(Task<int> task) => ""generic function"";
static string FormatGenericTaskState(Task<int> task, object? state) => (string)state!;

var token = CancellationToken.None;
var options = TaskContinuationOptions.ExecuteSynchronously;
var scheduler = TaskScheduler.Default;

await Task.CompletedTask.ContinueWith(ReportTask, token, options, scheduler);
await Task.CompletedTask.ContinueWith(ReportTaskState, ""task state combined"", token, options, scheduler);
Console.WriteLine(await Task.CompletedTask.ContinueWith(FormatTask, token, options, scheduler));
Console.WriteLine(await Task.CompletedTask.ContinueWith(FormatTaskState, ""task function state combined"", token, options, scheduler));

var genericTask = Task.FromResult(42);
await genericTask.ContinueWith(ReportGenericTask, token, options, scheduler);
await genericTask.ContinueWith(ReportGenericTaskState, ""generic state combined"", token, options, scheduler);
Console.WriteLine(await genericTask.ContinueWith(FormatGenericTask, token, options, scheduler));
Console.WriteLine(await genericTask.ContinueWith(FormatGenericTaskState, ""generic function state combined"", token, options, scheduler));
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "task action\n"
            + "task state combined\n"
            + "task function\n"
            + "task function state combined\n"
            + "generic action\n"
            + "generic state combined\n"
            + "generic function\n"
            + "generic function state combined",
            output);
    }

    [Test]
    public void AsyncTaskOfTPropagatesResult()
    {
        var csharp = @"
static async Task<int> GetAnswer()
{
    await Task.Delay(1);
    return 42;
}

static async Task<string> GetGreeting()
{
    await Task.Yield();
    return ""Hello"";
}

static async Task<int> GetImmediateAnswer()
{
    return 7;
}

static async Task<int> Fail()
{
    await Task.Yield();
    throw new Exception(""Boom"");
}

Console.WriteLine(await GetAnswer());
Console.WriteLine(await GetGreeting());
Console.WriteLine(await GetImmediateAnswer());
try
{
    Console.WriteLine(await Fail());
}
catch (Exception)
{
    Console.WriteLine(""Caught"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("42\nHello\n7\nCaught", output);
    }

}

public sealed class CoreIntegrationTests : IntegrationTestBase
{
    [Test]
    public void ConditionalIfElse()
    {
        var csharp = @"
var x = 5;
if (x == 5)
{
    Console.WriteLine(""Equal"");
}
else
{
    Console.WriteLine(""Not equal"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Equal", output);
    }

    [Test]
    public void SimpleLoop()
    {
        var csharp = @"
for (int i = 0; i < 3; i++)
{
    Console.WriteLine(i);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("0\n1\n2", output);
    }

    [Test]
    public void InheritanceBasicVirtualMethod()
    {
        var csharp = @"
var animal = new Animal();
var dog = new Dog();
Console.WriteLine(animal.Speak());
Console.WriteLine(dog.Speak());

public class Animal
{
    public virtual string Speak()
    {
        return ""Some sound"";
    }
}

public class Dog : Animal
{
    public override string Speak()
    {
        return ""Woof"";
    }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Some sound\nWoof", output);
    }

    [Test]
    public void InterfaceImplementation()
    {
        var csharp = @"
IGreeter formal = new FormalGreeter();
IGreeter casual = new CasualGreeter();
Console.WriteLine(formal.Greet(""Alice""));
Console.WriteLine(casual.Greet(""Bob""));

public interface IGreeter
{
    string Greet(string name);
}

public class FormalGreeter : IGreeter
{
    public string Greet(string name)
    {
        return $""Good day, {name}"";
    }
}

public class CasualGreeter : IGreeter
{
    public string Greet(string name)
    {
        return $""Hey, {name}"";
    }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Good day, Alice\nHey, Bob", output);
    }

    [Test]
    public void StringMethods()
    {
        var csharp = @"
// String methods
var text = ""  Hello World  "";
Console.WriteLine(text.Trim());
Console.WriteLine(text.ToUpper());
Console.WriteLine(text.Contains(""World""));
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Hello World\n  HELLO WORLD  \nTrue", output);
    }

    [Test]
    public void MathOperations()
    {
        var csharp = @"
// Math operations
Console.WriteLine(Math.Max(10, 20));
Console.WriteLine(Math.Min(10, 20));
Console.WriteLine(Math.Abs(-42));
Console.WriteLine(Math.Log(1));
Console.WriteLine(Math.Log10(10));
Console.WriteLine(Math.Exp(0));
Console.WriteLine(Math.Truncate(3.7));
Console.WriteLine(Math.Ceiling(3.2));
Console.WriteLine(Math.Floor(3.7));
Console.WriteLine(Math.Clamp(5, 0, 10));
";
        var output = CompileAndRun(csharp);
        Assert.Equal("20\n10\n42\n0\n1\n1\n3\n4\n3\n5", output);
    }

    [Test]
    public void EnumerableIntegrationTest()
    {
        var csharp = @"
using System.Collections;

foreach (var i in new Enumer())
{
    Console.WriteLine(i);
}

public class Enumer : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator()
    {
        for (var i = 0; i < 3; i++)
        {
            yield return i;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
";
        var output = CompileAndRun(csharp);

        Assert.Equal("0\n1\n2", output);
    }

    [Test]
    public void ArrayOperations()
    {
        var csharp = @"
// Array operations
var numbers = new int[] { 1, 2, 3 };
Console.WriteLine(numbers.Length);
Console.WriteLine(numbers[1]);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("3\n2", output);
    }

    [Test]
    public void ReflectionMetadataPreservesPropertiesMethodsTypesAndAttributes()
    {
        var csharp = @"
using System;

var model = new ViewModel();
foreach (var property in typeof(ViewModel).GetProperties())
{
    var attributes = property.GetCustomAttributes(false);
    Console.WriteLine(property.Name);
    Console.WriteLine(property.PropertyType.ToString());
    Console.WriteLine(attributes.Length == 0 ? ""none"" : attributes[0].GetType().ToString());
    if (property.Name == nameof(ViewModel.Name))
    {
        Console.WriteLine(property.GetValue(model));
        property.SetValue(model, ""changed"");
    }
}

foreach (var method in typeof(ViewModel).GetMethods())
{
    if (method.Name == nameof(ViewModel.Describe))
    {
        Console.WriteLine(method.Name);
        Console.WriteLine(method.ReturnType.ToString());
        Console.WriteLine(method.Invoke(model, Array.Empty<object>()));
    }
}

var typeAttributes = typeof(ViewModel).GetCustomAttributes(false);
Console.WriteLine(typeAttributes[typeAttributes.Length - 1].GetType().ToString());

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class MarkerAttribute : Attribute
{
    public MarkerAttribute(string value) => Value = value;
    public string Value { get; }
}

[Marker(""type"")]
public sealed class ViewModel
{
    [Marker(""property"")]
    public string Name { get; set; } = ""value"";
    public int Count { get; set; }
    public string Describe() => Name;
}
";

        var output = CompileAndRun(csharp);
        Assert.Equal(
            "Name\nSystem.String\nMarkerAttribute\nvalue\nCount\nSystem.Int32\nnone\nDescribe\nSystem.String\nchanged\nMarkerAttribute",
            output);
    }

    [Test]
    public void ExpressionTreesCanBeConstructedAndInspected()
    {
        var csharp = @"
using System;
using System.Linq.Expressions;

Expression<Func<ViewModel, string>> expression = model => model.Name;
var member = (MemberExpression)expression.Body;
Console.WriteLine(member.Member.Name);
Console.WriteLine(((ParameterExpression)member.Expression!).Name);
Console.WriteLine(expression.ReturnType.ToString());

Expression<Func<ViewModel, DetailModel>> detailExpression = model => model.Detail;
var detailMember = (MemberExpression)detailExpression.Body;
var detailProperty = (System.Reflection.PropertyInfo)detailMember.Member;
Console.WriteLine(detailExpression.ReturnType.GetProperties()[0].Name);
Console.WriteLine(detailProperty.PropertyType.GetProperties()[0].Name);

public sealed class ViewModel
{
    public string Name { get; set; } = ""value"";
    public DetailModel Detail { get; set; } = new();
}

public sealed class DetailModel
{
    public int Count { get; set; }
}
";

        var output = CompileAndRun(csharp);
        Assert.Equal("Name\nmodel\nSystem.String\nCount\nCount", output);
    }

    public static TestCaseDataCollection<string, string> UnsupportedDynamicReflectionPrograms => new()
    {
        {
            """
            using System;
            using System.Reflection;
            _ = Assembly.Load("Example");
            Console.WriteLine("unreachable");
            """,
            "dynamic assembly loading"
        },
        {
            """
            using System;
            using System.Reflection;
            using System.Reflection.Emit;
            _ = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Example"), AssemblyBuilderAccess.Run);
            Console.WriteLine("unreachable");
            """,
            "Reflection.Emit"
        },
        {
            """
            using System;
            _ = typeof(string).GetMethod(nameof(ToString), Type.EmptyTypes)!.MethodHandle.GetFunctionPointer();
            Console.WriteLine("unreachable");
            """,
            "unmanaged runtime handles"
        },
        {
            """
            using System;
            _ = typeof(string).Assembly.GetTypes();
            Console.WriteLine("unreachable");
            """,
            "framework-wide reflection"
        },
    };

    [TestCaseSource(nameof(UnsupportedDynamicReflectionPrograms))]
    public void UnsupportedDynamicReflectionFeaturesFailClosed(string csharp, string expectedFeature)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CompileAndRun(csharp));
        Assert.Contains(expectedFeature, exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not supported", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

}

public sealed class GenericIntegrationTests : IntegrationTestBase
{
    [Test]
    public void StaticGenericFields()
    {
        var csharp = @"
// Static generic fields stay separate
Test<int>.Value = 1;
Test<long>.Value = 2;
Test<string>.Value = ""test"";

Console.WriteLine(Test<int>.Value);
Console.WriteLine(Test<long>.Value);
Console.WriteLine(Test<string>.Value);

public class Test<T>
{
    public static T Value;
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("1\n2\ntest", output);
    }

    [Test]
    public void HiddenInstanceFieldsAndBackingFieldsRetainDeclaringTypeIdentity()
    {
        var csharp = @"
var derived = new Derived<int>();
Base<int> asBase = derived;

derived.Value = 11;
asBase.Value = 22;
derived.Property = 33;
asBase.SetBaseProperty(44);

Console.WriteLine(derived.Value);
Console.WriteLine(asBase.Value);
Console.WriteLine(derived.Property);
Console.WriteLine(asBase.ReadBaseProperty());

derived.SetDerivedValueViaReference(55);
asBase.SetBaseValueViaReference(66);
Console.WriteLine(derived.Value);
Console.WriteLine(asBase.Value);

foreach (var field in typeof(Derived<int>).GetFields())
{
    if (field.Name != nameof(Derived<int>.Value)) continue;
    var current = (int)field.GetValue(derived)!;
    Console.WriteLine(field.Name);
    Console.WriteLine(field.FieldType.ToString());
    Console.WriteLine(current);
    field.SetValue(derived, current == 55 ? 77 : 88);
}
Console.WriteLine(derived.Value);
Console.WriteLine(asBase.Value);

public class Base<T>
{
    public T Value = default!;
    public T Property { get; set; } = default!;
    public void SetBaseProperty(T value) => Property = value;
    public T ReadBaseProperty() => Property;
    public void SetBaseValueViaReference(T value) => Assign(ref Value, value);
    private static void Assign(ref T location, T value) => location = value;
}

public sealed class Derived<T> : Base<T>
{
    public new T Value = default!;
    public new T Property { get; set; } = default!;
    public void SetDerivedValueViaReference(T value) => Assign(ref Value, value);
    private static void Assign(ref T location, T value) => location = value;
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "11\n22\n33\n44\n55\n66\n"
            + "Value\nSystem.Int32\n55\n"
            + "Value\nSystem.Int32\n66\n"
            + "77\n88",
            output);
    }

    [Test]
    public void EnumMetadataIsAvailableThroughManagedReflection()
    {
        var csharp = @"
var backingTypes = Matches(typeof(SByteValue), typeof(sbyte))
    && Matches(typeof(ByteValue), typeof(byte))
    && Matches(typeof(Int16Value), typeof(short))
    && Matches(typeof(UInt16Value), typeof(ushort))
    && Matches(typeof(Int32Value), typeof(int))
    && Matches(typeof(UInt32Value), typeof(uint))
    && Matches(typeof(Int64Value), typeof(long))
    && Matches(typeof(UInt64Value), typeof(ulong));
Console.WriteLine(backingTypes);
Console.WriteLine(typeof(string).IsEnum);

var type = typeof(ReflectedValue);
var names = type.GetEnumNames();
Console.WriteLine(names.Length);
Console.WriteLine(names[0]);
Console.WriteLine(names[2]);
Console.WriteLine(names[3]);
Console.WriteLine(names[4]);

var values = (ReflectedValue[])type.GetEnumValues();
Console.WriteLine(values.Length);
Console.WriteLine(values[0] == ReflectedValue.Zero);
Console.WriteLine(values[2] == ReflectedValue.High);
Console.WriteLine(values[4] == ReflectedValue.Maximum);

var underlyingValues = (ulong[])type.GetEnumValuesAsUnderlyingType();
Console.WriteLine(underlyingValues[4] == ulong.MaxValue);
Console.WriteLine(type.GetEnumName(ReflectedValue.High));
Console.WriteLine(type.GetEnumName((ReflectedValue)2) == null);
Console.WriteLine(type.IsEnumDefined(ReflectedValue.HighAlias));
Console.WriteLine(type.IsEnumDefined(""Maximum""));
Console.WriteLine(type.IsEnumDefined((ReflectedValue)2));
Console.WriteLine(Enum.GetName(type, ReflectedValue.One));
Console.WriteLine(Enum.IsDefined(type, ReflectedValue.One));

static bool Matches(Type enumType, Type underlyingType) =>
    enumType.IsEnum && enumType.GetEnumUnderlyingType() == underlyingType;

enum SByteValue : sbyte { Value = -1 }
enum ByteValue : byte { Value = byte.MaxValue }
enum Int16Value : short { Value = short.MinValue }
enum UInt16Value : ushort { Value = ushort.MaxValue }
enum Int32Value : int { Value = int.MinValue }
enum UInt32Value : uint { Value = uint.MaxValue }
enum Int64Value : long { Value = long.MinValue }
enum UInt64Value : ulong { Value = ulong.MaxValue }
enum ReflectedValue : ulong
{
    Zero = 0,
    One = 1,
    High = 0x8000000000000000UL,
    HighAlias = High,
    Maximum = ulong.MaxValue,
}
";

        var output = CompileAndRun(csharp);

        Assert.Equal(
            "True\nFalse\n"
            + "5\nZero\nHigh\nHighAlias\nMaximum\n"
            + "5\nTrue\nTrue\nTrue\nTrue\n"
            + "High\nTrue\nTrue\nTrue\nFalse\nOne\nTrue",
            output);
    }


    [Test]
    public void StaticGenericProperties()
    {
        // NOTE: This looks like a dup of above, but it's not!
        // This is really about tunneling the generic context into methods
        var csharp = @"
// Static generic properties stay separate
Test<int>.Value = 1;
Test<long>.Value = 2;
Test<string>.Value = ""test"";

Console.WriteLine(Test<int>.Value);
Console.WriteLine(Test<long>.Value);
Console.WriteLine(Test<string>.Value);

public class Test<T>
{
    public static T Value { get; set; }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("1\n2\ntest", output);
    }


    [Test]
    public void GenericMethodCall()
    {
        var csharp = @"
Console.WriteLine(GenericTest.Identity<int>(42));
Console.WriteLine(GenericTest.Identity<string>(""test""));

// Generic method call
public static class GenericTest
{
    public static T Identity<T>(T value)
    {
        return value;
    }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("42\ntest", output);
    }

    [Test]
    public void GenericClassAndMethodRuntimeTypes()
    {
        var csharp = @"
using System;

namespace Compiler.Tests
{
    public static class GenericTest<T>
    {
        public static void Identity<Y>(T value)
        {
            Console.WriteLine(typeof(GenericTest<>).ToString());
            Console.WriteLine(typeof(GenericTest<int>).ToString());
            Console.WriteLine(typeof(T).ToString());
            Console.WriteLine(typeof(Y).ToString());
        }
    }
    
    public class Program
    {
        public static void Main()
        {
            GenericTest<string>.Identity<int>(""test"");
        }
    }
}
";
        var genericType = typeof(GenericTest<>).ToString();
        var concreteType = typeof(GenericTest<int>).ToString();
        var expected = $"{genericType}\n{concreteType}\n{typeof(string)}\n{typeof(int)}";
        var output = CompileAndRun(csharp);
        Assert.Equal(expected, output);
    }

    [Test]
    public void GenericTypeParameter()
    {
        var csharp = @"
var box = new GenericBox<int>(100);
Console.WriteLine(box.GetValue());

// Generic type parameter
public class GenericBox<T>
{
    private T _value;
    
    public GenericBox(T value)
    {
        _value = value;
    }
    
    public T GetValue()
    {
        return _value;
    }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("100", output);
    }

    [Test]
    public void NestedClosedGenericArgumentsRemainAvailableAtEveryRuntimeBoundary()
    {
        var csharp = @"
using System;
using System.Collections.Generic;

Console.WriteLine(Describe<Box<Box<int>>>());
Console.WriteLine(Describe<int?>());
Console.WriteLine(Describe<Dictionary<string, List<int?>>>());
Console.WriteLine(Forward<Box<Box<int>>>());

Console.WriteLine(MakeArray<int>().Length);
Console.WriteLine(MakeArray<Box<Box<int>>>().Length);

Box<Box<int>> local = default;
Console.WriteLine(local.DescribeArgument());

var model = new Model();
Console.WriteLine(model.Field.DescribeArgument());
Console.WriteLine(model.Property.DescribeArgument());

foreach (var property in typeof(Model).GetProperties())
{
    if (property.Name == nameof(Model.Property))
    {
        Console.WriteLine(property.PropertyType.ToString());
    }
    if (property.Name == nameof(Model.Optional))
    {
        Console.WriteLine(property.PropertyType.ToString());
    }
}

foreach (var method in typeof(Model).GetMethods())
{
    if (method.Name == nameof(Model.Create))
    {
        Console.WriteLine(method.ReturnType.ToString());
    }
}

static string Describe<T>() => typeof(T).ToString();
static string Forward<T>() => Describe<T>();
static T[] MakeArray<T>() => new T[1];

public struct Box<T>
{
    public string DescribeArgument() => typeof(T).ToString();
}

public sealed class Model
{
    public Box<Box<int>> Field;
    public Box<Box<int>> Property { get; set; }
    public int? Optional { get; set; }
    public Box<Box<int>> Create() => Property;
}
";

        var output = CompileAndRun(csharp);
        Assert.Equal(
            "Box`1[Box`1[System.Int32]]\n"
            + "System.Nullable`1[System.Int32]\n"
            + "System.Collections.Generic.Dictionary`2[System.String, System.Collections.Generic.List`1[System.Nullable`1[System.Int32]]]\n"
            + "Box`1[Box`1[System.Int32]]\n"
            + "1\n"
            + "1\n"
            + "Box`1[System.Int32]\n"
            + "Box`1[System.Int32]\n"
            + "Box`1[System.Int32]\n"
            + "Box`1[Box`1[System.Int32]]\n"
            + "System.Nullable`1[System.Int32]\n"
            + "Box`1[Box`1[System.Int32]]",
            output);
    }

    [Test]
    public void GenericElementArraysPreserveRuntimeIdentityCastsFieldsAndStores()
    {
        var csharp = @"
using System;

GenericBox<int>[] local = new GenericBox<int>[1];
local[0] = new GenericBox<int>();
ArrayHolder.IntegerBoxes = local;
ArrayHolder.StringBoxes = new GenericBox<string>[1];

object boxed = local;
var cast = (GenericBox<int>[])boxed;
Console.WriteLine(cast.Length);
Console.WriteLine(ArrayHolder.IntegerBoxes.Length);
Console.WriteLine(typeof(GenericBox<int>[]) == typeof(GenericBox<int>[]));
Console.WriteLine(typeof(GenericBox<int>[]) == typeof(GenericBox<string>[]));
Console.WriteLine(typeof(GenericBox<int>[,]).ToString());
Console.WriteLine(typeof(GenericBox<int>[][]).ToString());
Console.WriteLine(typeof(Container<GenericBox<int>[]>).ToString());

try
{
    object[] covariant = local;
    covariant[0] = new GenericBox<string>();
    Console.WriteLine(""store accepted"");
}
catch (ArrayTypeMismatchException)
{
    Console.WriteLine(""store rejected"");
}

public sealed class GenericBox<T>;
public sealed class Container<T>;
public static class ArrayHolder
{
    public static GenericBox<int>[] IntegerBoxes = null!;
    public static GenericBox<string>[] StringBoxes = null!;
}
";

        var output = CompileAndRun(csharp);
        Assert.Equal(
            "1\n"
            + "1\n"
            + "True\n"
            + "False\n"
            + "GenericBox`1[System.Int32][,]\n"
            + "GenericBox`1[System.Int32][][]\n"
            + "Container`1[GenericBox`1[System.Int32][]]\n"
            + "store rejected",
            output);
    }

    [Test]
    public void SzArraysImplementGenericEnumerationContracts()
    {
        var csharp = @"
using System;
using System.Collections;
using System.Collections.Generic;

string[] values = [""first"", ""second""];
IEnumerable<string> sequence = values;
var enumerator = sequence.GetEnumerator();

try
{
    Console.WriteLine(enumerator.Current);
}
catch (InvalidOperationException)
{
    Console.WriteLine(""current rejected before start"");
}

Console.WriteLine(enumerator.MoveNext());
Console.WriteLine(enumerator.Current);
Console.WriteLine(enumerator.MoveNext());
Console.WriteLine(enumerator.Current);
Console.WriteLine(enumerator.MoveNext());

try
{
    Console.WriteLine(enumerator.Current);
}
catch (InvalidOperationException)
{
    Console.WriteLine(""current rejected after end"");
}

((IEnumerator)enumerator).Reset();
Console.WriteLine(enumerator.MoveNext());
Console.WriteLine(enumerator.Current);
Console.WriteLine(((IEnumerator)enumerator).Current);
enumerator.Dispose();

foreach (var value in sequence)
{
    Console.WriteLine(value);
}

IEnumerable<object> covariant = values;
foreach (var value in covariant)
{
    Console.WriteLine(value);
}
";

        var output = CompileAndRun(csharp);
        Assert.Equal(
            "current rejected before start\n"
            + "True\n"
            + "first\n"
            + "True\n"
            + "second\n"
            + "False\n"
            + "current rejected after end\n"
            + "True\n"
            + "first\n"
            + "first\n"
            + "first\n"
            + "second\n"
            + "first\n"
            + "second",
            output);
    }

    [Test]
    public void BoxingAndUnboxing()
    {
        var csharp = @"
// Box value types to object
int intValue = 42;
object boxedInt = intValue;

// Unbox back to value type
int unboxedInt = (int)boxedInt;
Console.WriteLine(unboxedInt);

// Box different types
double doubleValue = 3.14;
object boxedDouble = doubleValue;
double unboxedDouble = (double)boxedDouble;
Console.WriteLine(unboxedDouble);

// Box bool
bool boolValue = true;
object boxedBool = boolValue;
bool unboxedBool = (bool)boxedBool;
Console.WriteLine(unboxedBool);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("42\n3.14\nTrue", output);
    }

    [Test]
    public void InvalidCastException()
    {
        var csharp = @"
try
{
    object obj = ""Hello"";
    int number = (int)obj;
    Console.WriteLine(""Should not reach here"");
}
catch (InvalidCastException)
{
    Console.WriteLine(""Caught InvalidCastException"");
}

// Also test valid cast works
try
{
    object obj = 42;
    int number = (int)obj;
    Console.WriteLine(number);
}
catch (InvalidCastException)
{
    Console.WriteLine(""Should not catch here"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Caught InvalidCastException\n42", output);
    }

    [Test]
    public void SwitchStatement()
    {
        var csharp = @"
int value = 2;
string result = value switch {
    0 => ""zero"",
    1 => ""one"",
    2 => ""two"",
    _ => ""other""
};
Console.WriteLine(result);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("two", output);
    }

    [Test]
    public void RefOutParameters()
    {
        var csharp = @"
void Swap(ref int a, ref int b) {
    int temp = a;
    a = b;
    b = temp;
}

int x = 10;
int y = 20;
Swap(ref x, ref y);
Console.WriteLine(x);
Console.WriteLine(y);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("20\n10", output);
    }

    [Test]
    public void SizeofAndVolatileUseSupportedRuntimeSemantics()
    {
        var csharp = @"
var holder = new Holder();
holder.Value = 7;
Console.WriteLine(holder.Value);
Console.WriteLine(SizeOf<int>());
Console.WriteLine(SizeOf<long>());

static unsafe int SizeOf<T>() where T : unmanaged => sizeof(T);

class Holder {
    public volatile int Value;
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("7\n4\n8", output);
    }

    [Test]
    public void StaticFields()
    {
        var csharp = @"
Counter.Increment();
Counter.Increment();
Counter.Increment();
Console.WriteLine(Counter.GetCount());

static class Counter {
    private static int count = 0;
    
    public static void Increment() {
        count++;
    }
    
    public static int GetCount() {
        return count;
    }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("3", output);
    }

}

public sealed class NumericIntegrationTests : IntegrationTestBase
{
    [Test]
    public void FixedWidthIntegerConversions()
    {
        var csharp = @"
int signedByteSource = 0x180;
int signedShortSource = 0x18000;
long unsignedSource = -1;
double floatingSource = 130.9;

Console.WriteLine((sbyte)signedByteSource);
Console.WriteLine((short)signedShortSource);
Console.WriteLine((byte)unsignedSource);
Console.WriteLine((ushort)unsignedSource);
Console.WriteLine((sbyte)floatingSource);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("-128\n-32768\n255\n65535\n-126", output);
    }

    [Test]
    public void CheckedIntegerConversions()
    {
        var csharp = @"
int signedSource = 127;
double floatingSource = 255.9;
long negativeSource = -1;
ulong unsignedSource = ulong.MaxValue;

Console.WriteLine(checked((sbyte)signedSource));
Console.WriteLine(checked((byte)floatingSource));

try {
    uint converted = checked((uint)negativeSource);
    Console.WriteLine(""unexpected"");
} catch (OverflowException) {
    Console.WriteLine(""uint overflow"");
}

try {
    long converted = checked((long)unsignedSource);
    Console.WriteLine(""unexpected"");
} catch (OverflowException) {
    Console.WriteLine(""long overflow"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("127\n255\nuint overflow\nlong overflow", output);
    }

    [Test]
    public void ArithmeticFailuresThrowCatchableClrExceptionTypes()
    {
        var csharp = @"
int zero = 0;
int negativeOne = -1;
int maximum = int.MaxValue;
int minimum = int.MinValue;

try {
    Console.WriteLine(1 / zero);
} catch (DivideByZeroException) {
    Console.WriteLine(""DivideByZeroException"");
}

try {
    Console.WriteLine(minimum / negativeOne);
} catch (OverflowException) {
    Console.WriteLine(""OverflowException"");
}

try {
    Console.WriteLine(minimum % negativeOne);
} catch (OverflowException) {
    Console.WriteLine(""OverflowException"");
}

try {
    Console.WriteLine(checked(maximum + 1));
} catch (OverflowException) {
    Console.WriteLine(""OverflowException"");
}

Console.WriteLine(7 / -2);
Console.WriteLine(double.IsPositiveInfinity(1d / zero));
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "DivideByZeroException\nOverflowException\nOverflowException\nOverflowException\n-3\nTrue",
            output);
    }

    [Test]
    public void RuntimeCreatedExceptionsAreCatchableBySystemException()
    {
        var csharp = @"
int zero = 0;
try {
    Console.WriteLine(1 / zero);
} catch (Exception exception) {
    Console.WriteLine(exception.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Attempted to divide by zero.", output);
    }

    [Test]
    public void ArrayFailuresThrowCatchableClrExceptionTypes()
    {
        var csharp = @"
int negativeLength = -1;
try {
    _ = new int[negativeLength];
} catch (OverflowException) {
    Console.WriteLine(""OverflowException"");
}

int[]? missing = null;
try {
    Console.WriteLine(missing.Length);
} catch (NullReferenceException) {
    Console.WriteLine(""NullReferenceException"");
}

var values = new int[1];
try {
    Console.WriteLine(values[1]);
} catch (IndexOutOfRangeException) {
    Console.WriteLine(""IndexOutOfRangeException"");
}

Base[] references = new Derived[1];
try {
    references[0] = new Base();
} catch (ArrayTypeMismatchException) {
    Console.WriteLine(""ArrayTypeMismatchException"");
}

class Base { }
class Derived : Base { }
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "OverflowException\nNullReferenceException\nIndexOutOfRangeException\nArrayTypeMismatchException",
            output);
    }

    [Test]
    public void NullReceiversThrowCatchableNullReferenceExceptions()
    {
        var csharp = @"
Target? target = null;
try {
    Console.WriteLine(target!.GetValue());
} catch (NullReferenceException) {
    Console.WriteLine(""callvirt"");
}

try {
    Func<int> getter = target!.GetValue;
    Console.WriteLine(getter());
} catch (NullReferenceException) {
    Console.WriteLine(""ldvirtftn"");
}

Holder? holder = null;
try {
    Console.WriteLine(holder!.Value);
} catch (NullReferenceException) {
    Console.WriteLine(""ldfld"");
}

try {
    holder!.Value = 1;
} catch (NullReferenceException) {
    Console.WriteLine(""stfld"");
}

try {
    SetValue(ref holder!.Value);
} catch (NullReferenceException) {
    Console.WriteLine(""ldflda"");
}

object? boxed = null;
try {
    Console.WriteLine((int)boxed!);
} catch (NullReferenceException) {
    Console.WriteLine(""unbox.any"");
}

Action? action = null;
try {
    action!();
} catch (NullReferenceException) {
    Console.WriteLine(""delegate"");
}

Exception? exception = null;
try {
    throw exception!;
} catch (NullReferenceException) {
    Console.WriteLine(""throw"");
}

static void SetValue(ref int value) => value = 2;

class Target {
    public virtual int GetValue() => 1;
}

class Holder {
    public int Value;
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "callvirt\nldvirtftn\nldfld\nstfld\nldflda\nunbox.any\ndelegate\nthrow",
            output);
    }

    [Test]
    public void ConstrainedCallsUseTheManagedReferenceReceiver()
    {
        const string source = """
            var counter = new Counter();
            Increment(ref counter);
            Console.WriteLine(counter.Value);

            static void Increment<T>(ref T value)
                where T : IIncrement => value.Increment();

            interface IIncrement
            {
                void Increment();
            }

            struct Counter : IIncrement
            {
                public int Value;

                public void Increment()
                {
                    Value++;
                }
            }
            """;

        var output = CompileAndRun(source);

        Assert.Equal("1", output);
    }

    [Test]
    public void TypeInitializerFailuresAreWrappedAndCached()
    {
        var csharp = @"
for (var attempt = 0; attempt < 2; attempt++) {
    try {
        Console.WriteLine(Broken.Value);
    } catch (TypeInitializationException exception) {
        Console.WriteLine(exception.TypeName);
        Console.WriteLine(exception.Message);
        Console.WriteLine(exception.InnerException!.Message);
    }
}

Console.WriteLine(Tracker.Attempts);

static class Tracker {
    public static int Attempts;
}

static class Broken {
    public static int Value = Initialize();

    private static int Initialize() {
        Tracker.Attempts++;
        throw new InvalidOperationException(""initialization failed"");
    }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "Broken\nThe type initializer for 'Broken' threw an exception.\ninitialization failed\n" +
            "Broken\nThe type initializer for 'Broken' threw an exception.\ninitialization failed\n1",
            output);
    }

    [Test]
    public void UnsignedIntegerToFloatingPointConversions()
    {
        var csharp = @"
uint source32 = uint.MaxValue;
ulong source64 = ulong.MaxValue;

double converted32 = source32;
double converted64 = source64;

Console.WriteLine(converted32 == 4294967295d);
Console.WriteLine(converted64 > long.MaxValue);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("True\nTrue", output);
    }

    [Test]
    public void NativeIntegerConversionsAndArrays()
    {
        var csharp = @"
long signedSource = -1;
uint unsignedSource = uint.MaxValue;
nint signed = (nint)signedSource;
nuint unsigned = (nuint)unsignedSource;
nint[] signedValues = new nint[1];
nuint[] unsignedValues = new nuint[1];

signedValues[0] = signed;
unsignedValues[0] = unsigned;

Console.WriteLine((long)signedValues[0]);
Console.WriteLine((ulong)unsignedValues[0] == uint.MaxValue);
";
        var output = CompileAndRun(csharp);
        Assert.Equal("-1\nTrue", output);
    }

    [Test]
    public void FloatingPointOperations()
    {
        var csharp = @"
float f = 3.14f;
double d = 2.718;
Console.WriteLine(f + d);
Console.WriteLine(f * 2.0f);
";
        var output = CompileAndRun(csharp);
        var lines = output.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("5.858", lines[0]);
        Assert.StartsWith("6.28", lines[1]);
    }

    [Test]
    public void NegativeZeroSurvivesFieldsArraysAndBoxing()
    {
        var csharp = @"
var holder = new Holder { Single = -0.0f, Double = -0.0d };
var singles = new[] { -0.0f, 1.0f, 2.0f, 3.0f };
var doubles = new[] { -0.0d, 1.0d, 2.0d, 3.0d };
object boxedSingle = -0.0f;
object boxedDouble = -0.0d;

Console.WriteLine(IsNegativeSingle(holder.Single));
Console.WriteLine(IsNegativeDouble(holder.Double));
Console.WriteLine(IsNegativeSingle(singles[0]));
Console.WriteLine(IsNegativeDouble(doubles[0]));
Console.WriteLine(IsNegativeSingle((float)boxedSingle));
Console.WriteLine(IsNegativeDouble((double)boxedDouble));

static bool IsNegativeSingle(float value) => value == 0.0f && 1.0f / value < 0.0f;
static bool IsNegativeDouble(double value) => value == 0.0d && 1.0d / value < 0.0d;

sealed class Holder
{
    public float Single;
    public double Double;
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("True\nTrue\nTrue\nTrue\nTrue\nTrue", output);
    }

    [Test]
    public void StructValueType()
    {
        var csharp = @"
Point p = new Point(10, 20);
Console.WriteLine(p.X);
Console.WriteLine(p.Y);

struct Point {
    public int X;
    public int Y;
    
    public Point(int x, int y) {
        X = x;
        Y = y;
    }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("10\n20", output);
    }

    [Test]
    public void NullableValueTypes()
    {
        var csharp = @"
int? nullable = 42;
int? nullValue = null;

if (nullable.HasValue) {
    Console.WriteLine(nullable.Value);
}

if (!nullValue.HasValue) {
    Console.WriteLine(""null"");
}

try {
    Console.WriteLine(nullValue.Value);
} catch (InvalidOperationException) {
    Console.WriteLine(""no value"");
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("42\nnull\nno value", output);
    }

}

public sealed class ExceptionIntegrationTests : IntegrationTestBase
{
    [Test]
    public void NestedExceptionHandlers()
    {
        var csharp = @"
try {
    try {
        throw new Exception(""Inner"");
    } catch (Exception ex) {
        Console.WriteLine(""Caught inner: "" + ex.Message);
        throw new Exception(""Outer"");
    }
} catch (Exception ex) {
    Console.WriteLine(""Caught outer: "" + ex.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("Caught inner: Inner\nCaught outer: Outer", output);
    }

    [Test]
    public void ExceptionUnwindingHonorsCatchTypesFinallyAndRethrow()
    {
        var csharp = @"
static void ThrowFromTryFinally()
{
    try
    {
        throw new InvalidOperationException(""failure"");
    }
    finally
    {
        Console.WriteLine(""exception finally"");
    }
}

try
{
    ThrowFromTryFinally();
}
catch (ArgumentException)
{
    Console.WriteLine(""wrong catch"");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
}

try
{
    Console.WriteLine(""body"");
}
finally
{
    Console.WriteLine(""normal finally"");
}

try
{
    try
    {
        throw new Exception(""original"");
    }
    catch (Exception)
    {
        throw;
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "exception finally\nfailure\nbody\nnormal finally\noriginal",
            output.Replace("\r\n", "\n"));
    }

    [Test]
    public void ExceptionUnwindSurvivesNestedFinallyInsideFinally()
    {
        var csharp = @"
try
{
    try
    {
        throw new InvalidOperationException(""original"");
    }
    finally
    {
        Console.WriteLine(""outer finally start"");
        try
        {
            Console.WriteLine(""nested try"");
        }
        finally
        {
            Console.WriteLine(""nested finally"");
        }
        Console.WriteLine(""outer finally end"");
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "outer finally start\nnested try\nnested finally\nouter finally end\noriginal",
            output.Replace("\r\n", "\n"));
    }

    [Test]
    public void ExceptionThrownFromFinallyReplacesPendingException()
    {
        var csharp = @"
try
{
    try
    {
        throw new InvalidOperationException(""original"");
    }
    finally
    {
        try
        {
            throw new Exception(""replacement"");
        }
        finally
        {
            Console.WriteLine(""nested finally"");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal("nested finally\nreplacement", output.Replace("\r\n", "\n"));
    }

    [Test]
    public void ExceptionUnwindRunsNestedFinallyRegionsInsideOut()
    {
        var csharp = @"
try
{
    try
    {
        try
        {
            try
            {
                throw new Exception(""failure"");
            }
            finally
            {
                Console.WriteLine(""inner finally"");
            }
        }
        finally
        {
            Console.WriteLine(""middle finally"");
        }
    }
    finally
    {
        Console.WriteLine(""outer finally"");
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "inner finally\nmiddle finally\nouter finally\nfailure",
            output.Replace("\r\n", "\n"));
    }

    [Test]
    public void LeavingProtectedRegionsRunsFinallyForReturnBreakAndContinue()
    {
        var csharp = @"
static int ReturnThroughFinally()
{
    try
    {
        return 42;
    }
    finally
    {
        Console.WriteLine(""return finally"");
    }
}

Console.WriteLine(ReturnThroughFinally());

for (var i = 0; i < 2; i++)
{
    try
    {
        Console.WriteLine(""break body"");
        break;
    }
    finally
    {
        Console.WriteLine(""break finally"");
    }
}

for (var i = 0; i < 2; i++)
{
    try
    {
        Console.WriteLine(""continue body"");
        Console.WriteLine(i);
        continue;
    }
    finally
    {
        Console.WriteLine(""continue finally"");
        Console.WriteLine(i);
    }
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "return finally\n42\nbreak body\nbreak finally\n"
            + "continue body\n0\ncontinue finally\n0\n"
            + "continue body\n1\ncontinue finally\n1",
            output.Replace("\r\n", "\n"));
    }

    [Test]
    public void ExceptionUnwindsAcrossMultipleCompiledMethodFrames()
    {
        var csharp = @"
static void ThrowAtLeaf()
{
    Console.WriteLine(""leaf"");
    throw new Exception(""cross-frame failure"");
}

static void Middle()
{
    try
    {
        ThrowAtLeaf();
    }
    finally
    {
        Console.WriteLine(""middle finally"");
    }
}

static void Outer()
{
    try
    {
        Middle();
    }
    finally
    {
        Console.WriteLine(""outer finally"");
    }
}

try
{
    Outer();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "leaf\nmiddle finally\nouter finally\ncross-frame failure",
            output.Replace("\r\n", "\n"));
    }

    [Test]
    public void ExceptionFiltersRunInOrderAndThrownFilterExceptionsContinueSearch()
    {
        var csharp = @"
static bool RecordFilter(string name, bool result)
{
    Console.WriteLine(name);
    return result;
}

static bool ThrowingFilter()
{
    Console.WriteLine(""throwing filter"");
    throw new Exception(""filter failure"");
}

try
{
    throw new InvalidOperationException(""original"");
}
catch (Exception) when (RecordFilter(""false filter"", false))
{
    Console.WriteLine(""wrong false handler"");
}
catch (Exception) when (ThrowingFilter())
{
    Console.WriteLine(""wrong throwing handler"");
}
catch (Exception ex) when (RecordFilter(""true filter"", true))
{
    Console.WriteLine(ex.Message);
}
finally
{
    Console.WriteLine(""finally"");
}

try
{
    throw new Exception(""typed fallback"");
}
catch (InvalidOperationException) when (RecordFilter(""wrong type filter"", true))
{
    Console.WriteLine(""wrong type handler"");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

try
{
    try
    {
        throw new Exception(""nested"");
    }
    finally
    {
        Console.WriteLine(""inner finally"");
    }
}
catch (Exception) when (RecordFilter(""outer filter"", true))
{
    Console.WriteLine(""outer handler"");
}

try
{
    try
    {
        throw new Exception(""filtered rethrow"");
    }
    catch (Exception) when (RecordFilter(""rethrow filter"", true))
    {
        throw;
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
";
        var output = CompileAndRun(csharp);
        Assert.Equal(
            "false filter\nthrowing filter\ntrue filter\noriginal\nfinally\n"
            + "typed fallback\nouter filter\ninner finally\nouter handler\n"
            + "rethrow filter\nfiltered rethrow",
            output.Replace("\r\n", "\n"));
    }
}
