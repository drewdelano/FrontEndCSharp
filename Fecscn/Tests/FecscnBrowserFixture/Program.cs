using Fecscn.Components.UI;
using Interop.JS.Browser;
using Interop.JS.React;

var clicks = 0;
var inputs = 0;
var textareaInputs = 0;
var checkboxClicks = 0;
var accordionClicks = 0;
var selectClicks = 0;
var dialogOpens = 0;
var dialogCloses = 0;
var sheetOpens = 0;
var sheetCloses = 0;
var toastShows = 0;
var drawerOpens = 0;
var drawerCloses = 0;
var dropdownClicks = 0;
var comboboxClicks = 0;
var inputValue = string.Empty;
var textareaValue = string.Empty;

void UpdateStatus()
{
    if (clicks == 1
        && inputs == 1
        && textareaInputs == 1
        && checkboxClicks == 1
        && accordionClicks == 1
        && selectClicks == 1
        && dialogOpens == 1
        && dialogCloses == 1
        && sheetOpens == 1
        && sheetCloses == 1
        && toastShows == 1
        && drawerOpens == 1
        && drawerCloses == 1
        && dropdownClicks == 1
        && comboboxClicks == 1
        && inputValue == "changed"
        && textareaValue == "expanded")
    {
        Document.Body.SetAttribute("data-fecscn-status", "ready");
    }
}

var button = Button.Create(props =>
{
    props.ClassName = "h-12 custom-button";
    props.Variant = ButtonVariant.Outline;
    props.Size = ButtonSize.Large;
    props.OnClick = _ =>
    {
        clicks++;
        UpdateStatus();
    };
}, React.Text("Managed button child"));

var disabledButton = Button.Create(props =>
{
    props.ClassName = "disabled-button";
    props.Disabled = true;
}, React.Text("Disabled"));

var input = Input.Create(props =>
{
    props.ClassName = "h-10 custom-input";
    props.Value = "initial";
    props.Name = "managed-name";
    props.Placeholder = "Managed input";
    props.OnInput = domEvent =>
    {
        inputs++;
        inputValue = domEvent.CurrentTarget?.Value ?? string.Empty;
        UpdateStatus();
    };
});

var textarea = Textarea.Create(props =>
{
    props.AriaLabel = "Managed notes";
    props.ClassName = "min-h-24 custom-textarea";
    props.Value = "initial notes";
    props.Placeholder = "Managed textarea";
    props.Required = true;
    props.OnInput = domEvent =>
    {
        textareaInputs++;
        textareaValue = domEvent.CurrentTarget?.Value ?? string.Empty;
        UpdateStatus();
    };
});

var spinner = Spinner.Create(props =>
{
    props.AriaLabel = "Managed loading";
    props.ClassName = "size-6 browser-spinner";
});

var breadcrumb = Breadcrumb.Create(
    props =>
    {
        props.AriaLabel = "Managed breadcrumb";
        props.ClassName = "browser-breadcrumb";
    },
    Breadcrumb.List.Create(
        props => props.ClassName = "browser-breadcrumb-list",
        Breadcrumb.Item.Create(
            _ => { },
            Breadcrumb.Link.Create(
                props => props.Href = "/managed-home",
                React.Text("Home"))),
        Breadcrumb.Separator.Create(_ => { }),
        Breadcrumb.Item.Create(_ => { }, Breadcrumb.Ellipsis.Create(_ => { })),
        Breadcrumb.Separator.Create(_ => { }),
        Breadcrumb.Item.Create(
            _ => { },
            Breadcrumb.Page.Create(_ => { }, React.Text("Current")))));

var progress = Progress.Create(
    props =>
    {
        props.ClassName = "browser-progress";
        props.Value = 65;
    },
    Progress.Label.Create(
        props => props.ClassName = "browser-progress-label",
        React.Text("Managed progress")),
    Progress.Value.Create(props => props.ClassName = "browser-progress-value"));

var checkbox = Checkbox.Create(props =>
{
    props.AriaLabel = "Managed checkbox";
    props.ClassName = "size-5 browser-checkbox";
    props.DefaultChecked = false;
    props.OnClick = _ =>
    {
        checkboxClicks++;
        UpdateStatus();
    };
});

