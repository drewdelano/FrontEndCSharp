# Managed UI Interoperability

This document describes the selected direction for connecting managed C# code to JavaScript UI and state libraries. The browser, React, MobX, MobX–React, Motion, Lucide Icons, and shadcn-scoped Base UI, `cn`, and Recharts adapter projects establish the assembly and package boundary. Most API surfaces remain intentionally small while browser-level behavior is expanded and verified; Lucide is the deliberate exception, with a generated canonical icon catalog.

## Objective

Provide a typed C# surface for browser UI libraries without making React, MobX, JSX, Razor, or any other framework part of the compiler or MSIL runtime.

The compiler should continue to understand managed assemblies, metadata, MSIL, and the existing runtime-override contract. Framework behavior should enter through explicitly referenced managed companion assemblies whose JavaScript implementations call the corresponding JavaScript libraries.

## Architectural boundary

The compiler and core runtime should remain framework-neutral. They may provide only the general interoperability primitives required by any JavaScript library:

- Stable managed object and delegate identity across the JavaScript boundary.
- Explicit lifetime and disposal behavior for retained objects, callbacks, and subscriptions.
- Managed `Task` and JavaScript `Promise` interoperation without losing managed exception behavior.
- Defined value, collection, and DTO marshaling rules.
- Synchronous callback results where the host API requires an immediate decision, such as event cancellation.
- A framework-neutral way for an adapter to observe managed state changes, if state adapters prove to require one.

React reconciliation, MobX observability, component lifecycles, hooks, and library-specific scheduling do not belong in those primitives.

## Use the existing `*.fecs.dll` model

UI integrations can use the runtime-override assembly mechanism already described
in [`../compiler/RUNTIME_OVERRIDE_ASSEMBLIES.md`](../compiler/RUNTIME_OVERRIDE_ASSEMBLIES.md):

1. A companion assembly exposes ordinary typed C# classes and methods.
2. Provider metadata identifies the managed signatures implemented at the JavaScript boundary.
3. The embedded signature-hash manifest maps each signature to its embedded JavaScript module.
4. That JavaScript module imports and calls the external library.
5. An application selects the integration through an ordinary `ProjectReference` or `PackageReference`; dependency-graph discovery selects the companion.

This keeps library selection explicit and prevents a global built-in set of UI integrations. The implemented separation is:

- `Interop.JS.Browser.dll` plus `Interop.JS.Browser.fecs.dll` for document, window, DOM nodes, events, timers, and animation-frame callbacks.
- `Interop.JS.React.dll` plus `Interop.JS.React.fecs.dll` for typed props, element/component creation, and client roots.
- `Interop.JS.MobX.dll` plus `Interop.JS.MobX.fecs.dll` for observable boxes, actions, autorun, and reaction disposal.
- `Interop.JS.MobX.React.dll` plus `Interop.JS.MobX.React.fecs.dll` for MobX observer components without coupling either base adapter to the other.
- `Interop.JS.Motion.dll` plus `Interop.JS.Motion.fecs.dll` for Motion-for-React elements, animation targets, transitions, hover state, and layout.
- `Interop.JS.LucideIcons.dll` plus `Interop.JS.LucideIcons.fecs.dll` for the
  reviewed `lucide-react` icons used by the selected profile and direct application
  composition through shared typed icon properties.
- `Interop.JS.Shadcn.BaseUI.dll` plus `Interop.JS.Shadcn.BaseUI.fecs.dll` for the selected `@base-ui/react` primitives and Toast manager behavior.
- `Interop.JS.Shadcn.Cn.dll` plus `Interop.JS.Shadcn.Cn.fecs.dll` for the locked `cn` class-merging function.
- `Interop.JS.Shadcn.Recharts.dll` plus `Interop.JS.Shadcn.Recharts.fecs.dll` for the selected responsive chart elements and typed-data conversion boundary.

