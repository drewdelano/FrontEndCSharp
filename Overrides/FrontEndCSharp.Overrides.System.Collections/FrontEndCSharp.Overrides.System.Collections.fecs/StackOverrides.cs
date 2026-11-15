using FrontEndCSharp.RuntimeOverrides;
using System.Collections.Generic;

[assembly: RuntimeOverrideAssembly("System.Collections")]

namespace FrontEndCSharp.SystemCollectionsRuntimeOverrides;

[RuntimeOverrideType(typeof(Stack<>))]
internal static class StackOverrides<T>
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    internal static Stack<T> Create() => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static void Push(Stack<T> @this, T item) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static T Pop(Stack<T> @this) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_Count")]
    internal static int Count(Stack<T> @this) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static Stack<T>.Enumerator GetEnumerator(Stack<T> @this)
    {
        var enumerator = default(Stack<T>.Enumerator);
        _ = enumerator.MoveNext();
        _ = enumerator.Current;
        enumerator.Dispose();
        return enumerator;
    }
}

[RuntimeOverrideType(typeof(Stack<>.Enumerator))]
internal static class StackEnumeratorOverrides<T>
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static bool MoveNext(Stack<T>.Enumerator @this) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_Current")]
    internal static T Current(Stack<T>.Enumerator @this) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static void Dispose(Stack<T>.Enumerator @this) => throw null!;
}
