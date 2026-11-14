global using FrontEndCSharp.Testing;
global using Assert = FrontEndCSharp.Testing.LegacyAssert;

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: FrontEndCSharp.Testing.TestDuration]
[assembly: LevelOfParallelism(4)]
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]

namespace FrontEndCSharp.Testing;

public sealed class TestCaseDataCollection<T1> : List<object[]>
{
    public void Add(T1 value1) => Add([value1!]);
}

public sealed class TestCaseDataCollection<T1, T2> : List<object[]>
{
    public void Add(T1 value1, T2 value2) => Add([value1!, value2!]);
}

public sealed class TestCaseDataCollection<T1, T2, T3> : List<object[]>
{
    public void Add(T1 value1, T2 value2, T3 value3) => Add([value1!, value2!, value3!]);
}

public sealed class TestCaseDataCollection<T1, T2, T3, T4> : List<object[]>
{
    public void Add(T1 value1, T2 value2, T3 value3, T4 value4) => Add([value1!, value2!, value3!, value4!]);
}

internal sealed class TestDurationAttribute : TestActionAttribute
{
    internal const int SlowMilliseconds = 5_000;
    internal const int MaximumMilliseconds = 10_000;

    private readonly ConcurrentDictionary<string, long> _startedAt = new(StringComparer.Ordinal);

    public override ActionTargets Targets => ActionTargets.Test;

    public override void BeforeTest(ITest test)
    {
        _startedAt[test.Id] = Stopwatch.GetTimestamp();
    }

    public override void AfterTest(ITest test)
    {
        if (!_startedAt.TryRemove(test.Id, out var startedAt))
            throw new AssertionException($"The duration wrapper did not record a start time for '{test.FullName}'.");

        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        var classification = elapsed.TotalMilliseconds < SlowMilliseconds ? "normal" : "slow";
        TestContext.Progress.WriteLine(
            $"[test-duration] {elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} ms " +
            $"[{classification}] {test.FullName}");

        if (elapsed.TotalMilliseconds >= MaximumMilliseconds)
        {
            throw new AssertionException(
                $"Test '{test.FullName}' took {elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} ms; " +
                $"the maximum is {MaximumMilliseconds} ms and the slow bar is {SlowMilliseconds} ms.");
        }
    }
}

// Keep the assertion behavior stable while the repository changes test runners.
// Test discovery, parameterization, execution, reporting, and lifecycle are NUnit-owned.
internal static class LegacyAssert
{
    public static void True(bool condition, string? userMessage = null)
    {
        NUnit.Framework.Assert.That(condition, Is.True, userMessage);
    }

    public static void False(bool condition, string? userMessage = null)
    {
        NUnit.Framework.Assert.That(condition, Is.False, userMessage);
    }

    public static void Null(object? value)
    {
        NUnit.Framework.Assert.That(value, Is.Null);
    }

    public static void NotNull(object? value)
    {
        NUnit.Framework.Assert.That(value, Is.Not.Null);
    }

    public static void Equal<T>(T expected, T actual)
    {
        NUnit.Framework.Assert.That(actual, Is.EqualTo(expected));
    }

