using Compiler.Helpers;
using System.Reflection;
using System.Threading.Tasks.Sources;

namespace Compiler.Tests;

public sealed class ManagedIdentityTests
{
    private interface IUntypedValue
    {
        object? Value { get; }
    }

    private sealed class TypedValue<T> : IUntypedValue
    {
        public T? Value => default;

        object? IUntypedValue.Value => Value;
    }

    private sealed class DualValueTaskSource : IValueTaskSource<int>, IValueTaskSource
    {
        int IValueTaskSource<int>.GetResult(short token) => 0;
        ValueTaskSourceStatus IValueTaskSource<int>.GetStatus(short token) => default;
        void IValueTaskSource<int>.OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
        }

        void IValueTaskSource.GetResult(short token)
        {
        }
        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => default;
        void IValueTaskSource.OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
        }
    }

    [Test]
    public void RuntimeTypeNamesUseNamespaceQualifiedNames()
    {
        Assert.Equal(
            "System.Int32",
            NameNormalizers.GetFullTypeName(typeof(int)));
        Assert.Equal(
            "Compiler.Tests.ManagedIdentityTests",
            NameNormalizers.GetFullTypeName(typeof(ManagedIdentityTests)));
    }

    [Test]
    public void RuntimeMethodNamesUseNamespaceQualifiedTypeNames()
    {
        var method = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

        Assert.Equal(
            "System.String.Contains(System.String)",
            NameNormalizers.GetFullyQualifiedMethodName(method));
    }

    [Test]
    public void ByRefRuntimeTypeNamesPreserveConstructedGenericIdentity()
    {
        var type = typeof(IEnumerable<string>).MakeByRefType();

        Assert.Equal(
            "System.Collections.Generic.IEnumerable`1[T]&",
            NameNormalizers.GetFullTypeName(type));
    }

    [Test]
    public void ExplicitInterfaceMethodsRemainDistinctWhenSimpleSignaturesCollide()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var getters = typeof(TypedValue<>).GetMethods(flags)
            .Where(method => method.Name.EndsWith("get_Value", StringComparison.Ordinal))
            .Select(NameNormalizers.GetFullyQualifiedMethodName)
            .ToArray();

        Assert.Equal(2, getters.Length);
        Assert.Equal(2, getters.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(getters, name => name.Contains(
            "IUntypedValue.get_Value()",
            StringComparison.Ordinal));
    }

    [Test]
    public void GenericAndNonGenericExplicitInterfacesKeepDistinctRuntimeNames()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var methods = typeof(DualValueTaskSource).GetMethods(flags)
            .Where(method => method.Name.EndsWith("GetStatus", StringComparison.Ordinal))
            .Select(NameNormalizers.GetFullyQualifiedMethodName)
            .ToArray();

        Assert.Equal(2, methods.Length);
        Assert.Equal(2, methods.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(methods, name => name.Contains(
            "IValueTaskSource.GetStatus(System.Int16)",
            StringComparison.Ordinal));
        Assert.Contains(methods, name => name.Contains(
            "IValueTaskSource`1[TResult].GetStatus(System.Int16)",
            StringComparison.Ordinal));
    }
}
