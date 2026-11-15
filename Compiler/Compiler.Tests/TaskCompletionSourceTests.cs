using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Compiler.Helpers;
using Compiler.Read;

namespace Compiler.Tests;

public class TaskCompletionSourceTests
{
    private static readonly RuntimeOverrideCollection RuntimeOverrides = RuntimeOverrideCollection.Load([
        Path.Combine(AppContext.BaseDirectory, "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.dll")
    ]);

    [Test]
    public void LongBranchTargetsStartAtInstructionBoundaries()
    {
        var asyncStateMachine = typeof(TaskCompletionSourceTests)
            .GetMethod(nameof(MethodWithMultipleAwaitStates), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetCustomAttribute<AsyncStateMachineAttribute>()!
            .StateMachineType;
        var moveNext = asyncStateMachine.GetMethod(
            nameof(IAsyncStateMachine.MoveNext),
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
        var decoder = new MethodDecoder();

        decoder.LoadMethod(moveNext);

        Assert.All(
            decoder.InstructionBlocks.Where(block => block.Instructions.Count > 0),
            block => Assert.Equal(block.ILOffset, block.Instructions[0].ILOffset));
    }

    [Test]
    public void AsyncBuilderOverrideIncludesStateMachineMoveNextDependency()
    {
        var startMethod = typeof(AsyncTaskMethodBuilder)
            .GetMethods()
            .Single(method => method.Name == nameof(AsyncTaskMethodBuilder.Start));

        var dependencies = RuntimeOverrides.GetDependencies(startMethod);

        Assert.Contains(
            dependencies,
            method => method.DeclaringType == typeof(IAsyncStateMachine)
                && method.Name == nameof(IAsyncStateMachine.MoveNext));
    }

    [Test]
    public void ExecutedAsyncStateMachineMethodsDoNotReferenceSetStateMachine()
    {
        var asyncMethod = typeof(TaskCompletionSourceTests)
            .GetMethod(nameof(MethodWithMultipleAwaitStates), BindingFlags.NonPublic | BindingFlags.Static)!;
        var stateMachineType = asyncMethod
            .GetCustomAttribute<AsyncStateMachineAttribute>()!
            .StateMachineType;
        var moveNext = stateMachineType.GetMethod(
            nameof(IAsyncStateMachine.MoveNext),
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;

        foreach (var executedMethod in new[] { asyncMethod, moveNext })
        {
            var decoder = new MethodDecoder();
            decoder.LoadMethod(executedMethod);

            Assert.DoesNotContain(
                decoder.ReferencedMethods.Keys,
                method => method.Name == nameof(IAsyncStateMachine.SetStateMachine));
        }

        var builderSetStateMachineMethods = new[]
        {
            typeof(AsyncTaskMethodBuilder).GetMethod(nameof(AsyncTaskMethodBuilder.SetStateMachine))!,
            typeof(AsyncTaskMethodBuilder<>).GetMethod(nameof(AsyncTaskMethodBuilder<int>.SetStateMachine))!
        };
        Assert.All(
            builderSetStateMachineMethods,
            method => Assert.False(
                RuntimeOverrides.HasRuntimeOverride(method),
                $"SetStateMachine should remain on the managed MSIL path: {method}."));
    }

    [Test]
    public void TaskDelayOverrideIncludesArgumentOutOfRangeExceptionDependency()
    {
        var delayMethod = typeof(Task).GetMethod(nameof(Task.Delay), [typeof(int)]);

        Assert.NotNull(delayMethod);
        var dependencies = RuntimeOverrides.GetDependencies(delayMethod);

        Assert.Contains(
            dependencies,
            method => method.DeclaringType == typeof(ArgumentOutOfRangeException)
                && method.IsConstructor
                && method.GetParameters().Select(parameter => parameter.ParameterType)
                    .SequenceEqual([typeof(string)]));
    }

    [Test]
    public void GenericTaskRuntimeOverridesResolveFromOpenGenericDefinitions()
    {
        var builderType = typeof(AsyncTaskMethodBuilder<int>);
        var builderMethods = new MethodBase[]
        {
            builderType.GetMethod(nameof(AsyncTaskMethodBuilder<int>.Create))!,
            builderType.GetProperty(nameof(AsyncTaskMethodBuilder<int>.Task))!.GetMethod!,
            builderType.GetMethod(nameof(AsyncTaskMethodBuilder<int>.SetResult))!,
            builderType.GetMethod(nameof(AsyncTaskMethodBuilder<int>.SetException), [typeof(Exception)])!,
            builderType.GetMethods().Single(method => method.Name == nameof(AsyncTaskMethodBuilder<int>.Start)),
            builderType.GetMethods().Single(method => method.Name == nameof(AsyncTaskMethodBuilder<int>.AwaitOnCompleted)
                && method.GetParameters().Length == 2),
            builderType.GetMethods().Single(method => method.Name == nameof(AsyncTaskMethodBuilder<int>.AwaitUnsafeOnCompleted)
                && method.GetParameters().Length == 2)
        };

        var awaiterType = typeof(TaskAwaiter<int>);
        var awaiterMethods = new MethodBase[]
        {
            typeof(Task<int>).GetMethod(nameof(Task<int>.GetAwaiter))!,
            awaiterType.GetProperty(nameof(TaskAwaiter<int>.IsCompleted))!.GetMethod!,
            awaiterType.GetMethod(nameof(TaskAwaiter<int>.GetResult))!,
            awaiterType.GetMethod(nameof(TaskAwaiter<int>.OnCompleted), [typeof(Action)])!,
            awaiterType.GetMethod(nameof(TaskAwaiter<int>.UnsafeOnCompleted), [typeof(Action)])!
        };

        Assert.All(builderMethods.Concat(awaiterMethods), method =>
            Assert.True(RuntimeOverrides.HasRuntimeOverride(method), $"Missing runtime override for {method}."));

        var openBuilderMethods = new MethodBase[]
        {
            typeof(AsyncTaskMethodBuilder<>).GetMethod(nameof(AsyncTaskMethodBuilder<int>.Create))!,
            typeof(AsyncTaskMethodBuilder<>).GetProperty(nameof(AsyncTaskMethodBuilder<int>.Task))!.GetMethod!,
            typeof(AsyncTaskMethodBuilder<>).GetMethod(nameof(AsyncTaskMethodBuilder<int>.SetResult))!
        };

        Assert.All(openBuilderMethods, method =>
            Assert.True(RuntimeOverrides.HasRuntimeOverride(method), $"Missing runtime override for {method}."));
        Assert.Equal(
            GetExpectedModuleFileName(
                "method",
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].Create()"),
            NameNormalizers.GetMethodModuleFileName(openBuilderMethods[0]));
        Assert.Equal(
            GetExpectedRuntimeOverrideFileName(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].Create()"),
            RuntimeOverrides.GetRuntimeOverrideFileName(openBuilderMethods[0]));
        Assert.Equal(
            GetExpectedModuleFileName(
                "type",
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult]"),
            NameNormalizers.GetTypeModuleFileName(typeof(AsyncTaskMethodBuilder<>)));
    }

    [Test]
    public void EveryPublicContinueWithOverloadHasRuntimeOverride()
    {
        var continuationMethods = new[] { typeof(Task), typeof(Task<>) }
            .SelectMany(type => type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.Name == nameof(Task.ContinueWith))
            .ToList();
        var missingSignatures = continuationMethods
            .Where(method => !RuntimeOverrides.HasRuntimeOverride(method))
            .Select(NameNormalizers.GetFullyQualifiedMethodName)
            .OrderBy(signature => signature)
            .ToList();

        Assert.NotEmpty(continuationMethods);
        Assert.True(
            missingSignatures.Count == 0,
            $"Missing runtime overrides:{Environment.NewLine}{string.Join(Environment.NewLine, missingSignatures)}");
    }

    [Test]
    public void RuntimeOverrideFileNamesAreDerivedFromCanonicalMethodSignatures()
    {
        var delayMethod = typeof(Task).GetMethod(nameof(Task.Delay), [typeof(int)])!;
        var signature = NameNormalizers.GetFullyQualifiedMethodName(delayMethod);

        Assert.Equal(
            GetExpectedRuntimeOverrideFileName(signature),
            RuntimeOverrides.GetRuntimeOverrideFileName(delayMethod));
    }

    [Test]
    public void NonGenericAwaiterSchedulingOverridesUseActualBclMethodNames()
    {
        var schedulingMethods = new MethodBase?[]
        {
            typeof(TaskAwaiter).GetMethod(nameof(TaskAwaiter.OnCompleted), [typeof(Action)]),
            typeof(TaskAwaiter).GetMethod(nameof(TaskAwaiter.UnsafeOnCompleted), [typeof(Action)]),
            typeof(YieldAwaitable.YieldAwaiter).GetMethod(
                nameof(YieldAwaitable.YieldAwaiter.OnCompleted),
                [typeof(Action)]),
            typeof(YieldAwaitable.YieldAwaiter).GetMethod(
                nameof(YieldAwaitable.YieldAwaiter.UnsafeOnCompleted),
                [typeof(Action)])
        };

        Assert.All(schedulingMethods, method =>
        {
            Assert.NotNull(method);
            Assert.True(RuntimeOverrides.HasRuntimeOverride(method));
        });
    }

    private static string GetExpectedRuntimeOverrideFileName(string signature) =>
        GetExpectedModuleFileName("method", signature);

    private static string GetExpectedModuleFileName(string prefix, string identity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"{prefix}.{Convert.ToHexString(hash).ToLowerInvariant()}.js";
    }

    private static async Task<string> MethodWithMultipleAwaitStates()
    {
        await Task.Delay(1);
        await Task.Yield();
        return "complete";
    }
}
