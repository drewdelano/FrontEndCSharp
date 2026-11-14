using Interop.JS.React;
using System;

namespace Interop.JS.MobX.React;

public static class MobXReact
{
    public static ReactElement Observe(Func<ReactNode> render) =>
        throw new PlatformNotSupportedException("MobX React integration is available only in FrontEndCSharp browser output.");

    public static ReactComponent<TProps> Observer<TProps>(ReactComponent<TProps> component)
        where TProps : class =>
        throw new PlatformNotSupportedException("MobX React integration is available only in FrontEndCSharp browser output.");
}
