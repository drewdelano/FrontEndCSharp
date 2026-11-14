using System.Runtime.CompilerServices;
using Interop.JS.Shadcn.Cn;
using FrontEndCSharp.RuntimeOverrides;

[assembly: RuntimeOverrideAssembly("Interop.JS.Shadcn.Cn")]
[assembly: JavaScriptPackageDependency("cn", "0.5.2")]

namespace Interop.JS.Shadcn.Cn.Overrides;

[RuntimeOverrideType(typeof(CnImports))]
internal static class CnOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern string MergeClassNames(string[] values);
}