var avatarGroup = Avatar.Group.Create(
    props => props.ClassName = "browser-avatar-group",
    Avatar.Create(
        props =>
        {
            props.ClassName = "browser-avatar";
            props.Size = AvatarSize.Large;
        },
        Avatar.Image.Create(props =>
        {
            props.ClassName = "browser-avatar-image";
            props.Src = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg'/%3E";
            props.Alt = "Managed avatar";
        }),
        Avatar.Badge.Create(_ => { })),
    Avatar.Create(
        props => props.Size = AvatarSize.Small,
        Avatar.Fallback.Create(
            props => props.ClassName = "browser-avatar-fallback",
            React.Text("DD"))),
    Avatar.GroupCount.Create(_ => { }, React.Text("+2")));

var accordion = Accordion.Create(
    props => props.ClassName = "browser-accordion",
    Accordion.Item.Create(
        props =>
        {
            props.ClassName = "browser-accordion-item";
            props.Value = "managed-item";
        },
        Accordion.Trigger.Create(
            props =>
            {
                props.ClassName = "browser-accordion-trigger";
                props.OnClick = _ =>
                {
                    accordionClicks++;
                    UpdateStatus();
                };
            },
            React.Text("Managed accordion trigger")),
        Accordion.Content.Create(
            props => props.ClassName = "browser-accordion-content",
            React.Text("Managed accordion content"))),
    Accordion.Item.Create(
        props =>
        {
            props.Value = "disabled-item";
            props.Disabled = true;
        },
        Accordion.Trigger.Create(_ => { }, React.Text("Disabled accordion trigger")),
        Accordion.Content.Create(_ => { }, React.Text("Disabled accordion content"))));

var select = Select.Create(
    props =>
    {
        props.DefaultValue = "alpha";
        props.Name = "managed-select";
    },
    Select.Trigger.Create(
        props =>
        {
            props.AriaLabel = "Managed select";
            props.ClassName = "browser-select-trigger";
            props.Size = SelectTriggerSize.Small;
        },
        Select.Value.Create(props => props.Placeholder = "Choose a value")),
    Select.Content.Create(
        props => props.ClassName = "browser-select-content",
        Select.Group.Create(
            props => props.ClassName = "browser-select-group",
            Select.Label.Create(_ => { }, React.Text("Managed choices")),
            Select.Item.Create(
                props => props.Value = "alpha",
                React.Text("Alpha")),
            Select.Separator.Create(_ => { }),
            Select.Item.Create(
                props =>
                {
                    props.ClassName = "browser-select-item";
                    props.Value = "beta";
                    props.OnClick = _ =>
                    {
                        selectClicks++;
                        UpdateStatus();
                    };
                },
                React.Text("Beta")))));

var tooltip = Tooltip.Create(
    _ => { },
    Tooltip.Trigger.Create(
        props =>
        {
            props.AriaLabel = "Managed tooltip trigger";
            props.ClassName = "browser-tooltip-trigger";
        },
        React.Text("Tooltip target")),
    Tooltip.Content.Create(
        props =>
        {
            props.ClassName = "browser-tooltip-content";
            props.Side = TooltipSide.Bottom;
        },
        React.Text("Managed tooltip content")));

var dialog = Dialog.Create(
    _ => { },
    Dialog.Trigger.Create(
        props =>
        {
            props.ClassName = "browser-dialog-trigger";
            props.OnClick = _ =>
            {
                dialogOpens++;
                UpdateStatus();
            };
        },
        React.Text("Open managed dialog")),
    Dialog.Content.Create(
        props => props.ClassName = "browser-dialog-content",
        Dialog.Header.Create(
            props => props.ClassName = "browser-dialog-header",
            Dialog.Title.Create(_ => { }, React.Text("Managed dialog title")),
            Dialog.Description.Create(_ => { }, React.Text("Managed dialog description"))),
        Dialog.Footer.Create(
            props => props.ClassName = "browser-dialog-footer",
            Dialog.Close.Create(
                props =>
                {
                    props.ClassName = "browser-dialog-close";
                    props.OnClick = _ =>
                    {
                        dialogCloses++;
                        UpdateStatus();
                    };
                },
                React.Text("Managed close")))));