The `Interop.JS.Shadcn.*` prefix is an ownership and scope signal: these adapters
expose only the upstream exports selected by the pinned FECSN/shadcn profile. They
remain separate assemblies with separate upstream package graphs, versions, and
tree-shaken reachability; the prefix does not combine Base UI, `cn`, and Recharts
into one runtime package or claim general support for any of those libraries.

The public façades use library-shaped names such as `LucideIcons.CheckIcon`,
`BaseUI.Button`, `ClassNames.Merge(...)`, and `Recharts.Bar`. Their signature-bearing raw import
classes are internal implementation details exposed only to the paired companion
through `InternalsVisibleTo`; applications and copied registry sources do not bind
to those provider types.

The Lucide adapter generates all 2,032 non-deprecated canonical icons from the
`lucide-react@1.72.0` ESM modules that directly call `createLucideIcon`. Alias-only
re-export modules and icon components marked deprecated in the package declarations
are excluded, leaving one public
`LucideIcons.<Name>Icon` facade per upstream icon-data identity. Every facade is a
concrete FECSX-compatible nested component over its own static import, so unused
icons remain unreachable from browser output. `LucideIconProps` shares typed
class, accessibility, color, size, stroke, absolute-stroke, and data-slot
properties while retaining the ordinary React property escape hatch. Dynamic
name lookup, `DynamicIcon`, base-icon helpers, and the non-icon Lucide API remain
outside this surface.
The registry attaches the Lucide managed/companion pair only to Spinner,
Breadcrumb, Checkbox, Accordion, Select, Dialog, Sheet, Toast, Dropdown Menu,
and Combobox, whose copied sources directly compose those concrete facades.
Registry items that do not render an icon do not acquire the Lucide packages.
The React demo also references the adapter directly for its Activity, Cloud
Lightning, and Crosshair panel accents; those demo-owned uses do not broaden
registry dependencies.

The browser globals use direct static C# façades: `Document.Body`, `Document.CreateElement(...)`, `Window.InnerWidth`, and `Window.SetTimeout(...)`. They do not expose a `Web` container, a `Current` singleton property, or artificial managed window/document handle objects. DOM nodes and events remain wrapped instance objects because applications pass those values between APIs.

The initial browser surface covers common application plumbing rather than attempting to mirror every Web API: window timers, intervals, microtasks, and animation frames; document roots, readiness, focus, element and text-node creation, and selectors; node traversal and mutation; element identity, classes, text/HTML, attributes, scoped selectors, focus, and click events; and event cancellation and propagation state. Each public browser type and each companion provider lives in its own source file so the surface can grow without recreating a monolithic wrapper.

Form callbacks also expose the event's nullable `CurrentTarget` and the string
`Value` of a form-control element. `Value` fails closed for elements that do not
provide a string-valued DOM property. This lets managed React `onInput` handlers
update managed models from the live control value without treating an HTML
attribute as mutable form state.

Each adapter folder also contains a singular `.Test` project (for example, `Interop.JS.MobX.Test`). These contract tests validate the managed/companion assembly pairing and every manifest signature-to-resource hash. The framework override folders use the same layout with `System.Console.Test` and `System.Private.CoreLib.Test`.

The React/MobX convenience adapter remains optional rather than becoming a dependency of either base adapter.

An integration for another renderer or state library should be addable as another companion dependency without compiler branches.

## JavaScript dependency metadata

A UI companion declares the JavaScript packages with which its embedded implementations interoperate through repeatable `JavaScriptPackageDependencyAttribute` assembly metadata.

Each companion build uses a checked-in npm lock to embed the package graph it owns. The compiler aggregates declarations from selected companions, rejects incompatible exact versions, merges identical dependency/peer declarations, and extracts the selected package archives only into its disposable webpack directory. React and React DOM are owned by the React companion and declared as peers by Motion, ensuring one framework instance. No npm packages are copied to the published browser directory.

Separate companion packages or entry points are preferable to one package with unconditional imports and nominally optional peers. An optional peer declaration affects package installation; it does not make an unconditional JavaScript import optional.

## Components and event callbacks

The first useful bridge should establish component and callback semantics before selecting a new source syntax. Important behavior includes:

