using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Collections.Concurrent;

namespace FrontEndCSharp.SystemCollectionsConcurrentRuntimeOverrides;

[RuntimeOverrideType(typeof(ConcurrentDictionary<,>))]
internal static class ConcurrentDictionaryOverrides<TKey, TValue>
    where TKey : notnull
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    internal static ConcurrentDictionary<TKey, TValue> Create() => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static TValue GetOrAdd<TArg>(
        ConcurrentDictionary<TKey, TValue> @this,
        TKey key,
        Func<TKey, TArg, TValue> valueFactory,
        TArg factoryArgument) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static TValue GetOrAdd(
        ConcurrentDictionary<TKey, TValue> @this,
        TKey key,
        Func<TKey, TValue> valueFactory) => throw null!;
}