var sheet = Sheet.Create(
    _ => { },
    Sheet.Trigger.Create(
        props =>
        {
            props.ClassName = "browser-sheet-trigger";
            props.OnClick = _ =>
            {
                sheetOpens++;
                UpdateStatus();
            };
        },
        React.Text("Open managed sheet")),
    Sheet.Content.Create(
        props =>
        {
            props.ClassName = "browser-sheet-content";
            props.Side = SheetSide.Left;
        },
        Sheet.Header.Create(
            props => props.ClassName = "browser-sheet-header",
            Sheet.Title.Create(_ => { }, React.Text("Managed sheet title")),
            Sheet.Description.Create(_ => { }, React.Text("Managed sheet description"))),
        Sheet.Footer.Create(
            props => props.ClassName = "browser-sheet-footer",
            Sheet.Close.Create(
                props =>
                {
                    props.ClassName = "browser-sheet-close";
                    props.OnClick = _ =>
                    {
                        sheetCloses++;
                        UpdateStatus();
                    };
                },
                React.Text("Close managed sheet")))));

var showToastButton = Button.Create(
    props =>
    {
        props.ClassName = "browser-toast-trigger";
        props.Variant = ButtonVariant.Secondary;
        props.OnClick = _ =>
        {
            toastShows++;
            ToastNotifications.Show(
                "Managed toast title",
                "Managed toast description",
                ToastType.Success);
            UpdateStatus();
        };
    },
    React.Text("Show managed toast"));

var toastHost = Toast.Create(props =>
{
    props.ClassName = "browser-toast-viewport";
    props.Timeout = 10000;
});

var drawer = Drawer.Create(
    props => props.Direction = DrawerDirection.Down,
    Drawer.Trigger.Create(
        props =>
        {
            props.ClassName = "browser-drawer-trigger";
            props.OnClick = _ =>
            {
                drawerOpens++;
                UpdateStatus();
            };
        },
        React.Text("Open managed drawer")),
    Drawer.Content.Create(
        props =>
        {
            props.ClassName = "browser-drawer-popup";
            props.Direction = DrawerDirection.Down;
            props.ShowSwipeHandle = true;
        },
        Drawer.Header.Create(
            props => props.ClassName = "browser-drawer-header",
            Drawer.Title.Create(_ => { }, React.Text("Managed drawer title")),
            Drawer.Description.Create(_ => { }, React.Text("Managed drawer description"))),
        Drawer.Footer.Create(
            props => props.ClassName = "browser-drawer-footer",
            Drawer.Close.Create(
                props =>
                {
                    props.ClassName = "browser-drawer-close";
                    props.OnClick = _ =>
                    {
                        drawerCloses++;
                        UpdateStatus();
                    };
                },
                React.Text("Close managed drawer")))));

var dropdownMenu = DropdownMenu.Create(
    _ => { },
    DropdownMenu.Trigger.Create(
        props => props.ClassName = "browser-dropdown-trigger",
        React.Text("Open managed menu")),
    DropdownMenu.Content.Create(
        props => props.ClassName = "browser-dropdown-content",
        DropdownMenu.Group.Create(
            _ => { },
            DropdownMenu.Label.Create(_ => { }, React.Text("Managed menu")),
            DropdownMenu.Item.Create(
                props =>
                {
                    props.ClassName = "browser-dropdown-item";
                    props.OnClick = _ =>
                    {
                        dropdownClicks++;
                        UpdateStatus();
                    };
                },
                React.Text("Managed action"),
                DropdownMenu.Shortcut.Create(_ => { }, React.Text("⌘M"))),
            DropdownMenu.CheckboxItem.Create(
                props => props.Checked = true,
                React.Text("Managed checked")),
            DropdownMenu.Separator.Create(_ => { }),
            DropdownMenu.RadioGroup.Create(
                props => props.DefaultValue = "one",
                DropdownMenu.RadioItem.Create(
                    props => props.Value = "one",
                    React.Text("Managed radio"))))));