- Typed component properties.
- Stable delegate-to-function wrappers so add/remove and memoization observe consistent identity.
- Explicit release of wrappers and subscriptions when a component or managed owner is disposed.
- Observation of faulted asynchronous event handlers rather than discarded rejections.
- An explicit rule for event APIs that require synchronous `preventDefault`, propagation, or return-value decisions.

React should remain the owner of its render tree and reconciliation. FrontEndCSharp should expose managed values and callbacks to it, not introduce a second renderer competing for the same tree.

Managed component composition uses `ReactComponent<TProps>`. `React.Component<TProps>` accepts a synchronous managed render delegate with a strongly typed props object and the component's `ReactNode[]` children. `React.CreateElement<TProps>` retains the original managed props and children while exposing the resulting element to ordinary React reconciliation. Define component handles once outside other render callbacks; recreating a component handle during render gives React a new component type and forces a remount.

`ReactProps` models React's one flat host-props object. Common DOM properties and events (`Id`, `ClassName`, `Type`, `AriaLabel`, `OnClick`, and the other declared members) are the primary API and retain their C# types. Every typed setter writes the same managed property bag exposed through `Properties`; that focused dictionary surface is the escape hatch for `data-*`, uncommon attributes, and library-defined values. The legacy `SetString`, `SetNumber`, `SetBoolean`, and `SetEventHandler` methods write that bag as well. Host element creation materializes one native props object at the React boundary, and managed component creation snapshots the bag so later managed mutations do not alter an already-created React element. Application component contracts should still use ordinary managed classes so C# enforces their complete shape. Mount React into a dedicated container whenever the document also has non-React-owned nodes; rendering at `Document.Body` lets a later React reconciliation remove those siblings.

An adapter that statically imports an external React component uses an opaque
`ImportedReactComponent` internally. `React.CreateElement` has a distinct overload
for that handle which materializes the ordinary `ReactProps` bag without adding
the private managed-component marker. A library adapter exposes concrete FECSX
façades such as `BaseUI.Button`, `BaseUI.Progress.Root`, and `Recharts.Bar`;
application and registry sources do not name the generic handle or
`ImportedReactElement`. External-library adapters identify only the imported
module exports; component-specific properties and variants remain in
application-owned component sources.

## shadcn/ui integration direction

shadcn/ui is a source distribution system rather than a conventional component package. Its components, registry dependencies, CSS variables, and JavaScript dependencies are copied into an application so the application owns and may edit them. FrontEndCSharp should preserve that ownership model instead of hiding a snapshot of the entire catalog behind one immutable runtime adapter.

FrontEndCSharp publishes its own `fecscn` registry derived from upstream shadcn
items. Maintainers import the upstream metadata and source identity, adapt each
selected component deliberately into typed C# and FECSX, and publish the resulting
owned sources through shadcn-shaped static registry items. This is not a general
TSX-to-FECSX translator or a compatibility promise for arbitrary shadcn or third-
party registry source. The upstream item address and digest remain recorded so a
port can be audited and refreshed deliberately.

Application developers use the C# `fecscn` tool with the familiar initial workflow:

- `fecscn init` configures an existing FECSX React project for the registry and its
  CSS toolchain.
- `fecscn add <item...>` resolves `registryDependencies` from the `fecscn`
  registry and copies the selected `.fecsx`, `.cs`, and declared asset sources into
  the application.

The application owns those copied component sources. The tool records the registry
item identity and source digest and must not overwrite a locally changed file
silently. Registry access occurs only during an explicit `fecscn` command, never
during a normal application build or at browser runtime. Search, diff, update,
presets, migrations, arbitrary registries, and the rest of the upstream CLI surface
are outside the first slice. shadcn AI Elements, agent/chat interface patterns, and
their `@shadcn/react` component surface are outside the `fecscn` scope rather than a
later tranche of the initial registry.

