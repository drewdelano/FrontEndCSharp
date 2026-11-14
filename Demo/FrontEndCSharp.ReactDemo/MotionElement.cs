using Interop.JS.Motion;
using Interop.JS.React;

namespace FrontEndCSharp.ReactDemo;

public static class MotionElement
{
    public static ReactElement Create(Action<MotionElementProps> configure, params ReactNode[] children)
    {
        var props = new MotionElementProps();
        configure(props);
        return Motion.Element(props.ElementName, props, children);
    }
}