var buttonGroup = ButtonGroup.Create(
    props =>
    {
        props.ClassName = "browser-button-group";
        props.Orientation = ComponentOrientation.Horizontal;
    },
    Button.Create(
        props => props.Variant = ButtonVariant.Outline,
        React.Text("Previous")),
    ButtonGroup.Separator.Create(
        props => props.ClassName = "browser-button-group-separator"),
    ButtonGroup.Text.Create(
        props => props.ClassName = "browser-button-group-text",
        React.Text("Page 2")),
    Button.Create(
        props => props.Variant = ButtonVariant.Outline,
        React.Text("Next")));

var separator = Separator.Create(props =>
{
    props.ClassName = "browser-separator";
    props.Orientation = ComponentOrientation.Horizontal;
});

var inputGroup = InputGroup.Create(
    props => props.ClassName = "browser-input-group",
    InputGroup.Addon.Create(
        props => props.Align = InputGroupAddonAlign.InlineStart,
        InputGroup.Text.Create(_ => { }, React.Text("https://"))),
    InputGroup.Input.Create(props =>
    {
        props.AriaLabel = "Managed grouped input";
        props.Value = "example.test";
    }),
    InputGroup.Addon.Create(
        props => props.Align = InputGroupAddonAlign.InlineEnd,
        InputGroup.Text.Create(_ => { }, React.Text(".dev"))));

var combobox = Combobox.Create(
    props =>
    {
        props.DefaultValue = "apple";
        props.Name = "managed-combobox";
    },
    Combobox.Input.Create(props =>
    {
        props.AriaLabel = "Managed combobox";
        props.ClassName = "browser-combobox-input";
        props.Placeholder = "Choose fruit";
        props.ShowClear = true;
    }),
    Combobox.Content.Create(
        props => props.ClassName = "browser-combobox-content",
        Combobox.List.Create(
            props => props.ClassName = "browser-combobox-list",
            Combobox.Group.Create(
                _ => { },
                Combobox.Label.Create(_ => { }, React.Text("Managed fruit")),
                Combobox.Item.Create(
                    props => props.Value = "apple",
                    React.Text("Apple")),
                Combobox.Separator.Create(_ => { }),
                Combobox.Item.Create(
                    props =>
                    {
                        props.ClassName = "browser-combobox-item";
                        props.Value = "banana";
                        props.OnClick = _ =>
                        {
                            comboboxClicks++;
                            Document.Body.SetAttribute("data-combobox-clicks", comboboxClicks.ToString());
                            UpdateStatus();
                        };
                    },
                    React.Text("Banana"))),
            Combobox.Empty.Create(_ => { }, React.Text("No fruit found")))));

var chart = Chart.Create(props =>
{
    props.AriaLabel = "Managed activity chart";
    props.ClassName = "browser-chart";
    props.Width = 360;
    props.Height = 220;
    props.SeriesLabel = "Managed value";
    props.Color = "#2563eb";
    props.Data =
    [
        new ChartDataPoint { Category = "Jan", Value = 12 },
        new ChartDataPoint { Category = "Feb", Value = 28 },
        new ChartDataPoint { Category = "Mar", Value = 19 }
    ];
});

var card = Card.Create(
    props => props.ClassName = "browser-card",
    Card.Header.Create(
        props => props.ClassName = "browser-card-header",
        Card.Title.Create(_ => { }, React.Text("Managed card title")),
        Card.Description.Create(_ => { }, React.Text("Managed card description"))),
    Card.Content.Create(_ => { }, breadcrumb, progress, checkbox, avatarGroup, button, disabledButton, input, textarea, accordion, select, tooltip, dialog, sheet, showToastButton, drawer, dropdownMenu, buttonGroup, separator, inputGroup, combobox, chart),
    Card.Footer.Create(_ => { }, spinner, React.Text("Managed card footer")));

var host = Document.CreateElement("div");
host.Id = "fecscn-root";
Document.Body.AppendChild(host);
var application = Tooltip.Provider.Create(
    props => props.Delay = 0,
    card,
    toastHost);
ReactRoot.Create(host).Render(application);
Document.Body.SetAttribute("data-fecscn-status", "mounted");
