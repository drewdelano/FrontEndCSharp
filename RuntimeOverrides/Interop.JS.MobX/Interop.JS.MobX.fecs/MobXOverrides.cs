using Interop.JS.MobX;
using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Runtime.CompilerServices;
using MobXApi = Interop.JS.MobX.MobX;

[assembly: RuntimeOverrideAssembly("Interop.JS.MobX")]
[assembly: JavaScriptPackageDependency("mobx", "7.0.6")]

namespace Interop.JS.MobXRuntimeOverrides;

[RuntimeOverrideType(typeof(ObservableBox<>))]
internal static class ObservableBoxOverrides<T>
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static T get_Value(ObservableBox<T> box) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static void set_Value(ObservableBox<T> box, T value) => throw null!;
}

[RuntimeOverrideType(typeof(Reaction))]
internal static class ReactionOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Dispose(Reaction reaction);
}

[RuntimeOverrideType(typeof(MobXApi))]
internal static class MobXOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Configure(MobXActionEnforcement actionEnforcement);

    [JavaScriptOverride]
    internal static T MakeAutoObservable<T>(T target)
        where T : class => throw null!;

    [JavaScriptOverride]
    internal static ObservableBox<T> Box<T>(T initialValue) => throw null!;

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Reaction Autorun(Action effect);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void RunInAction(Action action);
}
