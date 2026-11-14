using Interop.JS.Browser;
using Interop.JS.React;
using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Runtime.CompilerServices;
using ReactApi = Interop.JS.React.React;

[assembly: RuntimeOverrideAssembly("Interop.JS.React")]
[assembly: JavaScriptPackageDependency("react", "19.3.1")]
[assembly: JavaScriptPackageDependency("react-dom", "19.3.1")]

namespace Interop.JS.ReactRuntimeOverrides;

[RuntimeOverrideType(typeof(ReactApi))]
internal static class ReactOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ReactNode Text(string value);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ReactComponent Component(Func<ReactProps, ReactNode> render);

    [JavaScriptOverride]
    internal static ReactComponent<TProps> Component<TProps>(
        Func<TProps, ReactNode[], ReactNode> render)
        where TProps : class =>
        throw null!;

    [JavaScriptOverride]
    internal static ReactElement CreateElement(
        string elementName,
        ReactProps? props,
        ReactNode[] children)
    {
        if (props != null)
        {
            _ = props.GetPropertyNames();
            _ = props.GetPropertyValue(null!);
        }
        throw null!;
    }

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ReactElement CreateFragment(ReactNode[] children);

    [JavaScriptOverride]
    internal static ReactElement CreateElement(
        ReactComponent component,
        ReactProps? props,
        ReactNode[] children)
    {
        if (props != null)
        {
            _ = props.GetPropertyNames();
            _ = props.GetPropertyValue(null!);
            _ = props.Snapshot();
        }
        throw null!;
    }

    [JavaScriptOverride]
    internal static ReactElement CreateElement(
        ImportedReactComponent component,
        ReactProps? props,
        ReactNode[] children)
    {
        if (props != null)
        {
            _ = props.GetPropertyNames();
            _ = props.GetPropertyValue(null!);
        }
        throw null!;
    }

    [JavaScriptOverride]
    internal static ReactElement CreateElement<TProps>(
        ReactComponent<TProps> component,
        TProps props,
        ReactNode[] children)
        where TProps : class =>
        throw null!;

}

[RuntimeOverrideType(typeof(ReactRoot))]
internal static class ReactRootOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ReactRoot Create(Element container);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Render(ReactRoot root, ReactNode node);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void Unmount(ReactRoot root);
}
