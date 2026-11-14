using System.Runtime.CompilerServices;

namespace Interop.JS.Browser;

public static class Document
{
    public static extern Element Body
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public static extern Element Head
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public static extern Element DocumentElement
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public static extern Element? ActiveElement
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public static extern string ReadyState
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public static extern string Title
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
        [MethodImpl(MethodImplOptions.InternalCall)]
        set;
    }

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern Element? GetElementById(string id);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern Element? QuerySelector(string selectors);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern Element[] QuerySelectorAll(string selectors);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern Element CreateElement(string tagName);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern Node CreateTextNode(string data);
}
