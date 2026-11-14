# Orbit React + MobX demo

Orbit demonstrates FECSX component composition across React, fecscn, Tailwind CSS, MobX, Motion, browser `HttpClient`, `System.Net.Http.Json`, and managed DataAnnotations validation. Store-reading component bodies use inline `MobXReact.Observe(() => <markup>)` regions, while generated typed `ReactComponent<TProps>` boundaries and ordinary component facades continue to own composition. The application owns copies of the fecscn Card, Input, Button, Button Group, Separator, and Progress registry sources. The opening focus panel uses a responsive side-by-side composition: focus copy and actions remain on the left, while a live snapshot on the right composes the segmented Button Group energy selector, session length, task metrics, and focus state. Task completion uses the compound Progress control, and the focus-intent panel composes Card, Input, and Button. Imported controls render the pinned Base UI exports through the narrow React-import companion. The shared host's Tailwind build scans the demo's `.fecsx` and `.cs` files and serves the tree-shaken `fecscn.css` asset. The page also uses a managed Motion component facade carrying ordinary React DOM props and a live Open-Meteo current-weather panel populated through a typed managed HTTP/JSON request. A bounded three-field focus-intent form gets its invalid and valid states from `Validator.TryValidateObject`. The form includes an integer priority constrained to 1 through 3 while retaining out-of-range textbox input so the user can see and correct it. Label/input IDs and field-error lookups use direct property expressions such as `FormField<FocusIntentForm>.IdFor(model => model.FocusIntent)` instead of handwritten property-name strings.

Run it from the repository root:

```powershell
dotnet run --project Demo/FrontEndCSharp.Demo/FrontEndCSharp.Demo.csproj
```

The shared web project builds both managed demo programs, compiles them through FrontEndCSharp, and serves Orbit at `/react`. Its generated bundle lives under `wwwroot/js/react-demo`, independently of the simple demo bundle.

The component tree is in [`Components`](Components); [`Program.cs`](Program.cs) contains application state, actions, library adapters, and startup. Component-local layout, spacing, and responsive behavior use Tailwind utilities in the FECSX markup, while [`../FrontEndCSharp.Demo/wwwroot/css/react-demo.css`](../FrontEndCSharp.Demo/wwwroot/css/react-demo.css) retains the host-wide layout and Orbit-specific surfaces, typography, state selectors, pseudo-elements, and animation. The host page is [`../FrontEndCSharp.Demo/Pages/React.cshtml`](../FrontEndCSharp.Demo/Pages/React.cshtml).

The browser regression builds these FECSX inputs, compiles their MSIL, requires the Button Group and Progress controls to render, drives a MobX update, verifies required errors for all three form fields, submits a valid form, and verifies Motion output in Chromium. The demo continues to request live Open-Meteo data, but the regression does not assert volatile response values:

```powershell
dotnet run --project Compiler/Compiler.Tests/Integration/FrontEndCSharp.Compiler.Integration.csproj -- fecsx-react-demo-browser-smoke "C:\Program Files\Google\Chrome\Application\chrome.exe"
```
