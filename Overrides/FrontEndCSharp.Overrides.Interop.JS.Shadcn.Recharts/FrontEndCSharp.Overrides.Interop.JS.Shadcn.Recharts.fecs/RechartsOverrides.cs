using System.Runtime.CompilerServices;
using Interop.JS.Shadcn.Recharts;
using FrontEndCSharp.RuntimeOverrides;
using Interop.JS.React;

[assembly: RuntimeOverrideAssembly("FrontEndCSharp.Overrides.Interop.JS.Shadcn.Recharts")]
[assembly: JavaScriptPackageDependency("react-is", "19.3.1")]
[assembly: JavaScriptPackageDependency("recharts", "3.8.0")]
[assembly: JavaScriptPackageDependency("react", "19.3.1", Kind = JavaScriptPackageDependencyKind.PeerDependency)]
[assembly: JavaScriptPackageDependency("react-dom", "19.3.1", Kind = JavaScriptPackageDependencyKind.PeerDependency)]

namespace Interop.JS.Shadcn.Recharts.Overrides;

[RuntimeOverrideType(typeof(RechartsImports))]
internal static class RechartsOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsResponsiveContainer();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsBarChart(string[] categories, double[] values, string categoryKey, string dataKey);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsBar();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsCartesianGrid();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsXAxis();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsTooltip();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsLegend();
}
