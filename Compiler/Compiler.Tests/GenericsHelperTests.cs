using Compiler.DTOs;
using Compiler.Helpers;
using System.Reflection;

namespace Compiler.Tests;

public class GenericsHelperTests
{
    [Test]
    public void ConstructedTypeMapPreservesEveryNestedGenericArgument()
    {
        var map = GenericsHelper.GetGenericTypeMap(
            typeof(Dictionary<string, List<int?>>));

        Assert.Equal("System.String", map!["TKey"]);
        var list = Assert.IsType<ManagedTypeReference>(map["TValue"]);
        Assert.Equal("System.Collections.Generic.List`1[T]", list.TypeName);
        var nullable = Assert.IsType<ManagedTypeReference>(list.GenericParameters["T"]);
        Assert.Equal("System.Nullable`1[T]", nullable.TypeName);
        Assert.Equal("System.Int32", nullable.GenericParameters["T"]);
    }

    [Test]
    public void ConstructedMethodMapUsesTheSameRecursiveTypeReferences()
    {
        var definition = typeof(GenericsHelperTests)
            .GetMethod(nameof(GenericMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        var method = definition.MakeGenericMethod(typeof(Dictionary<string, List<int?>>));

        var map = GenericsHelper.GetGenericMethodMap(method);

        var dictionary = Assert.IsType<ManagedTypeReference>(map!["T"]);
        Assert.Equal("System.Collections.Generic.Dictionary`2[TKey, TValue]", dictionary.TypeName);
        Assert.Equal("System.String", dictionary.GenericParameters["TKey"]);
        var list = Assert.IsType<ManagedTypeReference>(dictionary.GenericParameters["TValue"]);
        var nullable = Assert.IsType<ManagedTypeReference>(list.GenericParameters["T"]);
        Assert.Equal("System.Int32", nullable.GenericParameters["T"]);
    }

    [Test]
    public void ArrayReferencesPreserveElementGenericsKindRankAndShape()
    {
        var integers = Assert.IsType<ManagedTypeReference>(
            GenericsHelper.GetTypeReference(typeof(List<int>[])));
        var strings = Assert.IsType<ManagedTypeReference>(
            GenericsHelper.GetTypeReference(typeof(List<string>[])));
        var rectangular = Assert.IsType<ManagedTypeReference>(
            GenericsHelper.GetTypeReference(typeof(List<int>[,])));
        var variableBound = Assert.IsType<ManagedTypeReference>(
            GenericsHelper.GetTypeReference(typeof(List<int>).MakeArrayType(1)));

        Assert.Equal("szarray", integers.ArrayKind);
        Assert.Equal(1, integers.ArrayRank);
        Assert.Empty(integers.ArraySizes);
        Assert.Empty(integers.ArrayLowerBounds);
        Assert.Equal("array", rectangular.ArrayKind);
        Assert.Equal(2, rectangular.ArrayRank);
        Assert.Equal("array", variableBound.ArrayKind);
        Assert.Equal(1, variableBound.ArrayRank);

        var integerElement = Assert.IsType<ManagedTypeReference>(integers.ArrayElementType);
        var stringElement = Assert.IsType<ManagedTypeReference>(strings.ArrayElementType);
        Assert.Equal("System.Int32", integerElement.GenericParameters["T"]);
        Assert.Equal("System.String", stringElement.GenericParameters["T"]);
    }

    [Test]
    public void ArrayReferencesRemainRecursiveWhenJaggedOrNestedInAGenericType()
    {
        var jagged = Assert.IsType<ManagedTypeReference>(
            GenericsHelper.GetTypeReference(typeof(List<int>[][])));
        var innerArray = Assert.IsType<ManagedTypeReference>(jagged.ArrayElementType);
        var list = Assert.IsType<ManagedTypeReference>(innerArray.ArrayElementType);
        Assert.Equal("szarray", jagged.ArrayKind);
        Assert.Equal("szarray", innerArray.ArrayKind);
        Assert.Equal("System.Int32", list.GenericParameters["T"]);

        var container = Assert.IsType<ManagedTypeReference>(
            GenericsHelper.GetTypeReference(typeof(Dictionary<string, List<int>[]>)));
        var genericArray = Assert.IsType<ManagedTypeReference>(
            container.GenericParameters["TValue"]);
        var genericElement = Assert.IsType<ManagedTypeReference>(
            genericArray.ArrayElementType);
        Assert.Equal("System.Int32", genericElement.GenericParameters["T"]);
    }

    private static void GenericMethod<T>()
    {
    }
}