The initial registry supports one pinned profile: the current upstream defaults of
Base UI, Nova, Lucide, Tailwind CSS v4, CSS variables, and non-RSC React output.
`fecscn` must not inherit a changed upstream default implicitly. Radix, React Aria,
other visual styles, other icon libraries, RTL transforms, and user-selected
profiles require later scope decisions.

Tailwind integration lives in the focused `FrontEndCSharp.Tailwind` build package, which ships
and invokes a pinned, otherwise unmodified upstream Tailwind CSS v4 toolchain;
FrontEndCSharp does not fork or reimplement Tailwind. The package scans explicit
application and imported-registry `*.fecsx` and `*.cs` inputs, generates one static
CSS asset during build, and makes the source set and complete tool configuration
part of the browser-build fingerprint. Release output is minified and contains the
utilities discovered from those inputs without carrying unused utility output. The
build requires no registry or package-network access after restore, and the browser
never runs Tailwind. CSS variables are the default theming boundary, with the
generated global theme asset loaded once and application-owned overrides layered
after it.

Copied-source ecosystems do not expose trustworthy per-component install counts:
npm downloads measure the underlying packages, not which shadcn sources an
application copied. Prioritization therefore starts with the official default
registry's dependency graph and implementation complexity. Public code-search
counts may be recorded only as a dated, biased secondary signal. In the current
default registry, Button, Input, and Card are high-centrality component dependencies
with a useful spread of implementation difficulty. They form the implemented first
vertical slice: Button and Input exercise the pinned Base UI boundary, variants,
class merging, form properties, and managed events; Card exercises a source-only
multi-export component. The next phased registry work begins with the implemented
source-only Textarea, which preserves the Nova classes, typed form-control props,
and managed input/change events and supplies a required dependency for the later
Input Group and Combobox ports. Spinner composes the canonical Loader Circle facade as
an accessible status icon and preserves the Nova size and animation contract.
The Breadcrumb port exposes the upstream navigation, list, item,
link, current-page, separator, and ellipsis parts as named FECSX components. It
retains the accessibility roles and current/disabled/hidden ARIA state and uses
the canonical Chevron Right and Ellipsis facades. Its link uses the
ordinary typed anchor contract; the upstream polymorphic `render`
escape hatch remains outside this bounded port rather than introducing a general
Base UI `useRender` abstraction.
Progress establishes the first compound imported Base UI component: its opaque
Root, Track, Indicator, Label, and Value handles share the upstream context while
application-owned FECSX and C# retain the Nova classes and typed nullable numeric
value. This is a selected Progress binding, not a general Base UI namespace API.
Checkbox similarly imports only Base UI Root and Indicator. Its application-owned
props distinguish omitted, controlled, and default checked state, retain ordinary
form and disabled properties, and deliver managed click events. The upstream
indicator is represented by the reviewed Check facade.
Avatar imports only Base UI Root, Image, and Fallback. Application-owned helpers
retain the upstream badge, group, and group-count composition and expose the
profile's default, small, and large size states without adding a general Avatar
or image-loading facade to the companion.
Accordion imports only Base UI Root, Item, Header, Trigger, and Panel. Its typed
application-owned props cover multiple expansion, disabled state, item values,
trigger events, and persistent mounting. The source retains the Nova transition
classes and composes the reviewed Chevron Down and Chevron Up facades.
Select establishes the selected popup-composition boundary for Root, Group,
Value, Trigger, Icon, Portal, Positioner, Popup, List, label/item/indicator,
separator, and scroll-arrow parts. Application-owned props preserve controlled
versus default values, form state, the Nova trigger sizes, popup placement, and
managed item clicks; the reviewed Check and Chevron facades retain the icon roles.
Tooltip imports the bounded Provider, Root, Trigger, Portal, Positioner, Popup,
and Arrow parts. Its typed props preserve provider delays, controlled/default
open state, placement, managed trigger events, and the Nova popup/arrow styling.
Dialog imports only Root, Trigger, Portal, Close, Backdrop, Popup, Title, and
Description. Source-owned Header and Footer composition retains the Nova layout;
native Base UI close buttons reuse the installed Button style contract without a
general polymorphic-render API. Typed props preserve controlled/default open
state, disabled actions, managed events, and optional content/footer close actions;
the content close action composes the reviewed X facade.
Sheet reuses that exact Dialog import surface and Button style contract. Its
application-owned API adds the four typed slide sides and preserves the Nova
side-specific transitions, overlay, header/footer, title/description, and optional
close action with the same reviewed X facade without adding duplicate runtime overrides.
Toast uses one selected manager adapter because Base UI projects its manager hook
into a dynamic list rather than exposing a declarative open-state root. Managed
code owns the typed notification API and status enum, and registry sources pass
all Nova classes plus the reviewed status/close icon elements into the adapter.
The host treats those elements as reserved child templates, so the Base UI
companion does not acquire an undeclared dependency on Lucide; it owns only the
singleton manager, list projection, and close dispatch. This is not a general
hook or toast-manager interop surface.
Drawer imports its bounded Root, Trigger, Portal, Close, Backdrop, Viewport,
Popup, Content, Title, and Description parts. Application-owned composition adds
the swipe handle and typed four-direction/axis contract while retaining managed
open/close events and the Nova swipe layout and transition classes.
Dropdown Menu imports the selected Menu root/portal/trigger/positioner/popup,
group/label/item, submenu, checkbox/radio selection, indicator, and separator
parts. Source-owned composition retains shortcuts while reviewed Check and
Chevron Right facades supply its indicators;
typed props cover placement, inset/destructive states, controlled selection,
disabled state, and managed actions without a general Menu namespace binding.
Button Group adds its transitive Separator registry item and imports only the
Base UI Separator primitive. Source-owned group, text, and separator composition
retains the horizontal and vertical Nova class variants through one typed
orientation enum. This does not add the upstream polymorphic `useRender` escape
hatch or broaden Separator into a general Base UI binding.
Combobox adds its source-owned Input Group dependency, which composes the existing
Button, Input, and Textarea boundaries. The selected Base UI imports cover root,
value/input/actions, popup positioning, list/group/collection/empty/separator,
and chip composition. Typed source props retain controlled/default selection and
open state, input actions, placement, disabled items, and managed events. Reviewed
Check, Chevron Down, and X facades supply its indicators and actions without
exposing Base UI filtering hooks or arbitrary render/ref escape hatches.
Chart adds one deliberately bounded Recharts 3.8.0 vertical slice: responsive bar
charts with Cartesian grid, X axis, tooltip, and legend. Application-owned props
retain typed `ChartDataPoint` input, dimensions, keys, labels, color, and visibility
flags. The import adapter converts the typed points to native Recharts data at the
BarChart boundary because ordinary `ReactProps` intentionally reject arbitrary
managed object graphs. This is not a general Recharts facade. The companion pins
Recharts' required `react-is@19.3.1` peer as an embedded direct dependency because
package archives are restored with peer dependencies omitted.

