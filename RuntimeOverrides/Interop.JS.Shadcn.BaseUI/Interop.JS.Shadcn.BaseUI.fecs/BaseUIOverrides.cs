using System.Runtime.CompilerServices;
using Interop.JS.Shadcn.BaseUI;
using FrontEndCSharp.RuntimeOverrides;
using Interop.JS.React;

[assembly: RuntimeOverrideAssembly("Interop.JS.Shadcn.BaseUI")]
[assembly: JavaScriptPackageDependency("@base-ui/react", "1.9.0")]
[assembly: JavaScriptPackageDependency("react", "19.3.1", Kind = JavaScriptPackageDependencyKind.PeerDependency)]
[assembly: JavaScriptPackageDependency("react-dom", "19.3.1", Kind = JavaScriptPackageDependencyKind.PeerDependency)]

namespace Interop.JS.Shadcn.BaseUI.Overrides;

[RuntimeOverrideType(typeof(BaseUiReactImports))]
internal static class BaseUIOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiButton();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiInput();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSeparator();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxValue();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxTrigger();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxClear();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxInput();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxPortal();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxPositioner();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxPopup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxList();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxItem();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxItemIndicator();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxGroup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxGroupLabel();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxCollection();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxEmpty();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxSeparator();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxChips();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxChip();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiComboboxChipRemove();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiProgressRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiProgressTrack();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiProgressIndicator();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiProgressLabel();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiProgressValue();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiCheckboxRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiCheckboxIndicator();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiAvatarRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiAvatarImage();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiAvatarFallback();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiAccordionRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiAccordionItem();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiAccordionHeader();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiAccordionTrigger();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiAccordionPanel();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectGroup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectValue();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectTrigger();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectIcon();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectPortal();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectPositioner();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectPopup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectList();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectGroupLabel();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectItem();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectItemText();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectItemIndicator();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectSeparator();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectScrollUpArrow();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiSelectScrollDownArrow();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiTooltipProvider();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiTooltipRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiTooltipTrigger();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiTooltipPortal();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiTooltipPositioner();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiTooltipPopup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiTooltipArrow();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDialogRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDialogTrigger();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDialogPortal();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDialogClose();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDialogBackdrop();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDialogPopup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDialogTitle();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDialogDescription();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiToastToaster();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void ShowToast(string title, string description, string type);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerTrigger();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerPortal();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerClose();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerBackdrop();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerViewport();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerPopup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerContent();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerTitle();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiDrawerDescription();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuPortal();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuTrigger();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuPositioner();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuPopup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuGroup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuGroupLabel();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuItem();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuSubmenuRoot();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuSubmenuTrigger();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuCheckboxItem();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuCheckboxItemIndicator();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuRadioGroup();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuRadioItem();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuRadioItemIndicator();

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ImportedReactComponent BaseUiMenuSeparator();
}
