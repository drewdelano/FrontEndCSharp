using System;
using System.Runtime.CompilerServices;

namespace Interop.JS.Browser;

public sealed class Element : Node
{
    private Element()
    {
    }

    public extern string Id
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
        [MethodImpl(MethodImplOptions.InternalCall)]
        set;
    }

    public extern string ClassName
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
        [MethodImpl(MethodImplOptions.InternalCall)]
        set;
    }

    public extern string TagName
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public extern string InnerHtml
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
        [MethodImpl(MethodImplOptions.InternalCall)]
        set;
    }

    public extern string InnerText
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
        [MethodImpl(MethodImplOptions.InternalCall)]
        set;
    }

    public extern string Value
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void SetAttribute(string name, string value);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern string? GetAttribute(string name);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern bool HasAttribute(string name);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void RemoveAttribute(string name);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern Element? QuerySelector(string selectors);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void Focus();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void Blur();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void Click();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void AddEventListener(string type, Action<DomEvent> listener);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void RemoveEventListener(string type, Action<DomEvent> listener);
}
