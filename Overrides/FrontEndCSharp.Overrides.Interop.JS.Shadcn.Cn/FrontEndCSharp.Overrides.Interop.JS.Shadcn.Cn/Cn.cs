using System.Runtime.CompilerServices;

namespace Interop.JS.Shadcn.Cn;

public static class ClassNames
{
    public static string Merge(string[] values) =>
        CnImports.MergeClassNames(values);
}

internal static class CnImports
{
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern string MergeClassNames(string[] values);
}
