using FrontEndCSharp.RuntimeOverrides;
using Interop.JS.React;
using MobXReactApi = Interop.JS.MobX.React.MobXReact;
using System;
using System.Runtime.CompilerServices;

[assembly: RuntimeOverrideAssembly("FrontEndCSharp.Overrides.Interop.JS.MobX.React")]
[assembly: JavaScriptPackageDependency("mobx-react-lite", "5.1.0")]
[assembly: JavaScriptPackageDependency("mobx", "7.0.6", Kind = JavaScriptPackageDependencyKind.PeerDependency)]
[assembly: JavaScriptPackageDependency("react", "19.3.1", Kind = JavaScriptPackageDependencyKind.PeerDependency)]
[assembly: JavaScriptPackageDependency("react-dom", "19.3.1", Kind = JavaScriptPackageDependencyKind.PeerDependency)]

namespace Interop.JS.MobX.ReactRuntimeOverrides;

[RuntimeOverrideType(typeof(MobXReactApi))]
internal static class MobXReactOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ReactElement Observe(Func<ReactNode> render);

    [JavaScriptOverride]
    internal static ReactComponent<TProps> Observer<TProps>(ReactComponent<TProps> component)
        where TProps : class =>
        throw null!;
}
