using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public sealed class InputProps
{
    public string? Id { get; set; }
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public string? Name { get; set; }
    public string? Placeholder { get; set; }
    public string Type { get; set; } = "text";
    public string? Value { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnChange { get; set; }
    public Action<DomEvent>? OnInput { get; set; }
}

public static class InputPrimitive
{
    private const string BaseClasses = "h-8 w-full min-w-0 rounded-lg border border-input bg-transparent px-2.5 py-1 text-base transition-colors outline-none file:inline-flex file:h-6 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:pointer-events-none disabled:cursor-not-allowed disabled:bg-input/50 disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 md:text-sm dark:bg-input/30 dark:disabled:bg-input/80 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40";

    public static ReactProps CreateProps(InputProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.Id = props.Id;
        importedProps.AriaLabel = props.AriaLabel;
        importedProps.Type = props.Type;
        importedProps.ClassName = ClassNames.Merge(BaseClasses, props.ClassName);
        importedProps.OnChange = props.OnChange;
        importedProps.OnInput = props.OnInput;
        importedProps.SetString("name", props.Name);
        importedProps.SetString("value", props.Value);
        importedProps.SetString("placeholder", props.Placeholder);
        importedProps.SetBoolean("disabled", props.Disabled);
        importedProps.SetString("data-slot", "input");
        return importedProps;
    }
}
