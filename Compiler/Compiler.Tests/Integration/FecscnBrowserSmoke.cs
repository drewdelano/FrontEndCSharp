namespace FrontEndCSharp.Compiler.Integration;

internal static class FecscnBrowserSmoke
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);

    public static void Run(string browserPath, bool noBuild) =>
        RunAsync(browserPath, noBuild).GetAwaiter().GetResult();

    private static async Task RunAsync(string browserPath, bool noBuild)
    {
        browserPath = Path.GetFullPath(browserPath);
        if (!File.Exists(browserPath))
        {
            throw new FileNotFoundException("The Chromium browser does not exist.", browserPath);
        }
        var repositoryRoot = RepositoryLayout.Root;
        var fixtureRoot = Path.Combine(repositoryRoot, "Fecscn", "Tests", "FecscnBrowserFixture");
        var fixtureProject = Path.Combine(fixtureRoot, "FecscnBrowserFixture.csproj");
        var fixtureAssembly = Path.Combine(fixtureRoot, "bin", "Debug", "net8.0", "FecscnBrowserFixture.dll");
        var lucideFixtureRoot = Path.Combine(repositoryRoot, "Compiler", "Compiler.Tests", "Fixtures", "LucideIconsReachabilityFixture");
        var lucideFixtureProject = Path.Combine(lucideFixtureRoot, "LucideIconsReachabilityFixture.csproj");
        var lucideFixtureAssembly = Path.Combine(lucideFixtureRoot, "bin", "Debug", "net8.0", "LucideIconsReachabilityFixture.dll");
        var compilerProject = Path.Combine(repositoryRoot, "Compiler", "Core", "Compiler.csproj");
        var compilerPath = Path.Combine(repositoryRoot, "Compiler", "Core", "bin", "Debug", "net8.0", "Compiler.dll");
        var output = TemporaryDirectory("FrontEndCSharp-fecscn-browser-");
        var profile = TemporaryDirectory("FrontEndCSharp-fecscn-profile-");
        try
        {
            if (!noBuild)
            {
                RunDotNet(repositoryRoot, "build", compilerProject, "--configuration", "Debug");
                RunDotNet(repositoryRoot, "build", fixtureProject, "--configuration", "Debug");
                RunDotNet(repositoryRoot, "build", lucideFixtureProject, "--configuration", "Debug");
            }
            VerifyLucideReachability(repositoryRoot, compilerPath, lucideFixtureProject, lucideFixtureAssembly);
            RunDotNet(repositoryRoot, compilerPath, fixtureProject, output, fixtureAssembly);
            await File.WriteAllTextAsync(
                Path.Combine(output, "index.html"),
                "<!doctype html><html><body><script type=\"module\" src=\"/index.js\"></script></body></html>");
            using var server = new StaticFileServer(output);
            await using var browser = await ChromiumCdpSession.LaunchAsync(browserPath, profile);
            await browser.SendAsync("Page.navigate", new { url = server.Url });
            await browser.WaitForAsync(
                "document.body?.getAttribute('data-fecscn-status') === 'mounted'",
                "fecscn component mount");
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=avatar-image]') !== null"
                    + " && document.querySelector('[data-slot=avatar-fallback]') !== null",
                "fecscn avatar image and fallback state");
            try
            {
                await browser.WaitForAsync(
                    "document.querySelectorAll('[data-slot=chart] .recharts-bar-rectangle path.recharts-rectangle').length === 3",
                    "fecscn Recharts bar rendering");
            }
            catch (Exception exception)
            {
                var chartEvidence = await browser.EvaluateAsync("""
                    (() => JSON.stringify({
                      status: document.body?.getAttribute('data-fecscn-status'),
                      chart: document.querySelector('[data-slot="chart"]')?.outerHTML,
                      bodyText: document.body?.innerText
                    }))()
                    """);
                throw new InvalidDataException(
                    "fecscn Recharts bars were not rendered. Evidence: "
                        + chartEvidence.GetString()
                        + "\nBrowser exceptions:\n"
                        + string.Join("\n", browser.Exceptions),
                    exception);
            }

            var initial = await browser.EvaluateAsync("""
                (() => {
                  const buttons = [...document.querySelectorAll('[data-slot="button"]')];
                  const input = document.querySelector('[data-slot="input"]');
                  const textarea = document.querySelector('textarea');
                  const spinner = document.querySelector('.browser-spinner[role="status"]');
                  const breadcrumb = document.querySelector('nav[data-slot="breadcrumb"]');
                  const breadcrumbPage = document.querySelector('[data-slot="breadcrumb-page"]');
                  const breadcrumbSeparators = [...document.querySelectorAll('[data-slot="breadcrumb-separator"]')];
                  const progress = document.querySelector('[data-slot="progress"]');
                  const progressIndicator = document.querySelector('[data-slot="progress-indicator"]');
                  const checkbox = document.querySelector('[data-slot="checkbox"]');
                  const avatarGroup = document.querySelector('[data-slot="avatar-group"]');
                  const avatars = [...document.querySelectorAll('[data-slot="avatar"]')];
                  const avatarImage = document.querySelector('[data-slot="avatar-image"]');
                  const accordion = document.querySelector('[data-slot="accordion"]');
                  const accordionItems = [...document.querySelectorAll('[data-slot="accordion-item"]')];
                  const accordionTriggers = [...document.querySelectorAll('[data-slot="accordion-trigger"]')];
                  const selectTrigger = document.querySelector('[data-slot="select-trigger"]');
                  const tooltipTrigger = document.querySelector('[data-slot="tooltip-trigger"]');
                  const dialogTrigger = document.querySelector('[data-slot="dialog-trigger"]');
                  const sheetTrigger = document.querySelector('[data-slot="sheet-trigger"]');
                  const drawerTrigger = document.querySelector('[data-slot="drawer-trigger"]');
                  const dropdownTrigger = document.querySelector('[data-slot="dropdown-menu-trigger"]');
                  const buttonGroup = document.querySelector('[data-slot="button-group"]');
                  const buttonGroupButtons = [...buttonGroup.querySelectorAll('[data-slot="button"]')];
                  const buttonGroupSeparator = buttonGroup.querySelector('[data-slot="button-group-separator"]');
                  const separator = document.querySelector('[data-slot="separator"]');
                  const inputGroup = document.querySelector('.browser-input-group');
                  const groupedInput = inputGroup.querySelector('[data-slot="input-group-control"]');
                  const comboboxInput = document.querySelector('input[role="combobox"][aria-label="Managed combobox"]');
                  const comboboxInputGroup = comboboxInput.closest('[data-slot="input-group"]');
                  const comboboxTrigger = comboboxInputGroup.querySelector('[data-slot="combobox-trigger"]');
                  const comboboxClear = comboboxInputGroup.querySelector('[data-slot="combobox-clear"]');
                  const chart = document.querySelector('[data-slot="chart"]');
                  const chartBars = [...chart.querySelectorAll('.recharts-bar-rectangle path.recharts-rectangle')];
                  const chartTicks = [...chart.querySelectorAll('.recharts-xAxis-tick-labels .recharts-cartesian-axis-tick-value')].map(tick => tick.textContent);
                  const card = document.querySelector('.browser-card');
                  return buttons.length === 5
                    && buttons[0].textContent === 'Managed button child'
                    && buttons[0].classList.contains('h-12')
                    && !buttons[0].classList.contains('h-8')
                    && buttons[0].classList.contains('border-border')
                    && buttons[0].type === 'button'
                    && buttons[0].getAttribute('data-slot') === 'button'
                    && !buttons[0].getAttributeNames().some(name => name.toLowerCase().includes('fecsmanaged'))
                    && buttons[1].disabled
                    && buttons[2].classList.contains('browser-toast-trigger')
                    && buttons[2].textContent === 'Show managed toast'
                    && input.value === 'initial'
                    && input.name === 'managed-name'
                    && input.placeholder === 'Managed input'
                    && input.getAttribute('data-slot') === 'input'
                    && !input.getAttributeNames().some(name => name.toLowerCase().includes('fecsmanaged'))
                    && input.classList.contains('h-10')
                    && !input.classList.contains('h-8')
                    && textarea.value === 'initial notes'
                    && textarea.placeholder === 'Managed textarea'
                    && textarea.getAttribute('aria-label') === 'Managed notes'
                    && textarea.required
                    && textarea.classList.contains('custom-textarea')
                    && textarea.classList.contains('min-h-24')
                    && !textarea.classList.contains('min-h-16')
                    && spinner.getAttribute('aria-label') === 'Managed loading'
                    && spinner.classList.contains('browser-spinner')
                    && spinner.classList.contains('size-6')
                    && !spinner.classList.contains('size-4')
                    && spinner.classList.contains('animate-spin')
                    && breadcrumb.getAttribute('aria-label') === 'Managed breadcrumb'
                    && breadcrumb.classList.contains('browser-breadcrumb')
                    && breadcrumb.querySelector('[data-slot="breadcrumb-list"]').classList.contains('browser-breadcrumb-list')
                    && breadcrumb.querySelector('[data-slot="breadcrumb-link"]').getAttribute('href') === '/managed-home'
                    && breadcrumbPage.getAttribute('aria-current') === 'page'
                    && breadcrumbPage.getAttribute('aria-disabled') === 'true'
                    && breadcrumbPage.textContent === 'Current'
                    && breadcrumbSeparators.length === 2
                    && breadcrumbSeparators.every(separator => separator.querySelector('svg.lucide') !== null)
                    && breadcrumb.querySelector('[data-slot="breadcrumb-ellipsis"] svg.lucide') !== null
                    && breadcrumb.querySelector('[data-slot="breadcrumb-ellipsis"]').textContent.includes('More')
                    && progress.getAttribute('role') === 'progressbar'
                    && progress.getAttribute('aria-valuenow') === '65'
                    && progress.getAttribute('aria-valuetext') === '65%'
                    && progress.classList.contains('browser-progress')
                    && progress.querySelector('[data-slot="progress-label"]').textContent === 'Managed progress'
                    && progress.querySelector('[data-slot="progress-value"]').textContent === '65%'
                    && progress.querySelector('[data-slot="progress-track"]') !== null
                    && progressIndicator.style.width === '65%'
                    && checkbox.getAttribute('role') === 'checkbox'
                    && checkbox.getAttribute('aria-label') === 'Managed checkbox'
                    && checkbox.getAttribute('aria-checked') === 'false'
                    && checkbox.classList.contains('browser-checkbox')
                    && checkbox.classList.contains('size-5')
                    && !checkbox.classList.contains('size-4')
                    && avatarGroup !== null
                    && avatarGroup.classList.contains('browser-avatar-group')
                    && avatars.length === 2
                    && avatars[0].getAttribute('data-size') === 'lg'
                    && avatars[0].classList.contains('browser-avatar')
                    && avatars[0].classList.contains('size-8')
                    && avatars[1].getAttribute('data-size') === 'sm'
                    && avatarImage !== null
                    && avatarImage.getAttribute('alt') === 'Managed avatar'
                    && avatarImage.classList.contains('browser-avatar-image')
                    && avatarGroup.querySelector('[data-slot="avatar-badge"]') !== null
                    && avatarGroup.querySelector('[data-slot="avatar-fallback"]') !== null
                    && avatarGroup.querySelector('[data-slot="avatar-fallback"]').textContent === 'DD'
                    && avatarGroup.querySelector('[data-slot="avatar-fallback"]').classList.contains('browser-avatar-fallback')
                    && avatarGroup.querySelector('[data-slot="avatar-group-count"]') !== null
                    && avatarGroup.querySelector('[data-slot="avatar-group-count"]').textContent === '+2'
                    && accordion.classList.contains('browser-accordion')
                    && accordionItems.length === 2
                    && accordionItems[0].classList.contains('browser-accordion-item')
                    && accordionTriggers.length === 2
                    && accordionTriggers[0].getAttribute('aria-expanded') === 'false'
                    && accordionTriggers[0].classList.contains('browser-accordion-trigger')
                    && accordionTriggers[0].querySelectorAll('[data-slot="accordion-trigger-icon"]').length === 2
                    && accordionTriggers[1].disabled
                    && accordion.querySelector('[data-slot="accordion-content"]') === null
                    && selectTrigger.getAttribute('role') === 'combobox'
                    && selectTrigger.getAttribute('aria-label') === 'Managed select'
                    && selectTrigger.getAttribute('aria-expanded') === 'false'
                    && selectTrigger.getAttribute('data-size') === 'sm'
                    && selectTrigger.classList.contains('browser-select-trigger')
                    && selectTrigger.textContent.includes('alpha')
                    && document.querySelector('[data-slot="select-content"]') === null
                    && tooltipTrigger.getAttribute('aria-label') === 'Managed tooltip trigger'
                    && tooltipTrigger.classList.contains('browser-tooltip-trigger')
                    && tooltipTrigger.textContent === 'Tooltip target'
                    && document.querySelector('[data-slot="tooltip-content"]') === null
                    && dialogTrigger.classList.contains('browser-dialog-trigger')
                    && dialogTrigger.textContent === 'Open managed dialog'
                    && document.querySelector('[data-slot="dialog-content"]') === null
                    && sheetTrigger.classList.contains('browser-sheet-trigger')
                    && sheetTrigger.textContent === 'Open managed sheet'
                    && document.querySelector('[data-slot="sheet-content"]') === null
                    && drawerTrigger.classList.contains('browser-drawer-trigger')
                    && drawerTrigger.textContent === 'Open managed drawer'
                    && document.querySelector('[data-slot="drawer-popup"]') === null
                    && dropdownTrigger.classList.contains('browser-dropdown-trigger')
                    && dropdownTrigger.textContent === 'Open managed menu'
                    && document.querySelector('[data-slot="dropdown-menu-content"]') === null
                    && buttonGroup.getAttribute('role') === 'group'
                    && buttonGroup.classList.contains('browser-button-group')
                    && buttonGroup.classList.contains('flex')
                    && buttonGroup.className.includes('rounded-r-none')
                    && buttonGroupButtons.length === 2
                    && buttonGroupButtons[0].textContent === 'Previous'
                    && buttonGroupButtons[1].textContent === 'Next'
                    && buttonGroupSeparator.getAttribute('role') === 'separator'
                    && buttonGroupSeparator.getAttribute('aria-orientation') === 'vertical'
                    && buttonGroupSeparator.classList.contains('browser-button-group-separator')
                    && buttonGroup.querySelector('[data-slot="button-group-text"]').textContent === 'Page 2'
                    && buttonGroup.querySelector('[data-slot="button-group-text"]').classList.contains('browser-button-group-text')
                    && separator.getAttribute('role') === 'separator'
                    && separator.getAttribute('aria-orientation') === 'horizontal'
                    && separator.getAttribute('data-orientation') === 'horizontal'
                    && separator.classList.contains('browser-separator')
                    && separator.className.includes('data-horizontal:w-full')
                    && inputGroup.getAttribute('role') === 'group'
                    && groupedInput.getAttribute('aria-label') === 'Managed grouped input'
                    && groupedInput.value === 'example.test'
                    && inputGroup.textContent.includes('https://')
                    && inputGroup.textContent.includes('.dev')
                    && comboboxInput.placeholder === 'Choose fruit'
                    && comboboxInputGroup.classList.contains('browser-combobox-input')
                    && comboboxTrigger.getAttribute('aria-label') === 'Toggle suggestions'
                    && comboboxTrigger.getAttribute('aria-expanded') === 'false'
                    && comboboxClear.getAttribute('aria-label') === 'Clear selection'
                    && document.querySelector('[data-slot="combobox-content"]') === null
                    && chart.getAttribute('role') === 'img'
                    && chart.getAttribute('aria-label') === 'Managed activity chart'
                    && chart.classList.contains('browser-chart')
                    && chart.querySelector('svg.recharts-surface') !== null
                    && chartBars.length === 3
                    && chartBars.every(bar => bar.getAttribute('fill') === '#2563eb')
                    && chartTicks.join(',') === 'Jan,Feb,Mar'
                    && chart.querySelectorAll('.recharts-cartesian-grid line').length > 0
                    && chart.querySelector('.recharts-legend-wrapper').textContent.includes('Managed value')
                    && card.textContent.includes('Managed card title')
                    && card.textContent.includes('Managed card description')
                    && card.textContent.includes('Managed card footer');
                })()
                """);
            if (initial.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                var dom = await browser.EvaluateAsync("document.documentElement.outerHTML");
                throw new InvalidDataException(
                    "fecscn components did not render their typed initial state.\nDOM:\n"
                    + dom.GetString()
                    + "\nBrowser exceptions:\n"
                    + string.Join("\n", browser.Exceptions));
            }
            var tooltipPoint = await browser.EvaluateAsync("""
                (() => {
                  const trigger = document.querySelector('[data-slot="tooltip-trigger"]');
                  trigger.scrollIntoView({ block: 'center' });
                  const rect = trigger.getBoundingClientRect();
                  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
                })()
                """);
            await browser.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "mouseMoved",
                x = tooltipPoint.GetProperty("x").GetDouble(),
                y = tooltipPoint.GetProperty("y").GetDouble()
            });
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=tooltip-content]') !== null",
                "fecscn tooltip focus opening");
            var openTooltip = await browser.EvaluateAsync("""
                (() => {
                  const content = document.querySelector('[data-slot="tooltip-content"]');
                  return content.textContent.includes('Managed tooltip content')
                    && content.classList.contains('browser-tooltip-content')
                    && ['top', 'bottom'].includes(content.getAttribute('data-side'))
                    && content.querySelector('[data-slot="tooltip-arrow"]') !== null;
                })()
                """);
            if (openTooltip.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                var tooltipDom = await browser.EvaluateAsync("document.querySelector('[data-slot=tooltip-content]').outerHTML");
                throw new InvalidDataException(
                    "fecscn Tooltip did not render its positioned popup composition. DOM: " + tooltipDom.GetString());
            }
            await browser.SendAsync("Input.dispatchMouseEvent", new { type = "mouseMoved", x = 0, y = 0 });
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=tooltip-content]') === null",
                "fecscn tooltip focus closing");
            await browser.EvaluateAsync("(() => { document.querySelector('[data-slot=dialog-trigger]').click(); return true; })()");
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=dialog-content]') !== null",
                "fecscn dialog opening");
            var openDialog = await browser.EvaluateAsync("""
                (() => {
                  const content = document.querySelector('[data-slot="dialog-content"]');
                  const overlay = document.querySelector('[data-slot="dialog-overlay"]');
                  const closeButtons = [...document.querySelectorAll('[data-slot="dialog-close"]')];
                  const valid = content.getAttribute('role') === 'dialog'
                    && content.classList.contains('browser-dialog-content')
                    && overlay !== null
                    && content.querySelector('[data-slot="dialog-header"]').classList.contains('browser-dialog-header')
                    && content.querySelector('[data-slot="dialog-title"]').textContent === 'Managed dialog title'
                    && content.querySelector('[data-slot="dialog-description"]').textContent === 'Managed dialog description'
                    && content.querySelector('[data-slot="dialog-footer"]').classList.contains('browser-dialog-footer')
                    && closeButtons.length === 2
                    && closeButtons.some(button => button.getAttribute('aria-label') === 'Close');
                  content.querySelector('.browser-dialog-close').click();
                  return valid;
                })()
                """);
            if (openDialog.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Dialog did not render its modal composition.");
            }
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=dialog-content]') === null",
                "fecscn dialog closing");
            await browser.EvaluateAsync("(() => { document.querySelector('[data-slot=sheet-trigger]').click(); return true; })()");
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=sheet-content]') !== null",
                "fecscn sheet opening");
            var openSheet = await browser.EvaluateAsync("""
                (() => {
                  const content = document.querySelector('[data-slot="sheet-content"]');
                  const overlay = document.querySelector('[data-slot="sheet-overlay"]');
                  const closeButtons = [...document.querySelectorAll('[data-slot="sheet-close"]')];
                  const valid = content.getAttribute('role') === 'dialog'
                    && content.getAttribute('data-side') === 'left'
                    && content.classList.contains('browser-sheet-content')
                    && overlay !== null
                    && content.querySelector('[data-slot="sheet-header"]').classList.contains('browser-sheet-header')
                    && content.querySelector('[data-slot="sheet-title"]').textContent === 'Managed sheet title'
                    && content.querySelector('[data-slot="sheet-description"]').textContent === 'Managed sheet description'
                    && content.querySelector('[data-slot="sheet-footer"]').classList.contains('browser-sheet-footer')
                    && closeButtons.length === 2
                    && closeButtons.some(button => button.getAttribute('aria-label') === 'Close');
                  content.querySelector('.browser-sheet-close').click();
                  return valid;
                })()
                """);
            if (openSheet.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Sheet did not render its side-specific modal composition.");
            }
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=sheet-content]') === null",
                "fecscn sheet closing");
            await browser.EvaluateAsync("(() => { document.querySelector('.browser-toast-trigger').click(); return true; })()");
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=toast]') !== null",
                "fecscn toast manager addition");
            var openToast = await browser.EvaluateAsync("""
                (() => {
                  const viewport = document.querySelector('[data-slot="toast-viewport"]');
                  const toast = document.querySelector('[data-slot="toast"]');
                  const close = toast.querySelector('[data-slot="toast-close"]');
                  const valid = viewport.classList.contains('browser-toast-viewport')
                    && toast.querySelector('[data-slot="toast-title"]').textContent === 'Managed toast title'
                    && toast.querySelector('[data-slot="toast-description"]').textContent === 'Managed toast description'
                    && toast.querySelector('[data-slot="toast-icon"]')?.matches('svg.lucide')
                    && close.getAttribute('aria-label') === 'Close toast'
                    && close.querySelector('svg.lucide') !== null;
                  close.click();
                  return valid;
                })()
                """);
            if (openToast.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Toast did not render its managed notification composition.");
            }
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=toast]') === null",
                "fecscn toast close action");
            await browser.EvaluateAsync("(() => { document.querySelector('[data-slot=drawer-trigger]').click(); return true; })()");
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=drawer-popup]') !== null",
                "fecscn drawer opening");
            var openDrawer = await browser.EvaluateAsync("""
                (() => {
                  const popup = document.querySelector('[data-slot="drawer-popup"]');
                  const content = popup.querySelector('[data-slot="drawer-content"]');
                  const valid = popup.getAttribute('role') === 'dialog'
                    && popup.getAttribute('data-swipe-direction') === 'down'
                    && popup.getAttribute('data-swipe-axis') === 'y'
                    && popup.classList.contains('browser-drawer-popup')
                    && document.querySelector('[data-slot="drawer-overlay"]') !== null
                    && document.querySelector('[data-slot="drawer-viewport"]') !== null
                    && popup.querySelector('[data-slot="drawer-swipe-handle"]') !== null
                    && content.querySelector('[data-slot="drawer-header"]').classList.contains('browser-drawer-header')
                    && content.querySelector('[data-slot="drawer-title"]').textContent === 'Managed drawer title'
                    && content.querySelector('[data-slot="drawer-description"]').textContent === 'Managed drawer description'
                    && content.querySelector('[data-slot="drawer-footer"]').classList.contains('browser-drawer-footer');
                  content.querySelector('.browser-drawer-close').click();
                  return valid;
                })()
                """);
            if (openDrawer.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Drawer did not render its swipe composition.");
            }
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=drawer-popup]') === null",
                "fecscn drawer closing");
            var dropdownPoint = await browser.EvaluateAsync("""
                (() => {
                  const trigger = document.querySelector('[data-slot="dropdown-menu-trigger"]');
                  trigger.scrollIntoView({ block: 'center' });
                  const rect = trigger.getBoundingClientRect();
                  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
                })()
                """);
            var dropdownX = dropdownPoint.GetProperty("x").GetDouble();
            var dropdownY = dropdownPoint.GetProperty("y").GetDouble();
            await browser.SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x = dropdownX, y = dropdownY, button = "left", clickCount = 1 });
            await browser.SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x = dropdownX, y = dropdownY, button = "left", clickCount = 1 });
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=dropdown-menu-content]') !== null",
                "fecscn dropdown menu opening");
            var openDropdown = await browser.EvaluateAsync("""
                (() => {
                  const content = document.querySelector('[data-slot="dropdown-menu-content"]');
                  const item = content.querySelector('[data-slot="dropdown-menu-item"]');
                  const checkbox = content.querySelector('[data-slot="dropdown-menu-checkbox-item"]');
                  const radio = content.querySelector('[data-slot="dropdown-menu-radio-item"]');
                  const valid = content.getAttribute('role') === 'menu'
                    && content.classList.contains('browser-dropdown-content')
                    && content.querySelector('[data-slot="dropdown-menu-label"]').textContent === 'Managed menu'
                    && item.classList.contains('browser-dropdown-item')
                    && item.querySelector('[data-slot="dropdown-menu-shortcut"]').textContent === '⌘M'
                    && checkbox.getAttribute('aria-checked') === 'true'
                    && checkbox.querySelector('[data-slot="dropdown-menu-checkbox-item-indicator"] svg.lucide') !== null
                    && content.querySelector('[data-slot="dropdown-menu-separator"]') !== null
                    && radio.getAttribute('aria-checked') === 'true';
                  item.click();
                  return valid;
                })()
                """);
            if (openDropdown.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Dropdown Menu did not render its item and selection composition.");
            }
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=dropdown-menu-content]') === null",
                "fecscn dropdown menu item selection");
            var comboboxPoint = await browser.EvaluateAsync("""
                (() => {
                  const trigger = document.querySelector('.browser-combobox-input[data-slot="input-group"] [data-slot="combobox-trigger"]');
                  trigger.scrollIntoView({ block: 'center' });
                  const rect = trigger.getBoundingClientRect();
                  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
                })()
                """);
            var comboboxX = comboboxPoint.GetProperty("x").GetDouble();
            var comboboxY = comboboxPoint.GetProperty("y").GetDouble();
            await browser.SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x = comboboxX, y = comboboxY, button = "left", clickCount = 1 });
            await browser.SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x = comboboxX, y = comboboxY, button = "left", clickCount = 1 });
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=combobox-content]') !== null",
                "fecscn combobox opening");
            var openCombobox = await browser.EvaluateAsync("""
                (() => {
                  const content = document.querySelector('[data-slot="combobox-content"]');
                  const items = [...content.querySelectorAll('[data-slot="combobox-item"]')];
                  const valid = content.classList.contains('browser-combobox-content')
                    && content.querySelector('[data-slot="combobox-list"]').classList.contains('browser-combobox-list')
                    && content.querySelector('[data-slot="combobox-group"]') !== null
                    && content.querySelector('[data-slot="combobox-label"]').textContent === 'Managed fruit'
                    && content.querySelector('[data-slot="combobox-separator"]') !== null
                    && content.querySelector('[data-slot="combobox-empty"]').textContent.includes('No fruit found')
                    && items.length === 2
                    && items[0].textContent.includes('Apple')
                    && items[0].querySelector('[data-slot="combobox-item-indicator"]') !== null
                    && items[1].textContent.includes('Banana')
                    && items[1].classList.contains('browser-combobox-item');
                  items[1].click();
                  return valid;
                })()
                """);
            if (openCombobox.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Combobox did not render its popup collection.");
            }
            var comboboxClosed = await browser.EvaluateAsync("""
                (() => {
                  const content = document.querySelector('[data-slot="combobox-content"]');
                  const input = document.querySelector('input[role="combobox"][aria-label="Managed combobox"]');
                  return input.getAttribute('aria-expanded') === 'false'
                    && input.value.toLowerCase().includes('banana')
                    && content.hasAttribute('data-closed');
                })()
                """);
            if (comboboxClosed.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Combobox did not close after item selection.");
            }
            var comboboxEvent = await browser.EvaluateAsync(
                "document.body.getAttribute('data-combobox-clicks') === '1'");
            if (comboboxEvent.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Combobox did not deliver its managed item event.");
            }
            await browser.EvaluateAsync("""
                (() => {
                  document.querySelector('[data-slot="button"]').click();
                  const input = document.querySelector('[data-slot="input"]');
                  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
                  setter.call(input, 'changed');
                  input.dispatchEvent(new Event('input', { bubbles: true }));
                  const textarea = document.querySelector('textarea');
                  const textareaSetter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set;
                  textareaSetter.call(textarea, 'expanded');
                  textarea.dispatchEvent(new Event('input', { bubbles: true }));
                  document.querySelector('[data-slot="checkbox"]').click();
                  document.querySelector('[data-slot="accordion-trigger"]').click();
                  document.querySelector('[data-slot="select-trigger"]').click();
                  return true;
                })()
                """);
            await browser.WaitForAsync(
                "document.querySelector('[data-slot=select-trigger]')?.getAttribute('aria-expanded') === 'true'"
                    + " && document.querySelector('[data-slot=select-content]') !== null"
                    + " && document.querySelectorAll('[data-slot=select-item]').length === 2",
                "fecscn select popup opening");
            var openSelect = await browser.EvaluateAsync("""
                (() => {
                  const content = document.querySelector('[data-slot="select-content"]');
                  const items = [...document.querySelectorAll('[data-slot="select-item"]')];
                  const valid = content.classList.contains('browser-select-content')
                    && content.querySelector('[data-slot="select-group"]').classList.contains('browser-select-group')
                    && content.querySelector('[data-slot="select-label"]').textContent === 'Managed choices'
                    && content.querySelector('[data-slot="select-separator"]') !== null
                    && items[0].textContent.includes('Alpha')
                    && items[1].textContent.includes('Beta')
                    && items[1].classList.contains('browser-select-item');
                  items[1].click();
                  return valid;
                })()
                """);
            if (openSelect.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Select did not render its popup composition.");
            }
            await browser.WaitForAsync(
                "document.body?.getAttribute('data-fecscn-status') === 'ready'"
                    + " && document.querySelector('[data-slot=checkbox]')?.getAttribute('aria-checked') === 'true'"
                    + " && document.querySelector('[data-slot=checkbox-indicator] svg.lucide') !== null"
                    + " && document.querySelector('[data-slot=accordion-trigger]')?.getAttribute('aria-expanded') === 'true'"
                    + " && document.querySelector('[data-slot=accordion-content]')?.textContent.trim() === 'Managed accordion content'"
                    + " && document.querySelector('[data-slot=accordion-content]')?.firstElementChild.classList.contains('browser-accordion-content')"
                    + " && document.querySelector('[data-slot=select-trigger]')?.getAttribute('aria-expanded') === 'false'"
                    + " && document.querySelector('[data-slot=select-trigger]')?.textContent.toLowerCase().includes('beta')",
                "fecscn managed click, input, checkbox, accordion, and select events");
            var comboboxSelection = await browser.EvaluateAsync(
                "document.querySelector('input[role=combobox][aria-label=\"Managed combobox\"]')?.value.toLowerCase().includes('banana')");
            if (comboboxSelection.ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new InvalidDataException("fecscn Combobox did not retain its selected value.");
            }
            if (browser.Exceptions.Count != 0)
            {
                throw new InvalidDataException("Browser exceptions:\n" + string.Join("\n", browser.Exceptions));
            }
            Console.WriteLine("Verified fecscn Card, Input, Textarea, Input Group, Spinner, Breadcrumb, Progress, Checkbox, Avatar, Accordion, Select, Combobox, Chart, Tooltip, Dialog, Sheet, Toast, Drawer, Dropdown Menu, Button Group, Separator, Button, cn, Base UI, and Recharts behavior in Chromium.");
        }
        finally
        {
            Directory.Delete(output, recursive: true);
            Directory.Delete(profile, recursive: true);
        }
    }

    private static string TemporaryDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void VerifyLucideReachability(
        string repositoryRoot,
        string compilerPath,
        string fixtureProject,
        string fixtureAssembly)
    {
        var output = TemporaryDirectory("FrontEndCSharp-lucide-reachability-");
        try
        {
            RunDotNet(repositoryRoot, compilerPath, "--no-bundle", fixtureProject, output, fixtureAssembly);
            var emittedJavaScript = string.Join(
                '\n',
                Directory.EnumerateFiles(output, "*.js", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));
            if (!emittedJavaScript.Contains(
                    "Interop.JS.LucideIcons.LucideIconImports.CheckIcon()",
                    StringComparison.Ordinal)
                || !emittedJavaScript.Contains("from 'lucide-react'", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Direct Lucide facade composition did not emit the selected CheckIcon override.");
            }

            var unusedIcons = new[]
            {
                "ActivityIcon", "ChevronDownIcon", "ChevronRightIcon", "ChevronUpIcon",
                "CloudLightningIcon", "CircleCheckIcon", "CrosshairIcon", "InfoIcon", "LoaderCircleIcon", "EllipsisIcon",
                "OctagonXIcon", "TriangleExclamationPointIcon", "XIcon"
            };
            var emittedUnusedIcons = unusedIcons.Where(icon => emittedJavaScript.Contains(
                $"Interop.JS.LucideIcons.LucideIconImports.{icon}()",
                StringComparison.Ordinal)).ToArray();
            if (emittedUnusedIcons.Length != 0)
            {
                throw new InvalidDataException(
                    "The direct Lucide fixture emitted unreachable icon overrides: "
                        + string.Join(", ", emittedUnusedIcons));
            }
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static void RunDotNet(string workingDirectory, params string[] arguments) =>
        BoundedProcess.Run("dotnet", workingDirectory, BuildTimeout, arguments);
}
