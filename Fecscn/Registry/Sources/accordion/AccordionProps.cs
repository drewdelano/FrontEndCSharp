using Interop.JS.Browser;
using Interop.JS.React;

namespace Fecscn.Components.UI;

public sealed class AccordionProps
{
    public string? ClassName { get; set; }
    public bool Multiple { get; set; }
    public bool Disabled { get; set; }
}

public sealed class AccordionItemProps
{
    public string? ClassName { get; set; }
    public string? Value { get; set; }
    public bool Disabled { get; set; }
}

public sealed class AccordionTriggerProps
{
    public string? ClassName { get; set; }
    public bool Disabled { get; set; }
    public Action<DomEvent>? OnClick { get; set; }
}

public sealed class AccordionContentProps
{
    public string? ClassName { get; set; }
    public bool KeepMounted { get; set; }
}

public static class AccordionPrimitive
{
    public static ReactProps CreateRootProps(AccordionProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(AccordionStyles.Root, props.ClassName);
        importedProps.SetString("data-slot", "accordion");
        importedProps.SetBoolean("multiple", props.Multiple);
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateItemProps(AccordionItemProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(AccordionStyles.Item, props.ClassName);
        importedProps.SetString("data-slot", "accordion-item");
        importedProps.SetString("value", props.Value);
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreateHeaderProps()
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = AccordionStyles.Header;
        return importedProps;
    }

    public static ReactProps CreateTriggerProps(AccordionTriggerProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = ClassNames.Merge(AccordionStyles.Trigger, props.ClassName);
        importedProps.OnClick = props.OnClick;
        importedProps.SetString("data-slot", "accordion-trigger");
        importedProps.SetBoolean("disabled", props.Disabled);
        return importedProps;
    }

    public static ReactProps CreatePanelProps(AccordionContentProps props)
    {
        var importedProps = ReactProps.Create();
        importedProps.ClassName = AccordionStyles.Panel;
        importedProps.SetString("data-slot", "accordion-content");
        importedProps.SetBoolean("keepMounted", props.KeepMounted);
        return importedProps;
    }
}

public static class AccordionStyles
{
    public const string Root = "flex w-full flex-col";
    public const string Item = "not-last:border-b";
    public const string Header = "flex";
    public const string Trigger = "group/accordion-trigger relative flex flex-1 items-start justify-between rounded-lg border border-transparent py-2.5 text-left text-sm font-medium transition-all outline-none hover:underline focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:after:border-ring aria-disabled:pointer-events-none aria-disabled:opacity-50 **:data-[slot=accordion-trigger-icon]:ml-auto **:data-[slot=accordion-trigger-icon]:size-4 **:data-[slot=accordion-trigger-icon]:text-muted-foreground";
    public const string Panel = "overflow-hidden text-sm data-open:animate-accordion-down data-closed:animate-accordion-up";
    public const string Content = "h-(--accordion-panel-height) pt-0 pb-2.5 data-ending-style:h-0 data-starting-style:h-0 [&_a]:underline [&_a]:underline-offset-3 [&_a]:hover:text-foreground [&_p:not(:last-child)]:mb-4";
    public const string ClosedIcon = "pointer-events-none shrink-0 group-aria-expanded/accordion-trigger:hidden";
    public const string OpenIcon = "pointer-events-none hidden shrink-0 group-aria-expanded/accordion-trigger:inline";
}