The repository-owned registry, tool, Tailwind integration, and copied source
scaffolding are under `Fecscn/`. The versioned authoring
document records the upstream `base-nova` addresses and digests, while the C#
registry builder validates and emits deterministic static `index.json` and item
documents. `fecscn init` discovers one host/browser project pair, writes the fixed
profile configuration and Tailwind input, and adds the required package references
idempotently. `fecscn add` fetches only the configured registry, resolves and
validates its complete dependency plan before mutation, and refuses target escapes,
cycles, digest mismatches, package conflicts, or locally changed destinations.

The three upstream-owned import boundaries expose only opaque handles for the selected Base UI Button,
Input, Progress parts, Checkbox parts, Avatar parts, Accordion parts, Select
parts, Tooltip parts, and the shared Dialog/Sheet parts plus the selected Toast
manager adapter and Drawer parts; the `cn` boundary exposes only class merging;
the Recharts boundary exposes only the chart graph used by the selected port. Registry sources
also expose the bounded Dropdown Menu and Combobox parts plus Separator used by
Button Group, along with the bounded Recharts graph handles used by Chart.
They construct ordinary `ReactProps` and render through the concrete nested
component façades; opaque imported handles and raw override signatures remain
internal to the adapter pair. The adapters do not publish shared component-specific
prop types. The Base UI companion embeds `@base-ui/react@1.9.0`, the Cn companion
embeds `cn@0.5.2`, and the Recharts companion embeds `recharts@3.8.0` with its
private `react-is@19.3.1` runtime dependency. Each adapter pair is added through
registry package dependencies only when an installed item needs that upstream
package, not by `fecscn init`; source-only items need no Base UI or Recharts
adapter. These remain selected surfaces rather than general compatibility layers.
The `fecscn` command implementation lives directly in its global-tool project rather
than a separately published core library. The C# fixtures separately prove the
registry/add transaction and offline initialized restore/build, while
`fecscn-browser-smoke` compiles the same reviewed sources and verifies their
composition, generic prop forwarding, class conflict resolution, disabled/value
state, children, Textarea form state, Spinner accessibility and class merging,
Breadcrumb navigation semantics, compound Progress value/ARIA behavior, and managed
click/input and Checkbox state-transition events, plus Avatar image/fallback and
group composition, Accordion expansion/disabled behavior, and Select portal,
selection, and popup composition, plus Tooltip hover opening, placement, arrow,
and closing behavior and Dialog modal open/close and labelled composition, in
addition to Sheet side-specific open/close composition, in real Chromium.
The same fixture proves managed Toast creation, typed status glyphs, content,
viewport styling, and close removal.
Drawer coverage proves its modal overlay/viewport/popup/content nesting, swipe
direction and axis data, handle, labelled structure, and close behavior.
Dropdown Menu coverage proves real pointer opening, group/label/action/shortcut
composition, checkbox and radio indicators, and close-on-action behavior.
Button Group and Combobox coverage proves their transitive Separator/Input Group
composition and real selection behavior. Chart coverage proves the typed managed
data boundary, responsive Recharts SVG, three bars, category ticks, grid, legend,
and pinned color in real Chromium.

