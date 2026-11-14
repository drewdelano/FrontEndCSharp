using Interop.JS.React;

namespace Fecscn.Components.UI;

public sealed class ProgressProps
{
    public string? ClassName { get; set; }
    public double? Value { get; set; }
}

public sealed class ProgressPartProps
{
    public string? ClassName { get; set; }
}

public static class ProgressPrimitive
{
    public static ReactProps CreateRootProps(ProgressProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(ProgressStyles.Root, props.ClassName);
        importedProps.SetString("data-slot", "progress");
        if (props.Value.HasValue)
        {
            importedProps.SetNumber("value", props.Value.Value);
        }
        return importedProps;
    }

    public static ReactProps CreateTrackProps(ProgressPartProps? props) =>
        CreatePartProps("progress-track", ProgressStyles.Track, props);

    public static ReactProps CreateIndicatorProps(ProgressPartProps? props) =>
        CreatePartProps("progress-indicator", ProgressStyles.Indicator, props);

    public static ReactProps CreateLabelProps(ProgressPartProps? props) =>
        CreatePartProps("progress-label", ProgressStyles.Label, props);

    public static ReactProps CreateValueProps(ProgressPartProps? props) =>
        CreatePartProps("progress-value", ProgressStyles.Value, props);

    private static ReactProps CreatePartProps(string slot, string baseClasses, ProgressPartProps? props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(baseClasses, props?.ClassName);
        importedProps.SetString("data-slot", slot);
        return importedProps;
    }
}

public static class ProgressStyles
{
    public const string Root = "flex flex-wrap gap-3";
    public const string Track = "relative flex h-1 w-full items-center overflow-x-hidden rounded-full bg-muted";
    public const string Indicator = "h-full bg-primary transition-all";
    public const string Label = "text-sm font-medium";
    public const string Value = "ml-auto text-sm text-muted-foreground tabular-nums";
}
