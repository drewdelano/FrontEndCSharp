using FrontEndCSharp.RuntimeOverrides;
using System.Collections.Generic;
using System.Linq;

[assembly: RuntimeOverrideAssembly("System.Linq")]

namespace FrontEndCSharp.SystemLinqRuntimeOverrides;

[RuntimeOverrideType(typeof(Enumerable))]
internal static class EnumerableOverrides
{
    [JavaScriptOverride]
    internal static IEnumerable<TResult> Select<TSource, TResult>(
        IEnumerable<TSource> source,
        System.Func<TSource, TResult> selector)
    {
        using var enumerator = source.GetEnumerator();
        _ = enumerator.MoveNext();
        _ = enumerator.Current;
        _ = selector(default!);
        throw null!;
    }

    [JavaScriptOverride]
    internal static TSource[] ToArray<TSource>(IEnumerable<TSource> source)
    {
        using var enumerator = source.GetEnumerator();
        _ = enumerator.MoveNext();
        _ = enumerator.Current;
        var setEnumerator = new HashSet<TSource>().GetEnumerator();
        _ = setEnumerator.MoveNext();
        _ = setEnumerator.Current;
        setEnumerator.Dispose();
        throw null!;
    }

    [JavaScriptOverride]
    internal static List<TSource> ToList<TSource>(IEnumerable<TSource> source)
    {
        using var enumerator = source.GetEnumerator();
        _ = enumerator.MoveNext();
        _ = enumerator.Current;
        throw null!;
    }
}
