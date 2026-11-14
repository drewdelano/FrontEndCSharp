using System.Runtime.CompilerServices;
using Interop.JS.React;

namespace Interop.JS.Shadcn.Recharts;

internal static class RechartsHandles
{
    internal static ImportedReactComponent ResponsiveContainer() =>
        RechartsImports.RechartsResponsiveContainer();

    internal static ImportedReactComponent BarChart(string[] categories, double[] values, string categoryKey, string dataKey) =>
        RechartsImports.RechartsBarChart(categories, values, categoryKey, dataKey);

    internal static ImportedReactComponent Bar() =>
        RechartsImports.RechartsBar();

    internal static ImportedReactComponent CartesianGrid() =>
        RechartsImports.RechartsCartesianGrid();

    internal static ImportedReactComponent XAxis() =>
        RechartsImports.RechartsXAxis();

    internal static ImportedReactComponent Tooltip() =>
        RechartsImports.RechartsTooltip();

    internal static ImportedReactComponent Legend() =>
        RechartsImports.RechartsLegend();
}

internal static class RechartsImports
{
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsResponsiveContainer();

    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsBarChart(string[] categories, double[] values, string categoryKey, string dataKey);

    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsBar();

    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsCartesianGrid();

    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsXAxis();

    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsTooltip();

    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent RechartsLegend();
}
