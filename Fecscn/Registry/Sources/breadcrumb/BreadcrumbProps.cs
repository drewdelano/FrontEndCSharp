namespace Fecscn.Components.UI;

public sealed class BreadcrumbProps
{
    public string? ClassName { get; set; }
    public string AriaLabel { get; set; } = "breadcrumb";
}

public sealed class BreadcrumbPartProps
{
    public string? ClassName { get; set; }
}

public sealed class BreadcrumbLinkProps
{
    public string? ClassName { get; set; }
    public string? Href { get; set; }
    public string? Target { get; set; }
    public string? Rel { get; set; }
}

public static class BreadcrumbStyles
{
    public const string List = "flex flex-wrap items-center gap-1.5 text-sm wrap-break-word text-muted-foreground";
    public const string Item = "inline-flex items-center gap-1";
    public const string Link = "transition-colors hover:text-foreground";
    public const string Page = "font-normal text-foreground";
    public const string Separator = "[&>svg]:size-3.5";
    public const string Ellipsis = "flex size-5 items-center justify-center [&>svg]:size-4";
}
