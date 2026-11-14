using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public sealed class CheckboxProps
{
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public string? Name { get; set; }
    public string? Value { get; set; }
    public bool? Checked { get; set; }
    public bool? DefaultChecked { get; set; }
    public bool Disabled { get; set; }
    public bool Required { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public static class CheckboxPrimitive
{
    public static ReactProps CreateRootProps(CheckboxProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.AriaLabel = props.AriaLabel;
        importedProps.ClassName = ClassNames.Merge(CheckboxStyles.Root, props.ClassName);
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", "checkbox");
        importedProps.SetString("name", props.Name);
        importedProps.SetString("value", props.Value);
        importedProps.SetBoolean("disabled", props.Disabled);
        importedProps.SetBoolean("required", props.Required);
        if (props.Checked.HasValue)
        {
            importedProps.SetBoolean("checked", props.Checked.Value);
        }
        if (props.DefaultChecked.HasValue)
        {
            importedProps.SetBoolean("defaultChecked", props.DefaultChecked.Value);
        }
        return importedProps;
    }

    public static ReactProps CreateIndicatorProps()
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = CheckboxStyles.Indicator;
        importedProps.SetString("data-slot", "checkbox-indicator");
        return importedProps;
    }
}

public static class CheckboxStyles
{
    public const string Root = "peer relative flex size-4 shrink-0 items-center justify-center rounded-[4px] border border-input transition-colors outline-none group-has-disabled/field:opacity-50 group-has-[:focus-visible]/field-label:ring-0 group-has-[:focus-visible]/field-label:not-data-checked:border-input after:absolute after:-inset-x-3 after:-inset-y-2 focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 aria-invalid:aria-checked:border-primary dark:bg-input/30 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40 data-checked:border-primary data-checked:bg-primary data-checked:text-primary-foreground group-has-[:focus-visible]/field-label:data-checked:border-primary dark:data-checked:bg-primary";
    public const string Indicator = "grid place-content-center text-xs leading-none text-current transition-none";
}