    public static void Equal<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        NUnit.Framework.Assert.That(actual, Is.EqualTo(expected));
    }

    public static void Equal(
        string? expected,
        string? actual,
        bool ignoreCase = false,
        bool ignoreLineEndingDifferences = false,
        bool ignoreWhiteSpaceDifferences = false)
    {
        if (ignoreLineEndingDifferences)
        {
            expected = NormalizeLineEndings(expected);
            actual = NormalizeLineEndings(actual);
        }

        if (ignoreWhiteSpaceDifferences)
        {
            expected = NormalizeWhitespace(expected);
            actual = NormalizeWhitespace(actual);
        }

        if (ignoreCase)
            NUnit.Framework.Assert.That(actual, Is.EqualTo(expected).IgnoreCase);
        else
            NUnit.Framework.Assert.That(actual, Is.EqualTo(expected));
    }

    public static void NotEqual<T>(T notExpected, T actual)
    {
        NUnit.Framework.Assert.That(actual, Is.Not.EqualTo(notExpected));
    }

    public static void Same(object? expected, object? actual)
    {
        NUnit.Framework.Assert.That(actual, Is.SameAs(expected));
    }

    public static void NotSame(object? expected, object? actual)
    {
        NUnit.Framework.Assert.That(actual, Is.Not.SameAs(expected));
    }

    public static void Empty(IEnumerable collection)
    {
        NUnit.Framework.Assert.That(collection, Is.Empty);
    }

    public static void NotEmpty(IEnumerable collection)
    {
        NUnit.Framework.Assert.That(collection, Is.Not.Empty);
    }

    public static void Contains(string expectedSubstring, string? actualString)
    {
        NUnit.Framework.Assert.That(actualString, Does.Contain(expectedSubstring));
    }

    public static void Contains(string expectedSubstring, string? actualString, StringComparison comparisonType)
    {
        NUnit.Framework.Assert.That(
            actualString != null && actualString.Contains(expectedSubstring, comparisonType),
            Is.True,
            $"Expected string to contain '{expectedSubstring}' using {comparisonType}, but was '{actualString}'.");
    }

    public static void Contains<T>(T expected, IEnumerable<T> collection)
    {
        NUnit.Framework.Assert.That(collection, Has.Member(expected));
    }

    public static void Contains<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer)
    {
        NUnit.Framework.Assert.That(
            collection.Contains(expected, comparer),
            Is.True,
            $"The collection did not contain the expected value '{expected}'.");
    }

    public static void Contains<T>(IEnumerable<T> collection, Predicate<T> filter)
    {
        NUnit.Framework.Assert.That(collection.Any(item => filter(item)), Is.True, "No collection item matched the predicate.");
    }

    public static void DoesNotContain(string expectedSubstring, string? actualString)
    {
        NUnit.Framework.Assert.That(actualString, Does.Not.Contain(expectedSubstring));
    }

    public static void DoesNotContain(string expectedSubstring, string? actualString, StringComparison comparisonType)
    {
        NUnit.Framework.Assert.That(
            actualString == null || !actualString.Contains(expectedSubstring, comparisonType),
            Is.True,
            $"Expected string not to contain '{expectedSubstring}' using {comparisonType}, but was '{actualString}'.");
    }

    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
    {
        NUnit.Framework.Assert.That(collection, Has.No.Member(expected));
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> filter)
    {
        NUnit.Framework.Assert.That(collection.Any(item => filter(item)), Is.False, "A collection item matched the predicate.");
    }

    public static T Single<T>(IEnumerable<T> collection)
    {
        var items = collection.Take(2).ToArray();
        NUnit.Framework.Assert.That(items, Has.Length.EqualTo(1));
        return items[0];
    }

    public static T Single<T>(IEnumerable<T> collection, Predicate<T> filter)
    {
        return Single(collection.Where(item => filter(item)));
    }

    public static void All<T>(IEnumerable<T> collection, Action<T> action)
    {
        var index = 0;
        foreach (var item in collection)
        {
            try
            {
                action(item);
            }
            catch (Exception exception)
            {
                throw new AssertionException($"Assertion failed for collection item at index {index}.", exception);
            }

            index++;
        }
    }

    public static void Collection<T>(IEnumerable<T> collection, params Action<T>[] elementInspectors)
    {
        var items = collection.ToArray();
        NUnit.Framework.Assert.That(items, Has.Length.EqualTo(elementInspectors.Length));
        for (var index = 0; index < items.Length; index++)
            elementInspectors[index](items[index]);
    }

    public static T IsType<T>(object? value)
    {
        NUnit.Framework.Assert.That(value, Is.TypeOf<T>());
        return (T)value!;
    }

    public static T IsAssignableFrom<T>(object? value)
    {
        NUnit.Framework.Assert.That(value, Is.InstanceOf<T>());
        return (T)value!;
    }

    public static T Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            NUnit.Framework.Assert.That(exception, Is.TypeOf<T>());
            return (T)exception;
        }

        throw new AssertionException($"Expected an exception of type {typeof(T).FullName}, but no exception was thrown.");
    }

    public static Exception Throws(Type exceptionType, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            NUnit.Framework.Assert.That(exception.GetType(), Is.EqualTo(exceptionType));
            return exception;
        }

        throw new AssertionException($"Expected an exception of type {exceptionType.FullName}, but no exception was thrown.");
    }

    public static async Task<T> ThrowsAsync<T>(Func<Task> action)
        where T : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            NUnit.Framework.Assert.That(exception, Is.TypeOf<T>());
            return (T)exception;
        }

        throw new AssertionException($"Expected an exception of type {typeof(T).FullName}, but no exception was thrown.");
    }

    public static void InRange<T>(T actual, T low, T high)
        where T : IComparable<T>
    {
        NUnit.Framework.Assert.That(actual, Is.InRange(low, high));
    }

    public static void StartsWith(string expectedStartString, string? actualString)
    {
        NUnit.Framework.Assert.That(actualString, Does.StartWith(expectedStartString));
    }

    public static void StartsWith(
        string expectedStartString,
        string? actualString,
        StringComparison comparisonType)
    {
        NUnit.Framework.Assert.That(
            actualString != null && actualString.StartsWith(expectedStartString, comparisonType),
            Is.True,
            $"Expected string to start with '{expectedStartString}' using {comparisonType}, but was '{actualString}'.");
    }

    public static void EndsWith(string expectedEndString, string? actualString)
    {
        NUnit.Framework.Assert.That(actualString, Does.EndWith(expectedEndString));
    }

    public static void EndsWith(
        string expectedEndString,
        string? actualString,
        StringComparison comparisonType)
    {
        NUnit.Framework.Assert.That(
            actualString != null && actualString.EndsWith(expectedEndString, comparisonType),
            Is.True,
            $"Expected string to end with '{expectedEndString}' using {comparisonType}, but was '{actualString}'.");
    }

    public static void Matches(string expectedRegexPattern, string? actualString)
    {
        NUnit.Framework.Assert.That(actualString, Does.Match(expectedRegexPattern));
    }

    public static void Matches(Regex expectedRegex, string? actualString)
    {
        NUnit.Framework.Assert.That(actualString, Does.Match(expectedRegex));
    }

    public static void DoesNotMatch(string expectedRegexPattern, string? actualString)
    {
        NUnit.Framework.Assert.That(actualString, Does.Not.Match(expectedRegexPattern));
    }

    public static void DoesNotMatch(Regex expectedRegex, string? actualString)
    {
        NUnit.Framework.Assert.That(actualString, Does.Not.Match(expectedRegex));
    }

    public static void Fail(string? message = null)
    {
        NUnit.Framework.Assert.Fail(message ?? "Assertion failed.");
    }

    private static string? NormalizeLineEndings(string? value)
    {
        return value?.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static string? NormalizeWhitespace(string? value)
    {
        return value == null ? null : Regex.Replace(value, "\\s+", " ");
    }
}
