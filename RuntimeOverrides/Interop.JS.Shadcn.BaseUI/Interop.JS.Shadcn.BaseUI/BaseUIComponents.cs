using System;
using Interop.JS.React;
using ReactApi = Interop.JS.React.React;

namespace Interop.JS.Shadcn.BaseUI;

public static class BaseUI
{
    public sealed class ElementProps
    {
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

    public static class Button
    {
        public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
            CreateElement(BaseUIHandles.Button(), configure, children);

        public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
            CreateElement(BaseUIHandles.Button(), props, children);
    }

    public static class Input
    {
        public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
            CreateElement(BaseUIHandles.Input(), configure, children);

        public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
            CreateElement(BaseUIHandles.Input(), props, children);
    }

    public static class Separator
    {
        public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
            CreateElement(BaseUIHandles.Separator(), configure, children);

        public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
            CreateElement(BaseUIHandles.Separator(), props, children);
    }

    public static class Combobox
    {
        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxRoot(), props, children);
        }

        public static class Value
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxValue(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxValue(), props, children);
        }

        public static class Trigger
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxTrigger(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxTrigger(), props, children);
        }

        public static class Clear
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxClear(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxClear(), props, children);
        }

        public static class Input
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxInput(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxInput(), props, children);
        }

        public static class Portal
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxPortal(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxPortal(), props, children);
        }

        public static class Positioner
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxPositioner(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxPositioner(), props, children);
        }

        public static class Popup
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxPopup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxPopup(), props, children);
        }

        public static class List
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxList(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxList(), props, children);
        }

        public static class Item
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxItem(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxItem(), props, children);
        }

        public static class ItemIndicator
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxItemIndicator(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxItemIndicator(), props, children);
        }

        public static class Group
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxGroup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxGroup(), props, children);
        }

        public static class GroupLabel
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxGroupLabel(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxGroupLabel(), props, children);
        }

        public static class Collection
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxCollection(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxCollection(), props, children);
        }

        public static class Empty
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxEmpty(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxEmpty(), props, children);
        }

        public static class Separator
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxSeparator(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxSeparator(), props, children);
        }

        public static class Chips
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxChips(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxChips(), props, children);
        }

        public static class Chip
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxChip(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxChip(), props, children);
        }

        public static class ChipRemove
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxChipRemove(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ComboboxChipRemove(), props, children);
        }
    }

    public static class Progress
    {
        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressRoot(), props, children);
        }

        public static class Track
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressTrack(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressTrack(), props, children);
        }

        public static class Indicator
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressIndicator(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressIndicator(), props, children);
        }

        public static class Label
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressLabel(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressLabel(), props, children);
        }

        public static class Value
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressValue(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ProgressValue(), props, children);
        }
    }

    public static class Checkbox
    {
        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.CheckboxRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.CheckboxRoot(), props, children);
        }

        public static class Indicator
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.CheckboxIndicator(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.CheckboxIndicator(), props, children);
        }
    }

    public static class Avatar
    {
        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AvatarRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AvatarRoot(), props, children);
        }

        public static class Image
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AvatarImage(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AvatarImage(), props, children);
        }

        public static class Fallback
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AvatarFallback(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AvatarFallback(), props, children);
        }
    }

    public static class Accordion
    {
        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionRoot(), props, children);
        }

        public static class Item
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionItem(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionItem(), props, children);
        }

        public static class Header
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionHeader(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionHeader(), props, children);
        }

        public static class Trigger
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionTrigger(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionTrigger(), props, children);
        }

        public static class Panel
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionPanel(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.AccordionPanel(), props, children);
        }
    }

    public static class Select
    {
        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectRoot(), props, children);
        }

        public static class Group
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectGroup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectGroup(), props, children);
        }

        public static class Value
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectValue(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectValue(), props, children);
        }

        public static class Trigger
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectTrigger(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectTrigger(), props, children);
        }

        public static class Icon
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectIcon(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectIcon(), props, children);
        }

        public static class Portal
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectPortal(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectPortal(), props, children);
        }

        public static class Positioner
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectPositioner(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectPositioner(), props, children);
        }

        public static class Popup
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectPopup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectPopup(), props, children);
        }

        public static class List
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectList(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectList(), props, children);
        }

        public static class GroupLabel
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectGroupLabel(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectGroupLabel(), props, children);
        }

        public static class Item
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectItem(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectItem(), props, children);
        }

        public static class ItemText
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectItemText(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectItemText(), props, children);
        }

        public static class ItemIndicator
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectItemIndicator(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectItemIndicator(), props, children);
        }

        public static class Separator
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectSeparator(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectSeparator(), props, children);
        }

        public static class ScrollUpArrow
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectScrollUpArrow(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectScrollUpArrow(), props, children);
        }

        public static class ScrollDownArrow
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectScrollDownArrow(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.SelectScrollDownArrow(), props, children);
        }
    }

    public static class Tooltip
    {
        public static class Provider
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipProvider(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipProvider(), props, children);
        }

        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipRoot(), props, children);
        }

        public static class Trigger
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipTrigger(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipTrigger(), props, children);
        }

        public static class Portal
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipPortal(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipPortal(), props, children);
        }

        public static class Positioner
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipPositioner(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipPositioner(), props, children);
        }

        public static class Popup
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipPopup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipPopup(), props, children);
        }

        public static class Arrow
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipArrow(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.TooltipArrow(), props, children);
        }
    }

    public static class Dialog
    {
        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogRoot(), props, children);
        }

        public static class Trigger
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogTrigger(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogTrigger(), props, children);
        }

        public static class Portal
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogPortal(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogPortal(), props, children);
        }

        public static class Close
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogClose(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogClose(), props, children);
        }

        public static class Backdrop
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogBackdrop(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogBackdrop(), props, children);
        }

        public static class Popup
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogPopup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogPopup(), props, children);
        }

        public static class Title
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogTitle(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogTitle(), props, children);
        }

        public static class Description
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogDescription(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DialogDescription(), props, children);
        }
    }

    public static class Drawer
    {
        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerRoot(), props, children);
        }

        public static class Trigger
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerTrigger(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerTrigger(), props, children);
        }

        public static class Portal
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerPortal(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerPortal(), props, children);
        }

        public static class Close
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerClose(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerClose(), props, children);
        }

        public static class Backdrop
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerBackdrop(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerBackdrop(), props, children);
        }

        public static class Viewport
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerViewport(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerViewport(), props, children);
        }

        public static class Popup
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerPopup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerPopup(), props, children);
        }

        public static class Content
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerContent(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerContent(), props, children);
        }

        public static class Title
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerTitle(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerTitle(), props, children);
        }

        public static class Description
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerDescription(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.DrawerDescription(), props, children);
        }
    }

    public static class Menu
    {
        public static class Root
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuRoot(), props, children);
        }

        public static class Portal
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuPortal(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuPortal(), props, children);
        }

        public static class Trigger
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuTrigger(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuTrigger(), props, children);
        }

        public static class Positioner
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuPositioner(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuPositioner(), props, children);
        }

        public static class Popup
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuPopup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuPopup(), props, children);
        }

        public static class Group
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuGroup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuGroup(), props, children);
        }

        public static class GroupLabel
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuGroupLabel(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuGroupLabel(), props, children);
        }

        public static class Item
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuItem(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuItem(), props, children);
        }

        public static class SubmenuRoot
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuSubmenuRoot(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuSubmenuRoot(), props, children);
        }

        public static class SubmenuTrigger
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuSubmenuTrigger(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuSubmenuTrigger(), props, children);
        }

        public static class CheckboxItem
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuCheckboxItem(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuCheckboxItem(), props, children);
        }

        public static class CheckboxItemIndicator
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuCheckboxItemIndicator(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuCheckboxItemIndicator(), props, children);
        }

        public static class RadioGroup
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuRadioGroup(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuRadioGroup(), props, children);
        }

        public static class RadioItem
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuRadioItem(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuRadioItem(), props, children);
        }

        public static class RadioItemIndicator
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuRadioItemIndicator(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuRadioItemIndicator(), props, children);
        }

        public static class Separator
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuSeparator(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.MenuSeparator(), props, children);
        }
    }

    public static class Toast
    {
        public static class Host
        {
            public static ReactElement Create(Action<ElementProps> configure, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ToastHost(), configure, children);

            public static ReactElement Create(ReactProps? props, params ReactNode[] children) =>
                CreateElement(BaseUIHandles.ToastHost(), props, children);
        }

        public static void Add(string title, string description, string type) =>
            BaseUIHandles.AddToast(title, description, type);
    }
}
