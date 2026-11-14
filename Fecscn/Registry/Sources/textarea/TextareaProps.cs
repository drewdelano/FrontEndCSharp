using Interop.JS.Browser;

namespace Fecscn.Components.UI;

public sealed class TextareaProps
{
    public string? Id { get; set; }
    public string? AriaLabel { get; set; }
    public string? ClassName { get; set; }
    public string? Placeholder { get; set; }
    public string? Value { get; set; }
    public bool Disabled { get; set; }
    public bool ReadOnly { get; set; }
    public bool Required { get; set; }
    public Action<DomEvent>? OnChange { get; set; }
    public Action<DomEvent>? OnInput { get; set; }
}

public static class TextareaStyles
{
    public const string Base = "flex field-sizing-content min-h-16 w-full rounded-lg border border-input bg-transparent px-2.5 py-2 text-base transition-colors outline-none placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:bg-input/50 disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 md:text-sm dark:bg-input/30 dark:disabled:bg-input/80 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40";
}
