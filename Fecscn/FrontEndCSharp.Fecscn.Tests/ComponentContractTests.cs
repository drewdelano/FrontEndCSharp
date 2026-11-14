using Interop.JS.Shadcn.BaseUI;
using Interop.JS.Shadcn.Recharts;
using Interop.JS.LucideIcons;
using FrontEndCSharp.Fecscn.Registry;
using Fecscn.Components.UI;
using Interop.JS.React;
using System.Diagnostics;

namespace FrontEndCSharp.Fecscn.Tests;

public sealed class ComponentContractTests
{
    [Test]
    public async Task Locked_cn_package_matches_the_component_merge_cases()
    {
        var packageDirectory = Path.Combine(
            FindRepositoryRoot(),
            "RuntimeOverrides",
            "Interop.JS.Shadcn.Cn",
            "Interop.JS.Shadcn.Cn.fecs",
            "JavaScriptPackages");
        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = packageDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--input-type=module");
        startInfo.ArgumentList.Add("--eval");
        startInfo.ArgumentList.Add(
            "import {cn} from 'cn'; console.log(JSON.stringify([cn('px-2','px-4'),cn('text-sm','','font-medium'),cn('hover:bg-muted','custom')]))");
        using var process = Process.Start(startInfo)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, stderr);
        Assert.Equal("[\"px-4\",\"text-sm font-medium\",\"hover:bg-muted custom\"]", stdout.Trim());
    }

    [Test]
    public void Imported_components_expose_concrete_library_owned_facades()
    {
        var props = ReactProps.Create();
        props.SetBoolean("disabled", true);
        props.SetString("data-slot", "button");

        AssertElementFacades(typeof(BaseUI.Button), typeof(BaseUI.Input));
        AssertElementFacades(
            typeof(LucideIcons.ActivityIcon), typeof(LucideIcons.CheckIcon), typeof(LucideIcons.ChevronDownIcon),
            typeof(LucideIcons.ChevronRightIcon), typeof(LucideIcons.ChevronUpIcon),
            typeof(LucideIcons.CloudLightningIcon), typeof(LucideIcons.CircleCheckIcon),
            typeof(LucideIcons.CrosshairIcon), typeof(LucideIcons.InfoIcon),
            typeof(LucideIcons.LoaderCircleIcon), typeof(LucideIcons.EllipsisIcon),
            typeof(LucideIcons.OctagonXIcon), typeof(LucideIcons.TriangleExclamationPointIcon),
            typeof(LucideIcons.XIcon));
        Assert.True((bool)props.Properties["disabled"]!);
        Assert.Equal("button", props.Properties["data-slot"]);
        Assert.Null(typeof(BaseUI).Assembly.GetType("Interop.JS.Shadcn.BaseUI.BaseButtonProps"));
        Assert.Null(typeof(BaseUI).Assembly.GetType("Interop.JS.Shadcn.BaseUI.BaseInputProps"));
        Assert.DoesNotContain(typeof(BaseUI).Assembly.GetTypes(), type => type.Name.EndsWith("Imports", StringComparison.Ordinal) && type.IsPublic);
        Assert.DoesNotContain(typeof(Interop.JS.Shadcn.Cn.ClassNames).Assembly.GetTypes(), type => type.Name.EndsWith("Imports", StringComparison.Ordinal) && type.IsPublic);
        Assert.DoesNotContain(typeof(Recharts).Assembly.GetTypes(), type => type.Name.EndsWith("Imports", StringComparison.Ordinal) && type.IsPublic);
        Assert.DoesNotContain(
            typeof(BaseUI).Assembly.GetExportedTypes().SelectMany(type => type.GetMethods()),
            method => method.ReturnType == typeof(ImportedReactComponent));
        Assert.DoesNotContain(
            typeof(Recharts).Assembly.GetExportedTypes().SelectMany(type => type.GetMethods()),
            method => method.ReturnType == typeof(ImportedReactComponent));
        Assert.DoesNotContain(
            typeof(LucideIcons).Assembly.GetExportedTypes().SelectMany(type => type.GetMethods()),
            method => method.ReturnType == typeof(ImportedReactComponent));

        var repositoryRoot = FindRepositoryRoot();
        var sourceRoots = new[]
        {
            Path.Combine(repositoryRoot, "Demo", "FrontEndCSharp.ReactDemo"),
            Path.Combine(repositoryRoot, "Fecscn", "Registry", "Sources")
        };
        var authoredSources = sourceRoots.SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".fecsx", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"));
        Assert.DoesNotContain(
            authoredSources,
            path => File.ReadAllText(path).Contains("ImportedReactElement", StringComparison.Ordinal));
    }

    [Test]
    public void Authored_registry_adds_import_packages_through_component_dependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(repositoryRoot, "Fecscn", "Registry", "registry.json")));
        var utility = Assert.Single(document.Items, item => item.Name == "utils");
        var button = Assert.Single(document.Items, item => item.Name == "button");
        var chart = Assert.Single(document.Items, item => item.Name == "chart");

        Assert.Equal(
            ["Interop.JS.Shadcn.Cn", "Interop.JS.Shadcn.Cn.fecs"],
            utility.Packages!.Select(package => package.Name));
        Assert.All(utility.Packages!, package => Assert.Equal("0.5.2", package.Version));
        Assert.Equal(
            ["Interop.JS.Shadcn.BaseUI", "Interop.JS.Shadcn.BaseUI.fecs"],
            button.Packages!.Select(package => package.Name));
        Assert.All(button.Packages!, package => Assert.Equal("1.9.0", package.Version));
        Assert.Equal(
            ["Interop.JS.Shadcn.Recharts", "Interop.JS.Shadcn.Recharts.fecs"],
            chart.Packages!.Select(package => package.Name));
        Assert.All(chart.Packages!, package => Assert.Equal("3.8.0", package.Version));
        Assert.All(
            document.Items.SelectMany(item => item.Packages ?? []),
            package => Assert.StartsWith("Interop.JS.", package.Name, StringComparison.Ordinal));

        var lucideUsers = new HashSet<string>(StringComparer.Ordinal)
        {
            "spinner", "breadcrumb", "checkbox", "accordion", "select",
            "dialog", "sheet", "toast", "dropdown-menu", "combobox"
        };
        foreach (var item in document.Items)
        {
            var lucidePackages = (item.Packages ?? [])
                .Where(package => package.Name.StartsWith("Interop.JS.LucideIcons", StringComparison.Ordinal))
                .ToArray();
            if (lucideUsers.Contains(item.Name))
            {
                Assert.Equal(
                    ["Interop.JS.LucideIcons", "Interop.JS.LucideIcons.fecs"],
                    lucidePackages.Select(package => package.Name));
                Assert.All(lucidePackages, package => Assert.Equal("1.72.0", package.Version));
                Assert.Contains(item.Dependencies ?? [], dependency =>
                    dependency.Name == "lucide-react" && dependency.Version == "1.72.0");
            }
            else
            {
                Assert.Empty(lucidePackages);
            }
        }
    }

    [Test]
    public void Reviewed_registry_components_compose_the_pinned_lucide_facades()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "Sources");
        var expectedFacades = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["spinner"] = ["LoaderCircleIcon"],
            ["breadcrumb"] = ["ChevronRightIcon", "EllipsisIcon"],
            ["checkbox"] = ["CheckIcon"],
            ["accordion"] = ["ChevronDownIcon", "ChevronUpIcon"],
            ["select"] = ["CheckIcon", "ChevronDownIcon", "ChevronUpIcon"],
            ["dialog"] = ["XIcon"],
            ["sheet"] = ["XIcon"],
            ["toast"] = ["CircleCheckIcon", "InfoIcon", "LoaderCircleIcon", "OctagonXIcon", "TriangleExclamationPointIcon", "XIcon"],
            ["dropdown-menu"] = ["CheckIcon", "ChevronRightIcon"],
            ["combobox"] = ["CheckIcon", "ChevronDownIcon", "XIcon"]
        };

        foreach (var (component, facades) in expectedFacades)
        {
            var source = string.Join(
                '\n',
                Directory.EnumerateFiles(Path.Combine(sourceRoot, component), "*", SearchOption.AllDirectories)
                    .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith(".fecsx", StringComparison.OrdinalIgnoreCase))
                    .Select(File.ReadAllText));
            Assert.Contains("LucideIcons", source, StringComparison.Ordinal);
            Assert.All(facades, facade =>
                Assert.Contains($"LucideIcons.{facade}", source, StringComparison.Ordinal));
        }
    }

    [TestCase(ButtonVariant.Default, "bg-primary")]
    [TestCase(ButtonVariant.Outline, "border-border")]
    [TestCase(ButtonVariant.Secondary, "bg-secondary")]
    [TestCase(ButtonVariant.Ghost, "hover:bg-muted")]
    [TestCase(ButtonVariant.Destructive, "bg-destructive/10")]
    [TestCase(ButtonVariant.Link, "hover:underline")]
    public void Button_exposes_each_default_profile_variant(ButtonVariant variant, string expected)
    {
        Assert.Contains(expected, ButtonStyles.Variant(variant), StringComparison.Ordinal);
    }

    [TestCase(ButtonSize.Default, "h-8")]
    [TestCase(ButtonSize.ExtraSmall, "h-6")]
    [TestCase(ButtonSize.Small, "h-7")]
    [TestCase(ButtonSize.Large, "h-9")]
    [TestCase(ButtonSize.Icon, "size-8")]
    public void Button_exposes_the_initial_size_surface(ButtonSize size, string expected)
    {
        Assert.Contains(expected, ButtonStyles.Size(size), StringComparison.Ordinal);
    }

    [Test]
    public void Input_exposes_common_identity_and_accessibility_properties()
    {
        var id = Assert.IsAssignableFrom<System.Reflection.PropertyInfo>(
            typeof(InputProps).GetProperty("Id"));
        var ariaLabel = Assert.IsAssignableFrom<System.Reflection.PropertyInfo>(
            typeof(InputProps).GetProperty("AriaLabel"));
        Assert.Equal(typeof(string), id.PropertyType);
        Assert.Equal(typeof(string), ariaLabel.PropertyType);
    }

    [Test]
    public void Textarea_exposes_the_typed_form_control_surface()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "textarea");

        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal(typeof(string), typeof(TextareaProps).GetProperty(nameof(TextareaProps.Value))!.PropertyType);
        Assert.Equal(typeof(bool), typeof(TextareaProps).GetProperty(nameof(TextareaProps.Disabled))!.PropertyType);
        Assert.Equal(
            typeof(Action<Interop.JS.Browser.DomEvent>),
            typeof(TextareaProps).GetProperty(nameof(TextareaProps.OnInput))!.PropertyType);
    }

    [Test]
    public void Spinner_exposes_an_accessible_status_contract()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "spinner");
        var props = new SpinnerProps();

        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal("Loading", props.AriaLabel);
        Assert.Contains("animate-spin", SpinnerStyles.Base, StringComparison.Ordinal);
    }

    [Test]
    public void Breadcrumb_exposes_link_and_current_page_contracts()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "breadcrumb");
        var props = new BreadcrumbProps();

        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal("breadcrumb", props.AriaLabel);
        Assert.Equal(typeof(string), typeof(BreadcrumbLinkProps).GetProperty(nameof(BreadcrumbLinkProps.Href))!.PropertyType);
        Assert.Contains("text-muted-foreground", BreadcrumbStyles.List, StringComparison.Ordinal);
    }

    [Test]
    public void Progress_exposes_compound_base_ui_imports_and_value_contract()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "progress");

        AssertElementFacades(
            typeof(BaseUI.Progress.Root),
            typeof(BaseUI.Progress.Track),
            typeof(BaseUI.Progress.Indicator),
            typeof(BaseUI.Progress.Label),
            typeof(BaseUI.Progress.Value));
        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal(typeof(double?), typeof(ProgressProps).GetProperty(nameof(ProgressProps.Value))!.PropertyType);
    }

    [Test]
    public void Checkbox_exposes_compound_base_ui_imports_and_optional_checked_state()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "checkbox");

        AssertElementFacades(typeof(BaseUI.Checkbox.Root), typeof(BaseUI.Checkbox.Indicator));
        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal(typeof(bool?), typeof(CheckboxProps).GetProperty(nameof(CheckboxProps.Checked))!.PropertyType);
        Assert.Equal(typeof(bool?), typeof(CheckboxProps).GetProperty(nameof(CheckboxProps.DefaultChecked))!.PropertyType);
    }

    [Test]
    public void Avatar_exposes_compound_imports_and_all_profile_sizes()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "avatar");

        AssertElementFacades(typeof(BaseUI.Avatar.Root), typeof(BaseUI.Avatar.Image), typeof(BaseUI.Avatar.Fallback));
        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal(
            [AvatarSize.Default, AvatarSize.Small, AvatarSize.Large],
            Enum.GetValues<AvatarSize>());
    }

    [Test]
    public void Accordion_exposes_compound_imports_and_typed_interaction_state()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "accordion");

        AssertElementFacades(
            typeof(BaseUI.Accordion.Root),
            typeof(BaseUI.Accordion.Item),
            typeof(BaseUI.Accordion.Header),
            typeof(BaseUI.Accordion.Trigger),
            typeof(BaseUI.Accordion.Panel));
        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal(typeof(bool), typeof(AccordionProps).GetProperty(nameof(AccordionProps.Multiple))!.PropertyType);
        Assert.Equal(typeof(string), typeof(AccordionItemProps).GetProperty(nameof(AccordionItemProps.Value))!.PropertyType);
        Assert.Equal(typeof(bool), typeof(AccordionItemProps).GetProperty(nameof(AccordionItemProps.Disabled))!.PropertyType);
    }

    [Test]
    public void Select_exposes_the_complete_popup_composition_and_trigger_sizes()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "select");

        AssertElementFacades(
            typeof(BaseUI.Select.Root), typeof(BaseUI.Select.Group),
            typeof(BaseUI.Select.Value), typeof(BaseUI.Select.Trigger),
            typeof(BaseUI.Select.Icon), typeof(BaseUI.Select.Portal),
            typeof(BaseUI.Select.Positioner), typeof(BaseUI.Select.Popup),
            typeof(BaseUI.Select.List), typeof(BaseUI.Select.GroupLabel),
            typeof(BaseUI.Select.Item), typeof(BaseUI.Select.ItemText),
            typeof(BaseUI.Select.ItemIndicator), typeof(BaseUI.Select.Separator),
            typeof(BaseUI.Select.ScrollUpArrow), typeof(BaseUI.Select.ScrollDownArrow));
        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal(typeof(string), typeof(SelectProps).GetProperty(nameof(SelectProps.DefaultValue))!.PropertyType);
        Assert.Equal(
            [SelectTriggerSize.Default, SelectTriggerSize.Small],
            Enum.GetValues<SelectTriggerSize>());
    }

    [Test]
    public void Tooltip_exposes_provider_trigger_and_positioned_popup_parts()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "tooltip");

        AssertElementFacades(
            typeof(BaseUI.Tooltip.Provider), typeof(BaseUI.Tooltip.Root),
            typeof(BaseUI.Tooltip.Trigger), typeof(BaseUI.Tooltip.Portal),
            typeof(BaseUI.Tooltip.Positioner), typeof(BaseUI.Tooltip.Popup),
            typeof(BaseUI.Tooltip.Arrow));
        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal(typeof(double), typeof(TooltipProviderProps).GetProperty(nameof(TooltipProviderProps.Delay))!.PropertyType);
        Assert.Equal(typeof(TooltipSide), typeof(TooltipContentProps).GetProperty(nameof(TooltipContentProps.Side))!.PropertyType);
    }

    [Test]
    public void Dialog_exposes_modal_parts_and_button_dependency()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "dialog");

        AssertElementFacades(
            typeof(BaseUI.Dialog.Root), typeof(BaseUI.Dialog.Trigger),
            typeof(BaseUI.Dialog.Portal), typeof(BaseUI.Dialog.Close),
            typeof(BaseUI.Dialog.Backdrop), typeof(BaseUI.Dialog.Popup),
            typeof(BaseUI.Dialog.Title), typeof(BaseUI.Dialog.Description));
        Assert.Equal(["button", "utils"], item.RegistryDependencies);
        Assert.Equal(typeof(bool?), typeof(DialogProps).GetProperty(nameof(DialogProps.Open))!.PropertyType);
        Assert.True(new DialogContentProps().ShowCloseButton);
    }

    [Test]
    public void Sheet_reuses_dialog_imports_and_exposes_all_slide_sides()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "sheet");

        Assert.Equal(["button", "utils"], item.RegistryDependencies);
        Assert.Equal(
            [SheetSide.Right, SheetSide.Left, SheetSide.Top, SheetSide.Bottom],
            Enum.GetValues<SheetSide>());
        Assert.True(new SheetContentProps().ShowCloseButton);
        AssertElementFacades(typeof(BaseUI.Dialog.Popup));
    }

    [Test]
    public void Toast_exposes_selected_manager_adapter_and_all_status_types()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "toast");

        AssertElementFacades(typeof(BaseUI.Toast.Host));
        Assert.Equal(
            typeof(void),
            typeof(BaseUI.Toast).GetMethod(nameof(BaseUI.Toast.Add))!.ReturnType);
        Assert.Equal(["button", "utils"], item.RegistryDependencies);
        Assert.Equal(
            [ToastType.Default, ToastType.Success, ToastType.Info, ToastType.Warning, ToastType.Error, ToastType.Loading],
            Enum.GetValues<ToastType>());
    }

    [Test]
    public void Drawer_exposes_swipe_composition_and_all_directions()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "drawer");

        AssertElementFacades(
            typeof(BaseUI.Drawer.Root), typeof(BaseUI.Drawer.Trigger),
            typeof(BaseUI.Drawer.Portal), typeof(BaseUI.Drawer.Close),
            typeof(BaseUI.Drawer.Backdrop), typeof(BaseUI.Drawer.Viewport),
            typeof(BaseUI.Drawer.Popup), typeof(BaseUI.Drawer.Content),
            typeof(BaseUI.Drawer.Title), typeof(BaseUI.Drawer.Description));
        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal(
            [DrawerDirection.Down, DrawerDirection.Up, DrawerDirection.Left, DrawerDirection.Right],
            Enum.GetValues<DrawerDirection>());
    }

    [Test]
    public void Dropdown_menu_exposes_items_selection_parts_and_popup_placement()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var item = Assert.Single(document.Items, candidate => candidate.Name == "dropdown-menu");

        AssertElementFacades(
            typeof(BaseUI.Menu.Root), typeof(BaseUI.Menu.Portal),
            typeof(BaseUI.Menu.Trigger), typeof(BaseUI.Menu.Positioner),
            typeof(BaseUI.Menu.Popup), typeof(BaseUI.Menu.Group),
            typeof(BaseUI.Menu.GroupLabel), typeof(BaseUI.Menu.Item),
            typeof(BaseUI.Menu.SubmenuRoot), typeof(BaseUI.Menu.SubmenuTrigger),
            typeof(BaseUI.Menu.CheckboxItem), typeof(BaseUI.Menu.CheckboxItemIndicator),
            typeof(BaseUI.Menu.RadioGroup), typeof(BaseUI.Menu.RadioItem),
            typeof(BaseUI.Menu.RadioItemIndicator), typeof(BaseUI.Menu.Separator));
        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal(typeof(bool?), typeof(DropdownMenuCheckboxItemProps).GetProperty(nameof(DropdownMenuCheckboxItemProps.Checked))!.PropertyType);
        Assert.Equal(
            [DropdownMenuItemVariant.Default, DropdownMenuItemVariant.Destructive],
            Enum.GetValues<DropdownMenuItemVariant>());
    }

    [Test]
    public void Button_group_exposes_typed_orientation_and_separator_dependency()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var separator = Assert.Single(document.Items, candidate => candidate.Name == "separator");
        var buttonGroup = Assert.Single(document.Items, candidate => candidate.Name == "button-group");

        AssertElementFacades(typeof(BaseUI.Separator));
        Assert.Equal(["utils"], separator.RegistryDependencies);
        Assert.Equal(["separator", "utils"], buttonGroup.RegistryDependencies);
        Assert.Equal(
            [ComponentOrientation.Horizontal, ComponentOrientation.Vertical],
            Enum.GetValues<ComponentOrientation>());
        Assert.Equal(ComponentOrientation.Horizontal, new ButtonGroupProps().Orientation);
        Assert.Equal(ComponentOrientation.Vertical, new ButtonGroupSeparatorProps().Orientation);
    }

    [Test]
    public void Combobox_exposes_popup_collection_chips_and_input_group_dependency()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var inputGroup = Assert.Single(document.Items, candidate => candidate.Name == "input-group");
        var combobox = Assert.Single(document.Items, candidate => candidate.Name == "combobox");

        AssertElementFacades(
            typeof(BaseUI.Combobox.Root), typeof(BaseUI.Combobox.Value),
            typeof(BaseUI.Combobox.Trigger), typeof(BaseUI.Combobox.Clear),
            typeof(BaseUI.Combobox.Input), typeof(BaseUI.Combobox.Portal),
            typeof(BaseUI.Combobox.Positioner), typeof(BaseUI.Combobox.Popup),
            typeof(BaseUI.Combobox.List), typeof(BaseUI.Combobox.Item),
            typeof(BaseUI.Combobox.ItemIndicator), typeof(BaseUI.Combobox.Group),
            typeof(BaseUI.Combobox.GroupLabel), typeof(BaseUI.Combobox.Collection),
            typeof(BaseUI.Combobox.Empty), typeof(BaseUI.Combobox.Separator),
            typeof(BaseUI.Combobox.Chips), typeof(BaseUI.Combobox.Chip),
            typeof(BaseUI.Combobox.ChipRemove));
        Assert.Equal(["button", "input", "textarea", "utils"], inputGroup.RegistryDependencies);
        Assert.Equal(["button", "input-group", "utils"], combobox.RegistryDependencies);
        Assert.Equal(typeof(string), typeof(ComboboxProps).GetProperty(nameof(ComboboxProps.DefaultValue))!.PropertyType);
        Assert.Equal(typeof(bool), typeof(ComboboxInputProps).GetProperty(nameof(ComboboxInputProps.ShowTrigger))!.PropertyType);
        Assert.Equal(
            [InputGroupAddonAlign.InlineStart, InputGroupAddonAlign.InlineEnd, InputGroupAddonAlign.BlockStart, InputGroupAddonAlign.BlockEnd],
            Enum.GetValues<InputGroupAddonAlign>());
    }

    [Test]
    public void Chart_exposes_a_pinned_responsive_recharts_bar_graph()
    {
        var document = FecscnJson.ParseRegistryDocument(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Fecscn", "Registry", "registry.json")));
        var chart = Assert.Single(document.Items, candidate => candidate.Name == "chart");

        AssertElementFacades(
            typeof(Recharts.ResponsiveContainer), typeof(Recharts.BarChart),
            typeof(Recharts.Bar), typeof(Recharts.CartesianGrid),
            typeof(Recharts.XAxis), typeof(Recharts.Tooltip),
            typeof(Recharts.Legend));
        Assert.Equal(typeof(string[]), typeof(Recharts.BarChartElementProps).GetProperty(nameof(Recharts.BarChartElementProps.Categories))!.PropertyType);
        Assert.Equal(typeof(double[]), typeof(Recharts.BarChartElementProps).GetProperty(nameof(Recharts.BarChartElementProps.Values))!.PropertyType);
        Assert.Equal(["card", "utils"], chart.RegistryDependencies);
        Assert.Contains(chart.Dependencies!, dependency =>
            dependency.Name == "recharts" && dependency.Version == "3.8.0");
        Assert.Contains(chart.Dependencies!, dependency =>
            dependency.Name == "react-is" && dependency.Version == "19.3.1");
        Assert.Equal(
            typeof(ChartDataPoint[]),
            typeof(ChartProps).GetProperty(nameof(ChartProps.Data))!.PropertyType);
        Assert.Equal("Value", new ChartBarProps().DataKey);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static void AssertElementFacades(params Type[] facadeTypes)
    {
        Assert.All(facadeTypes, facadeType =>
        {
            var createMethods = facadeType.GetMethods(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(method => method.Name == "Create")
                .ToArray();
            Assert.NotEmpty(createMethods);
            Assert.All(createMethods, method => Assert.Equal(typeof(ReactElement), method.ReturnType));
        });
    }
}
