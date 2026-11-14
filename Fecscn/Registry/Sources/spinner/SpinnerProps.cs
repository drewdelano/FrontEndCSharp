namespace Fecscn.Components.UI;

public sealed class SpinnerProps
{
    public string? ClassName { get; set; }
    public string AriaLabel { get; set; } = "Loading";
}

public static class SpinnerStyles
{
    public const string Base = "inline-block size-4 animate-spin rounded-full border-2 border-current border-r-transparent";
}
