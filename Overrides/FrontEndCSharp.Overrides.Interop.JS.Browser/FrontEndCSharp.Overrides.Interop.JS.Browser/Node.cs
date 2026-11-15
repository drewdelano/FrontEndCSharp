using System.Runtime.CompilerServices;

namespace Interop.JS.Browser;

public class Node
{
    internal Node()
    {
    }

    public extern string? TextContent
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
        [MethodImpl(MethodImplOptions.InternalCall)]
        set;
    }

    public extern Node? ParentNode
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public extern Node? FirstChild
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public extern Node? LastChild
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    public extern bool IsConnected
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void AppendChild(Node child);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern Node InsertBefore(Node newNode, Node? referenceNode);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern Node RemoveChild(Node child);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern bool HasChildNodes();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void Remove();
}
