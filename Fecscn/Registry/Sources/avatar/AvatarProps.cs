using Interop.JS.React;

namespace Fecscn.Components.UI;

public enum AvatarSize
{
    Default,
    Small,
    Large
}

public sealed class AvatarProps
{
    public string? ClassName { get; set; }
    public AvatarSize Size { get; set; }
}

public sealed class AvatarImageProps
{
    public string? ClassName { get; set; }
    public string? Src { get; set; }
    public string? Alt { get; set; }
}

public sealed class AvatarPartProps
{
    public string? ClassName { get; set; }
}

public static class AvatarPrimitive
{
    public static ReactProps CreateRootProps(AvatarProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(AvatarStyles.Root, props.ClassName);
        importedProps.SetString("data-slot", "avatar");
        importedProps.SetString("data-size", AvatarStyles.Size(props.Size));
        return importedProps;
    }

    public static ReactProps CreateImageProps(AvatarImageProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(AvatarStyles.Image, props.ClassName);
        importedProps.SetString("data-slot", "avatar-image");
        importedProps.SetString("src", props.Src);
        importedProps.SetString("alt", props.Alt);
        return importedProps;
    }

    public static ReactProps CreateFallbackProps(AvatarPartProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(AvatarStyles.Fallback, props.ClassName);
        importedProps.SetString("data-slot", "avatar-fallback");
        return importedProps;
    }
}

public static class AvatarStyles
{
    public const string Root = "group/avatar relative flex size-8 shrink-0 rounded-full select-none after:absolute after:inset-0 after:rounded-full after:border after:border-border after:mix-blend-darken data-[size=lg]:size-10 data-[size=sm]:size-6 dark:after:mix-blend-lighten";
    public const string Image = "aspect-square size-full rounded-full object-cover";
    public const string Fallback = "flex size-full items-center justify-center rounded-full bg-muted text-sm text-muted-foreground group-data-[size=sm]/avatar:text-xs";
    public const string Badge = "absolute right-0 bottom-0 z-10 inline-flex items-center justify-center rounded-full bg-primary text-primary-foreground bg-blend-color ring-2 ring-background select-none group-data-[size=sm]/avatar:size-2 group-data-[size=sm]/avatar:[&>svg]:hidden group-data-[size=default]/avatar:size-2.5 group-data-[size=default]/avatar:[&>svg]:size-2 group-data-[size=lg]/avatar:size-3 group-data-[size=lg]/avatar:[&>svg]:size-2";
    public const string Group = "group/avatar-group flex -space-x-2 *:data-[slot=avatar]:ring-2 *:data-[slot=avatar]:ring-background";
    public const string GroupCount = "relative flex size-8 shrink-0 items-center justify-center rounded-full bg-muted text-sm text-muted-foreground ring-2 ring-background group-has-data-[size=lg]/avatar-group:size-10 group-has-data-[size=sm]/avatar-group:size-6 [&>svg]:size-4 group-has-data-[size=lg]/avatar-group:[&>svg]:size-5 group-has-data-[size=sm]/avatar-group:[&>svg]:size-3";

    public static string Size(AvatarSize value)
    {
        if (value == AvatarSize.Small) return "sm";
        if (value == AvatarSize.Large) return "lg";
        return "default";
    }
}