## State integration alternatives

Managed state and UI rendering do not require one universal adapter. At least two distinct models are plausible.

### React external-store adapter

C# remains the authoritative state owner. A React adapter exposes subscription and snapshot behavior compatible with React's external-store model. The adapter is responsible for stable snapshots, versioning, subscription cleanup, and rendering updates.

### MobX adapter

The MobX companion exposes `MakeAutoObservable<T>(T)` for ordinary managed models. It annotates the model's existing backing storage in place, returns the same managed reference, and lets reactions track normal C# property reads and writes. MobX retains its upstream `Observed` action-enforcement default unless an application explicitly calls `MobX.Configure(MobXActionEnforcement.Never)` once during startup. The relaxed mode permits direct managed property writes without an additional action wrapper. `RunInAction` remains an adapter API for consumers that explicitly require a MobX transaction, but the React demo does not use it. `Box<T>` remains the standalone-value API and returns `ObservableBox<T>`, whose `Value` property represents MobX's `get()` and `set()` operations.

Automatic model observability covers stored managed properties. A future generated or explicit facade can extend that baseline when an adapter needs deliberate computed-value classification, write validation, specialized marshalling, or subscription ownership.

React consumption is provided separately through `Interop.JS.MobX.React`. `MobXReact.Observer<TProps>` wraps a typed managed function component with `mobx-react-lite`'s `observer`. `MobXReact.Observe(Func<ReactNode>)` creates the library's stable `Observer` render-region component and executes the managed callback under its reaction, allowing a FECSX body to use `MobXReact.Observe(() => <markup>)` without manufacturing a component type during render. Managed property reads performed inside either boundary establish dependencies and rerender only that observer boundary. MobX without React and React without MobX remain valid configurations.

Motion remains a React element producer. `MotionProps` inherits `ReactProps`, so an animated element uses the same typed DOM properties, events, and fallback dictionary alongside typed Motion properties such as `Initial`, `Animate`, `Transition`, and `Layout`. Motion materializes that single inherited bag for `motion.create(...)`; it does not maintain or merge a second React-props object.

