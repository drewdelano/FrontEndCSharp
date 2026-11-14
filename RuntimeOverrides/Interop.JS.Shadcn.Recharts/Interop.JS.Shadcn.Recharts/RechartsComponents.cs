using System;
using Interop.JS.React;
using ReactApi = Interop.JS.React.React;

namespace Interop.JS.Shadcn.Recharts;

public static class Recharts
{
    public sealed class ElementProps
    {
        public ReactProps? Props { get; set; }
    }

    public sealed class BarChartElementProps
    {
        public string[] Categories { get; set; } = [];
        public double[] Values { get; set; } = [];
        public string CategoryKey { get; set; } = string.Empty;
        public string DataKey { get; set; } = string.Empty;
        public ReactProps? Props { get; set; }
    }

    private static ReactElement CreateElement(
        ImportedReactComponent component,
        Action<ElementProps> configure,
        ReactNode[] children)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var elementProps = new ElementProps();
        configure(elementProps);
        return ReactApi.CreateElement(component, elementProps.Props, children);
    }

    private static ReactElement CreateElement(
        ImportedReactComponent component,
        ReactProps? props,
        ReactNode[] children) =>
        ReactApi.CreateElement(component, props, children);

    public static class ResponsiveContainer
    {
        public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
            CreateElement(RechartsHandles.ResponsiveContainer(), configure, children);

        public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
            CreateElement(RechartsHandles.ResponsiveContainer(), props, children);
    }

    public static class BarChart
    {
        public static ReactElement Create(Action<BarChartElementProps> configure, params ReactNode[] children)
        {
            ArgumentNullException.ThrowIfNull(configure);
            var elementProps = new BarChartElementProps();
            configure(elementProps);
            return ReactApi.CreateElement(
                RechartsHandles.BarChart(
                    elementProps.Categories,
                    elementProps.Values,
                    elementProps.CategoryKey,
                    elementProps.DataKey),
                elementProps.Props,
                children);
        }

        public static ReactElement Create(
            string[] categories,
            double[] values,
            string categoryKey,
            string dataKey,
            ReactProps? props,
            params ReactNode[] children) =>
            ReactApi.CreateElement(
                RechartsHandles.BarChart(categories, values, categoryKey, dataKey),
                props,
                children);
    }

    public static class Bar
    {
        public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
            CreateElement(RechartsHandles.Bar(), configure, children);

        public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
            CreateElement(RechartsHandles.Bar(), props, children);
    }

    public static class CartesianGrid
    {
        public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
            CreateElement(RechartsHandles.CartesianGrid(), configure, children);

        public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
            CreateElement(RechartsHandles.CartesianGrid(), props, children);
    }

    public static class XAxis
    {
        public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
            CreateElement(RechartsHandles.XAxis(), configure, children);

        public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
            CreateElement(RechartsHandles.XAxis(), props, children);
    }

    public static class Tooltip
    {
        public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
            CreateElement(RechartsHandles.Tooltip(), configure, children);

        public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
            CreateElement(RechartsHandles.Tooltip(), props, children);
    }

    public static class Legend
    {
        public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
            CreateElement(RechartsHandles.Legend(), configure, children);

        public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
            CreateElement(RechartsHandles.Legend(), props, children);
    }
}
