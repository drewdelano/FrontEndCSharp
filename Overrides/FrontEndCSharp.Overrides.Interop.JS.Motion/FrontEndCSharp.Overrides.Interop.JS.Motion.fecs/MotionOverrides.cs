using Interop.JS.Motion;
using Interop.JS.React;
using FrontEndCSharp.RuntimeOverrides;
using System.Runtime.CompilerServices;
using MotionApi = Interop.JS.Motion.Motion;

[assembly: RuntimeOverrideAssembly("FrontEndCSharp.Overrides.Interop.JS.Motion")]
[assembly: JavaScriptPackageDependency("motion", "14.4.0")]
[assembly: JavaScriptPackageDependency("react", "19.3.1", Kind = JavaScriptPackageDependencyKind.PeerDependency)]
[assembly: JavaScriptPackageDependency("react-dom", "19.3.1", Kind = JavaScriptPackageDependencyKind.PeerDependency)]

namespace Interop.JS.MotionRuntimeOverrides;

[RuntimeOverrideType(typeof(MotionStyle))]
internal static class MotionStyleOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MotionStyle Create();

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MotionStyle SetNumber(MotionStyle style, string name, double value);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MotionStyle SetString(MotionStyle style, string name, string value);
}

[RuntimeOverrideType(typeof(MotionTransition))]
internal static class MotionTransitionOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MotionTransition Create();

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MotionTransition SetType(MotionTransition transition, string type);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MotionTransition SetDuration(MotionTransition transition, double seconds);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MotionTransition SetDelay(MotionTransition transition, double seconds);
}

[RuntimeOverrideType(typeof(MotionApi))]
internal static class MotionOverrides
{
    [JavaScriptOverride]
    internal static ReactElement Element(
        string elementName,
        MotionProps? props,
        ReactNode[] children)
    {
        if (props != null)
        {
            _ = props.GetPropertyNames();
            _ = props.GetPropertyValue(null!);
        }
        throw null!;
    }
}