These models have different mutation, equality, dependency-tracking, computed-value, and asynchronous semantics. They should be treated as alternative adapters over common interoperability primitives rather than collapsed into one compiler feature.

## Source syntax

A TSX-like authoring experience for C# remains worth exploring, but syntax should follow the proven interoperation model rather than define it prematurely.

- `.cshtml` is oriented toward producing server-rendered text and does not naturally express retained client event delegates.
- `.razor` has a component and event vocabulary closer to the desired experience, but adopting Razor or Blazor rendering would introduce a second component model and currently conflicts with the repository's Razor anti-goal.
- A future C#-native syntax, generator, or generated binding surface could target the same framework-neutral ABI without changing its runtime semantics.

The initial bridge can therefore be exercised from ordinary C# plus generated or handwritten typed bindings. A syntax decision can be made after component properties, callbacks, identity, lifetime, and state notification have been validated.

## Initial-scope boundary

The first implementation is deliberately narrow:

- Typed managed calls into a JavaScript UI library.
- Managed values supplied as component inputs.
- Stable managed event callbacks.
- Locked, selected JavaScript package graphs bundled from companion resources.

The C# `FrontEndCSharp.Compiler.Integration javascript-interop-browser-smoke` command compiles and serves the combined fixture in real Chromium. It verifies raw document mutation, typed managed props and their dictionary fallback, typed children, managed React and Motion click callbacks, direct MobX mutations with action enforcement disabled, autorun behavior and reaction disposal, both a whole-component observer rerender and an `Observer` render-region rerender, and Motion-for-React rendering with inherited DOM props in one application. Asynchronous callback failure observation and explicit component-unmount lifetime tests remain later hardening work rather than assumptions in the current contract.

The following should remain stretch goals or explicit non-goals until separately approved:

- MobX observability and generated observable facades.
- React external-store hooks.
- Automatic generation of React hooks or component bindings beyond the explicit typed and observer APIs.
- A new TSX-like C# syntax.
- Razor or Blazor integration.
- Server-side rendering and hydration.
- Automatic inference of actions or computed properties from arbitrary C# members.
- A universal abstraction over every JavaScript UI or state library.

`Demo/FrontEndCSharp.ReactDemo/` exercises the complete composition model in the
Orbit dashboard: typed parent/child component boundaries, inline `Observe` regions
owning state reads, direct observable-state callbacks under explicit `Never` action
enforcement, Motion elements carrying React props, responsive styling, and
independent observer-region rerenders. It owns installed copies of the fecscn Card,
Input, Button, Button Group, Separator, Progress, and class-merging sources. Focus
actions use Button, while the opening panel's responsive live-snapshot sidecar
composes the Button Group energy selector, session length, task metrics, and focus
state. Its component-local grid, flex, spacing, metric, and breakpoint rules use
tree-shaken Tailwind utilities in FECSX; the application stylesheet retains the
Orbit-specific layered surfaces, typography, state selectors, pseudo-elements,
and animation. Task completion uses the compound Progress control, and the
focus-intent panel composes Card, Input, and Button while retaining its controlled
form and managed validation behavior. `Demo/FrontEndCSharp.Demo/` builds the browser
bundle and tree-shaken Tailwind asset and serves Orbit at `/react` alongside the
simple managed demo at `/simple`. The combined Chromium regression proves the
installed control markers, generated styles, imported component rendering, input
events, submission, MobX, and Motion in that page.

## Decision criteria before implementation

The first implementation answers the packaging and compiler-boundary questions. Remaining API work should answer:

- Which object-identity and lifetime guarantees must the core interoperability layer expose?
- Can event cancellation and asynchronous failures preserve both host-library and managed semantics?
- What is the minimum framework-neutral state-change primitive, if one is needed at all?
- Can React-only, MobX-only, React-plus-MobX, and an unrelated adapter coexist without loading unused libraries?
