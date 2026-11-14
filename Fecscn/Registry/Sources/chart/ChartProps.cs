using Interop.JS.Shadcn.Recharts;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public sealed class ChartDataPoint
{
    public string Category { get; set; } = string.Empty;
    public double Value { get; set; }
}

public sealed class ChartProps
{
    public string? Id { get; set; }
    public string AriaLabel { get; set; } = "Chart";
    public string? ClassName { get; set; }
    public ChartDataPoint[] Data { get; set; } = [];
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 200;
    public string CategoryKey { get; set; } = "Category";
    public string DataKey { get; set; } = "Value";
    public string SeriesLabel { get; set; } = "Value";
    public string Color { get; set; } = "hsl(221 83% 53%)";
    public bool ShowGrid { get; set; } = true;
    public bool ShowTooltip { get; set; } = true;
    public bool ShowLegend { get; set; } = true;
}

public sealed class ChartContainerProps
{
    public string? Id { get; set; }
    public string AriaLabel { get; set; } = "Chart";
    public string? ClassName { get; set; }
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 200;
}

public sealed class ChartBarChartProps
{
    public ChartDataPoint[] Data { get; set; } = [];
    public string CategoryKey { get; set; } = "Category";
    public string DataKey { get; set; } = "Value";
}

public sealed class ChartBarProps
{
    public string DataKey { get; set; } = "Value";
    public string Name { get; set; } = "Value";
    public string Color { get; set; } = "hsl(221 83% 53%)";
    public double Radius { get; set; } = 4;
    public bool IsAnimationActive { get; set; }
}

public sealed class ChartGridProps
{
    public bool Vertical { get; set; }
}

public sealed class ChartXAxisProps
{
    public string DataKey { get; set; } = "Category";
    public bool Hide { get; set; }
    public bool TickLine { get; set; }
    public bool AxisLine { get; set; }
    public double TickMargin { get; set; } = 8;
}

public sealed class ChartPartProps
{
    public string? ClassName { get; set; }
}

public static class ChartPrimitive
{
    public static ReactNode CreateDefaultChart(ChartProps props)
    {
        var barChartProps = new ChartBarChartProps
        {
            Data = props.Data,
            CategoryKey = props.CategoryKey,
            DataKey = props.DataKey
        };
        var barChart = Recharts.BarChart.Create(
            CreateCategories(barChartProps),
            CreateValues(barChartProps),
            barChartProps.CategoryKey,
            barChartProps.DataKey,
            CreateBarChartProps(barChartProps),
            CreateGrid(props.ShowGrid),
            Recharts.XAxis.Create(
                CreateXAxisProps(new ChartXAxisProps { DataKey = props.CategoryKey })),
            CreateTooltip(props.ShowTooltip),
            CreateLegend(props.ShowLegend),
            Recharts.Bar.Create(
                CreateBarProps(new ChartBarProps
                {
                    DataKey = props.DataKey,
                    Name = props.SeriesLabel,
                    Color = props.Color
                })));
        return Recharts.ResponsiveContainer.Create(
            CreateResponsiveProps(props.Width, props.Height),
            barChart);
    }

    public static string[] CreateCategories(ChartBarChartProps props)
    {
        var categories = new string[props.Data.Length];
        for (var index = 0; index < props.Data.Length; index++)
        {
            categories[index] = props.Data[index].Category;
        }
        return categories;
    }

    public static double[] CreateValues(ChartBarChartProps props)
    {
        var values = new double[props.Data.Length];
        for (var index = 0; index < props.Data.Length; index++)
        {
            values[index] = props.Data[index].Value;
        }
        return values;
    }

    public static ReactProps CreateResponsiveProps(double width, double height)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetNumber("width", width);
        importedProps.SetNumber("height", height);
        return importedProps;
    }

    public static ReactProps CreateBarChartProps(ChartBarChartProps props)
    {
        return ReactProps.Create();
    }

    public static ReactProps CreateBarProps(ChartBarProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetString("dataKey", props.DataKey);
        importedProps.SetString("name", props.Name);
        importedProps.SetString("fill", props.Color);
        importedProps.SetNumber("radius", props.Radius);
        importedProps.SetBoolean("isAnimationActive", props.IsAnimationActive);
        return importedProps;
    }

    public static ReactProps CreateGridProps(ChartGridProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetBoolean("vertical", props.Vertical);
        return importedProps;
    }

    public static ReactProps CreateXAxisProps(ChartXAxisProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.SetString("dataKey", props.DataKey);
        importedProps.SetBoolean("hide", props.Hide);
        importedProps.SetBoolean("tickLine", props.TickLine);
        importedProps.SetBoolean("axisLine", props.AxisLine);
        importedProps.SetNumber("tickMargin", props.TickMargin);
        return importedProps;
    }

    public static ReactProps CreateTooltipProps(ChartPartProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = props.ClassName;
        importedProps.SetBoolean("isAnimationActive", false);
        return importedProps;
    }

    public static ReactProps CreateLegendProps(ChartPartProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = props.ClassName;
        return importedProps;
    }

    private static ReactNode CreateGrid(bool show) =>
        show
            ? Recharts.CartesianGrid.Create(CreateGridProps(new ChartGridProps()))
            : React.CreateFragment();

    private static ReactNode CreateTooltip(bool show) =>
        show
            ? Recharts.Tooltip.Create(CreateTooltipProps(new ChartPartProps()))
            : React.CreateFragment();

    private static ReactNode CreateLegend(bool show) =>
        show
            ? Recharts.Legend.Create(CreateLegendProps(new ChartPartProps()))
            : React.CreateFragment();
}

public static class ChartStyles
{
    public const string Container = "flex aspect-video justify-center text-xs [&_.recharts-cartesian-axis-tick_text]:fill-muted-foreground [&_.recharts-cartesian-grid_line[stroke='#ccc']]:stroke-border/50 [&_.recharts-curve.recharts-tooltip-cursor]:stroke-border [&_.recharts-dot[stroke='#fff']]:stroke-transparent [&_.recharts-layer]:outline-hidden [&_.recharts-polar-grid_[stroke='#ccc']]:stroke-border [&_.recharts-radial-bar-background-sector]:fill-muted [&_.recharts-rectangle.recharts-tooltip-cursor]:fill-muted [&_.recharts-reference-line_[stroke='#ccc']]:stroke-border [&_.recharts-sector]:outline-hidden [&_.recharts-sector[stroke='#fff']]:stroke-transparent [&_.recharts-surface]:outline-hidden";
}
