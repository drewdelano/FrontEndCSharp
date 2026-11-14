using System;
using System.Runtime.CompilerServices;

namespace Interop.JS.MobX;

public enum MobXActionEnforcement
{
    Observed,
    Never,
    Always,
}

public sealed class ObservableBox<T>
{
    private ObservableBox()
    {
    }

    public T Value
    {
        get => throw new PlatformNotSupportedException("MobX is available only in FrontEndCSharp browser output.");
        set => throw new PlatformNotSupportedException("MobX is available only in FrontEndCSharp browser output.");
    }
}

public sealed class Reaction : IDisposable
{
    private Reaction()
    {
    }

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void Dispose();
}

public static class MobX
{
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern void Configure(MobXActionEnforcement actionEnforcement);

    public static T MakeAutoObservable<T>(T target)
        where T : class =>
        throw new PlatformNotSupportedException("MobX is available only in FrontEndCSharp browser output.");

    public static ObservableBox<T> Box<T>(T initialValue) =>
        throw new PlatformNotSupportedException("MobX is available only in FrontEndCSharp browser output.");

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern Reaction Autorun(Action effect);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern void RunInAction(Action action);
}
