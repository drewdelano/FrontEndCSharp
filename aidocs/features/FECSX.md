# FECSX React Markup

Status: the shared syntax/parser, component generator, React lowering,
transitive build package, typed intrinsic properties, converted demo,
editor-neutral JSX operations, Roslyn projection seam, Visual Studio host, and
end-to-end debugger proof exist. The Visual Studio host registers `.fecsx`,
ships the adapted grammar and language configuration, and adapts completion,
brace matching, and linked tag editing to the shared service layer.
The FECSX VSIX also ships a self-contained React application project template.

Quoted `className` and `ClassName` attribute values also participate in the
async completion host when the owning FECSX project, or its paired
FrontEndCSharp host project, has the `FrontEndCSharp.FECS.Internals.Tailwind` build integration
enabled. The package exposes its evaluated availability, opt-out, and paired
browser-project path as compiler-visible MSBuild properties. It also publishes
the project editor-catalog path; the build task, not Visual Studio, resolves the
effective Tailwind v4 CSS import graph and writes that catalog beneath `obj`.
Setting `FrontEndCSharpTailwindEnabled` to `false` suppresses both the build and
completion. Before the first successful build, the editor falls back to its
embedded complete default class list from the pinned Tailwind CSS engine. After
a build, it loads the project catalog so top-level `@theme` variables and imported
theme files determine the available utilities and variants. This includes custom
namespaces such as `--drop-shadow-card` → `drop-shadow-card` and
`--breakpoint-tablet` → `tablet:`/`max-tablet:` as defined by Tailwind's
[theme-variable contract](https://tailwindcss.com/docs/theme). Legacy
`tailwind.config.js` compatibility and unsaved CSS changes are intentionally out
of scope; rebuilding refreshes the catalog and the next completion session reloads
it. The catalog includes responsive media variants and the `hover:`, `focus:`,
and `dark:` variants. It offers `sm:`, `md:`, `lg:`, `xl:`,
`2xl:` and their `max-*:` counterparts with the exact generated media query,
and offers `hover:`, `focus:`, and `dark:` with the exact generated condition.
The built-in `dark:` completion retains Tailwind's default
`@media (prefers-color-scheme: dark)` condition. Completion
filters by the active whitespace-delimited token, preserves stacked variants
such as `md:hover:`, and shows the generated CSS in the pre-commit description.
The initial source set keeps exact variants and standalone utilities but collapses
functional utilities by their Tailwind-reported root and emitted CSS property, such
as `m-`, `p-`, or `drop-shadow-`. Broad roots are divided into property-consistent
families: repeated color scales become entries such as `bg-amber-`, while unrelated
values such as `bg-auto` remain exact. Value-less defaults such as `grow` remain
visible and sort directly beside `grow-`. A family description begins with a concise
purpose-first explanation and lists only classes present in the generated
catalog, so discontinuous scales are preserved rather than manufactured. When the
effective `--spacing` theme value is an absolute `px` or `rem` length, spacing-based
descriptions also show the corresponding pixel values; unresolved project variables
remain exact CSS instead of receiving a guessed conversion. Family descriptions and
the expanded source set include only arbitrary bracket/custom-property examples that
the pinned Tailwind design system successfully compiles. A family preview compares
the generated declarations across all of its concrete members and selects the value
that actually varies. Utilities implemented through shared CSS-variable indirection
therefore show useful values such as `ring width: 2px`, `blur: var(--blur-sm)`, or
`border-width: 2px` instead of repeating the same final `box-shadow`/`filter` rule.
Entering or committing `:` restarts the frozen
Visual Studio completion session so the following list contains prefixed utilities
such as `md:block`, rather than stale fuzzy matches from the variant token.
Entering or committing a generated family prefix similarly restarts completion so
`m-` expands to its real values and editable arbitrary templates without repopulating
the initial list with every concrete class.
Drop-shadow completions additionally use the
official Tailwind utility documentation to explain size defaults, slash opacity
modifiers, arbitrary bracket values, and the parenthesized custom-property
shorthand. Template commits place the caret inside `[]` or `()` and Tailwind
items use the official mark strictly as source attribution. Expression-valued
attributes remain C# completion contexts rather than being scanned as strings.
The open-bracket suggestion displays
`drop-shadow-[0_35px_35px_rgba(0,0,0,0.25)]` as a concrete example while still
committing editable `drop-shadow-[]`; its description shows both the authored
underscore form and the resulting space-separated CSS value. While the caret is
inside that value, the description identifies the active
`offset-x_offset-y_blur_color` parameter and explains its direction,
units, or meaning. `filter: drop-shadow()` has no spread-distance or `inset`
parameter; those belong to box shadows. The image manifest
registers the Tailwind mark as a 16-pixel image-service resource, and the VSIX
pkgdef supplies the assembly codebase required to resolve that embedded XAML.

Visual Studio freezes an async-completion source set for the lifetime of a
session, which cannot represent grammar states discovered after `/`, `[`, or `(`
is typed. A FECSX-only view controller therefore dismisses and retriggers the
active session after those state transitions and while editing an arbitrary value,
including inside the auto-closed `[]`/`()` template. Moving the caret within an
arbitrary value also retriggers an already-open session so its parameter help tracks
the caret. The editor-neutral engine then computes the exact modifier or closing
template and its current-value description. The controller does not replace the
completion item manager, so ordinary Roslyn filtering, selection, and completion
remain owned by Visual Studio.

The embedded fallback is a deterministic compressed source artifact generated
from the pinned package's IntelliSense design-system API by the C# Tailwind task
host. Each project build uses the same API against the build's effective v4 CSS
input to produce its own compressed catalog. Both contain class names, advertised
modifiers, the first generated utility rule, the most-specific functional root
reported by Tailwind's parsed candidate, the emitted CSS property used for semantic
grouping, the effective spacing value, compiled
arbitrary-value examples, and selected responsive/state variants from
`getVariants()`. Visual Studio only reads the compressed result, so
it requires neither Node.js nor network access. Regenerate the embedded fallback
with the command in `Fecscn/FrontEndCSharp.FECS.Internals.Tailwind/README.md` whenever the pinned
Tailwind version changes.

Visual Studio obtains an async-completion source set when the first character of
a class token starts the session, then filters that fixed set as later characters
are typed. It can retain that session even after the token is erased and a new
first character is typed. The FECSX source therefore seeds a one-letter session
with the complete grouped surface: exact standalone utilities, every functional
family root, and every variant. A session begun with `p-` can be erased and
retargeted to `blo` without retaining only padding candidates, while the popup no
longer carries tens of thousands of numeric and color values before a family is
selected. Visual Studio can still display only its first 300 filtered candidates,
so the source supplies a stable relationship-aware sort key that keeps exact defaults
beside their parameterized family. Descriptions remain
lazy; complete family values, pixel conversions, exact generated CSS, and
arbitrary examples are materialized only when Visual Studio highlights an item.

FECSX is a JSX-shaped authoring format for composing the managed React API with
C# expressions. It is a source-generation feature, not a second application
runtime: generated C# is compiled by Roslyn to ordinary MSIL and then enters the
existing FrontEndCSharp compiler and browser runtime unchanged.

## Goals

- Make managed React component trees concise and readable without introducing
  Razor, Blazor, TypeScript, or a parallel execution model.
- Preserve ordinary C# type checking for expressions and component properties.
- Provide useful completion and diagnostics for intrinsic HTML elements and
  managed components.
- Preserve source locations through generated C#, Portable PDBs, and the
  managed browser debugger so breakpoints can be placed in `.fecsx` files.
- Allow libraries such as Motion and MobX React to participate through normal
  component composition or extension contracts, without making them part of
  the FECSX grammar.
- Lower to the existing managed `React.CreateElement` API for compatibility.

## Non-goals

- Parsing or executing TypeScript or JavaScript.
- Replacing Roslyn's C# parser, binder, or compiler.
- Teaching the MSIL compiler about FECSX syntax.
- Reimplementing React reconciliation, JSX runtime internals, MobX, or Motion.
- Treating `.fecsx` files as `.tsx` files and accepting incorrect TypeScript
  diagnostics for embedded C#.

## Compilation pipeline

```text
*.fecsx
  -> FECSX parser and syntax tree
  -> generated *.fecsx.g.cs with #line mappings
  -> Roslyn C# compilation
  -> application DLL + Portable PDB
  -> existing FrontEndCSharp MSIL and bundling pipeline
  -> React.createElement at the JavaScript interop boundary
```

The generator should emit direct calls to `React.CreateElement`, `React.Text`,
and the existing managed component APIs. A managed equivalent of React's
automatic `jsx`/`jsxs` runtime is deliberately deferred; it is not needed to
compose components and would add a second lowering contract prematurely.

Embedded `{ ... }` regions are C# expressions. The FECSX parser locates their
boundaries and delegates their syntax and semantics to Roslyn rather than
implementing a C# parser.

## Syntax and lowering model

The file form is an explicit, C#-shaped component module. A file may contain
ordinary `using` directives and one file-scoped namespace, followed by one or
more `public static` components with the semantic contract
`Func<TProps, ReactNode[], ReactNode>`. Exactly one declaration must be named
`Default`:

```csharp
using Interop.JS.React;
namespace Demo.Components;

public static ReactNode Default(
    TaskCardProps props,
    ReactNode[] children)
{
    return <article className="task-card">
        <h2>{props.Title}</h2>
        {children}
    </article>;
}
```

The valid C# identifier in the file stem supplies the outer/default component
identity. `TaskCard.fecsx` therefore generates a `TaskCard` facade containing
one stable `ReactComponent<TaskCardProps>`, its `Component` property, and a
typed `Create` method used by `<TaskCard ...>`. `public static` makes the export
shape explicit while React, rather than a managed object instance, owns
component lifetime.

### Named components

Additional public static declarations are named components in the same file:

```csharp
// App.fecsx
using Interop.JS.React;
namespace Demo;

public static ReactNode Default(AppProps props, ReactNode[] children) =>
    <main className={props.ClassName}>{children}</main>;

public static ReactNode DarkMode(ThemeProps props, ReactNode[] children) =>
    <section className="theme-dark">{children}</section>;

public static ReactNode LightMode(ThemeProps props, ReactNode[] children) =>
    <section className="theme-light">{children}</section>;
```

The example above generates these author-facing component surfaces:

```csharp
App.Component
App.Create(...)
App.DarkMode.Component
App.DarkMode.Create(...)
App.LightMode.Component
App.LightMode.Create(...)
```

Markup therefore uses `<App>` for the default export and `<App.DarkMode>` or
`<App.LightMode>` for named exports. Each named surface is a nested generated
static facade, not an instance or a runtime lookup. This matches the existing
lowering of a qualified tag to `App.DarkMode.Create(...)`, preserves fully
typed props for every component, and adds no new React runtime contract.

The invariants are:

- exactly one `Default` method per file;
- zero or more additional uniquely named public static component methods;
- the file stem is an unescaped uppercase C# identifier and is the generated
  outer facade name;
- every method independently has the existing
  `(TProps props, ReactNode[] children) -> ReactNode` contract and exactly one
  markup root or fragment;
- named components may use different props types;
- `Default` is reserved and is exposed through the outer facade rather than as
  `App.Default`;
- a component body may use either the concise `=> <markup>;` form or the
  block-bodied `{ return <markup>; }` form; a block body contains exactly that
  one markup return statement, while arbitrary local statements and control
  flow remain ordinary C# helpers in `.cs` files;
- either body form may place its one markup root inside a zero-parameter lambda
  passed as the sole argument to a wrapper call, for example
  `=> MobXReact.Observe(() => <markup>);`; this remains one FECSX root and does
  not permit markup at arbitrary positions in general C# expressions;
- both forms require the markup expression or return statement to end with a
  semicolon, although the parser and editor identify the complete markup root
  rather than using `;` as a markup delimiter;
- ordinary helper C# remains in `.cs` files rather than becoming a second kind
  of declaration inside FECSX.

A legacy single-method fallback is intentionally unsupported: it would weaken
the required-export invariant and leave the parser and editor with two language
modes. Source mappings, Portable PDB sequence points, Roslyn projections, and
editor operations retain each component body independently. Visual Studio tag
and prop completion recurse through nested facades, and the TextMate grammar
uses recursive root scopes so classification returns to C# between component
declarations.

`Func` places its result type last: props and children are inputs, and the one
composed `ReactNode` tree is the output. Multiple top-level children therefore
use a fragment rather than changing the component result to `ReactNode[]`.

The syntax tree should follow the small, established JSX vocabulary:

- element and opening/closing element
- fragment
- text
- expression container
- attribute
- spread attribute, when that feature is added

The implementation should use recognizable lowering phases based on Babel's
JSX transform, reimplemented against FECSX syntax and Roslyn nodes:

- `ConvertJSXIdentifier`
- `ConvertAttributeValue`
- `AccumulateAttribute`
- `BuildChildren`
- `BuildPropsExpression`
- `BuildElementExpression`
- `BuildFragmentExpression`
- `GetTag`

Lowercase names identify intrinsic elements, such as `<button>`. Uppercase and
member-qualified names identify managed components, such as `<TaskCard>` and
`<Controls.TaskCard>`. This follows the familiar JSX/TypeScript distinction
while the resulting values and expressions remain entirely managed C#.

Attribute spelling is a project-level authoring preference. A browser project
may set `FrontEndCSharpFecsxAttributeCasing` to `camelCase` or `PascalCase`:

```xml
<FrontEndCSharpFecsxAttributeCasing>camelCase</FrontEndCSharpFecsxAttributeCasing>
```

The build package exposes this MSBuild property to the incremental generator,
and the Visual Studio completion providers read the same analyzer configuration.
`camelCase` uses React spellings for intrinsic attributes (`className`, `onClick`,
and the standard dashed `aria-*` forms) and lowers the first character of managed
C# property names. `PascalCase` uses the actual typed C# names (`ClassName`,
`OnClick`, `AriaLabel`, and managed component properties). The selected convention
controls completion and build validation; using the other spelling produces
`FECSX4006`. Omitting the property selects `camelCase`, and new FECSX project
templates set it explicitly. Invalid property values produce `FECSX5001` rather
than silently selecting a convention.

Managed component properties lower through the generated facade's typed
configuration lambda so Roslyn performs member lookup, conversion, and access
checking:

```csharp
TaskCard.Create(
    props =>
    {
        props.Name = task.Name;
        props.IsDone = task.IsDone;
    });
```

## Typed intrinsic HTML properties

Intrinsic markup should have a typed authoring model. `ReactProps` now provides
typed common React/DOM members backed by one managed property bag, with
`Properties` retained as the explicit escape hatch. FECSX's element-specific
types build on that baseline so the fallback dictionary is not the primary
source of completion or build-time validation.

A single `HTMLElement` type containing every possible property would technically
make properties optional, but would produce noisy completion and permit many
invalid element/property combinations. FECSX instead keeps one canonical,
publicly queryable schema in `IntrinsicElementSchema` and generates a hierarchy
of optional property types from it:

```text
HtmlProps
  |- ButtonProps
  |- InputProps
  |- AnchorProps
  |- FormProps
  `- ...
```

`HtmlProps` contains global HTML and common React properties such as `Id`,
`ClassName`, `Title`, `Role`, and common event handlers. Element-specific types
contain optional properties valid for that element, for example `Disabled` and
`Type` on `ButtonProps`, or `Value`, `Checked`, and `Placeholder` on
`InputProps`. The shared typed surface includes the common `aria-label`,
`aria-current`, `aria-disabled`, and `aria-hidden` accessibility states plus the
registry-oriented `data-slot` marker. Other `data-*`, uncommon future
attributes, and library-defined properties require an explicit escape hatch
rather than forcing every possible name into the base type.

These types are the authoring and validation contract. The FECSX lowerer emits
assignments through the element-specific type, so Roslyn checks expression
types, and its generated `Create` method performs the final conversion to
`ReactProps`. The same schema is the single source for:

- tag-to-property-type binding;
- property names and managed value types;
- React/DOM output names;
- generated managed property definitions;
- FECSX completion and diagnostics;
- conversion to the native React property object.

This preserves a typed `.fecsx` experience while retaining the common
`ReactProps` bag and its uncommon-property fallback at the final JavaScript
boundary. It also avoids making the FECSX parser infer whether
an arbitrary C# expression requires `SetString`, `SetBoolean`, `SetNumber`, or
`SetEventHandler`; Roslyn and the generated typed conversion code make that
decision. The initial schema covers the semantic elements and form controls
needed by the demo. It is intentionally reviewed in repository code rather
than copied wholesale from a browser definition whose React naming and event
types differ.

React's special `key` and `ref` values must be modeled explicitly rather than
treated as ordinary DOM properties. Attribute order and future spread
attributes must preserve React/JSX overwrite behavior.

## Project and package structure

Use four production projects, kept as direct children of the existing
`Compiler` solution folder, plus colocated test projects:

- `FrontEndCSharp.Fecsx` contains the shared parser, syntax nodes, incremental
  generator, lowering, diagnostics, source mapping, and intrinsic schema.
- `FrontEndCSharp.FECS.Internals.Fecsx.Build` packages the analyzer/generator assembly and the
  transitive MSBuild props/targets that register `*.fecsx` files as generator
  inputs.
- `FrontEndCSharp.Fecsx.Editor` contains editor-neutral language services such
  as classification, paired-tag edits, completion, formatting, and projected
  Roslyn operations. It depends on the shared FECSX language core but has no
  Visual Studio dependency, so a future LSP or another editor can reuse it.
- `FrontEndCSharp.Fecsx.VSIX` is the thin Visual Studio host and packaging
  layer: content-type registration, TextMate and Language Configuration assets,
  editor adapters, and deployment of the shared editor service.

The FECSX content type derives from Visual Studio's `code++` TextMate base. A
plain `code` base is insufficient: Visual Studio can discover and cache the
grammar while never attaching its TextMate classifier, brace, comment, or
language-configuration providers to the buffer. This matches the effective
TextMate activation contract beneath the installed JSX/TSX editor path.

The grammar is paired with `fecsx.tmTheme`, which maps FECSX markup scopes to
the same Visual Studio classification names as the installed TSX editor
(`HTML Element Name`, `HTML Attribute Name`, `html operator`, and
`xml literal - text`). Visual Studio's selected theme therefore controls the
colors; FECSX does not hard-code a palette. Visual Studio's `source.cs` include
does not contribute child lexical scopes inside this custom grammar, so the FECSX
expression seam owns C# string, character, comment, and recursive-brace states.
Those states keep an interpolated-string hole or another nested C# construct from
ending the containing JSX expression and corrupting every following markup scope.
Only the delimiters and literal segments of an interpolated string receive a
`string.*` scope; its balanced interpolation holes deliberately have no string
parent so Roslyn classifications remain visible for `props` and member accesses.
Closing-element rules capture their
names directly, as the donor TSX grammar does, rather than depending on a
begin-capture backreference that Visual Studio can leave in the fallback markup
scope.

The packaging project is intentionally separate. It follows the repository's
existing build-package boundary and prevents NuGet/MSBuild layout concerns from
leaking into the generator implementation. The parser should remain ordinary
C# code in the shared project. Visual Studio APIs must remain confined to the
VSIX project so the editor-neutral service can be tested without launching
Visual Studio.

The build targets should include project `*.fecsx` files as `AdditionalFiles`
and arrange for generated sources to participate in the normal C# compilation.
Generated files belong under `obj` and must never be treated as handwritten
source.

## Diagnostics and debugging

Parser diagnostics must use spans in the original `.fecsx` document. Generated
C# must contain narrowly scoped `#line` directives for embedded expressions and
generated render statements so Roslyn diagnostics and Portable PDB sequence
points resolve to the original file.

Debugger support is part of the first usable slice, not a later polish item.
The generator maps the render expression to the original `.fecsx` path, hides
generated facade and intrinsic-property plumbing, and the integration suite
proves that the existing managed debugger binds, stops, and steps in an
executable FECSX line.

## Visual Studio editor strategy

Visual Studio already has JSX/TSX-aware editing through its TypeScript language
service, including React project support and JSX tag behavior. That service
cannot be reused wholesale because it parses expression containers as
JavaScript or TypeScript rather than C#.

Use staged editor support:

1. Package a `.fecsx` content type, TextMate grammar, and Visual Studio Language
   Configuration in `FrontEndCSharp.Fecsx.VSIX`. Fork the open-source TypeScript React
   grammar's JSX states for tags, attributes, fragments, children, entities,
   and expression delimiters, replacing its embedded TypeScript expression
   rules with C# scopes. Preserve the upstream license, version, and provenance
   beside the derived grammar. This layer is implemented in
   `FrontEndCSharp.Fecsx.VSIX`, pinned to TypeScript-TmLanguage commit `3d2222f`.
2. Port the editor-neutral JSX portions of the open-source TypeScript parser and
   language service into FECSX. Preserve its recovery rules and recognizable
   service boundaries instead of independently recreating similar behavior.
   Replace TypeScript AST and checker calls at explicit seams with FECSX syntax,
   the intrinsic-property schema, and Roslyn symbols. The initial service layer
   now implements recovered classification, closing-tag and linked-tag edits,
   contextual attribute completion, JSX/C# comment behavior, brace matching,
   expression formatting, and bidirectional C# expression projection.
3. Add semantic C# completion, navigation, rename, and quick info inside
   `{ ... }` by projecting those spans into generated or virtual C# documents.
   Positions and edits must map bidirectionally between C# and FECSX.
   Go To Definition on a managed opening or closing tag resolves the generated
   facade type in the owning project, then maps that type to the `Default` or
   named component declaration in the authored `.fecsx` document. Intrinsic tags
   do not claim this command.
4. Host those shared services through a focused Visual Studio extension layer.
   The initial VSIX adapts intrinsic/closing-tag completion, brace matching, and
   linked editing directly to Visual Studio editor APIs while TextMate supplies
   lexical presentation. The generator and editor consume the same parser.

The editor maintains two Roslyn views for semantic completion. The projected C#
document removes the FECSX generator so the active component declaration is not
compiled twice. A separate component catalog is built from the owning project
with the generator enabled, using the active buffer text for its matching
`AdditionalDocument`; this preserves sibling generated facades and their props
metadata. Tag and attribute completion consume that immutable catalog, while
ordinary C# expression completion continues to use the generator-free projected
document. Do not copy generated syntax trees into the projection: the owning
project remains responsible for generator execution, identity, and invalidation.
Generator removal recognizes ordinary analyzer identities and also inspects the
loaded generator behind Roslyn's incremental-generator wrapper when Visual Studio
supplies an opaque project-analyzer reference; other analyzers remain attached.
Managed-tag definition navigation deliberately uses the generator-enabled owning
project instead. Its probe retains only the active file's imports and namespace,
so props declarations are not duplicated, and generated facade identity—not a
filename-only guess—selects the authored declaration.

Visual Studio officially supports TextMate grammars and Language Configuration
for basic custom-language editing, and an LSP extension for semantic features.
An FECSX LSP would represent a new content type, which is the supported use case.
The Visual Studio host must not reference private installed TypeScript
assemblies; reusable implementation should come from pinned, licensed upstream
source and be adapted into repository-owned code.

### TSX donor implementation research

The Visual Studio 2022 17.11 installation used for the initial research exposes
the layers that make up its TSX experience and identifies their open-source
donor implementations:

- `TypeScriptVisualStudio.pkgdef` maps `.tsx` to the TypeScript language service
  and editor, while separately registering the TextMate repository and Language
  Configuration. FECSX needs the same separation between content type, lexical
  presentation, and semantic service.
- `TypeScriptReact.tmLanguage` identifies the upstream
  `Microsoft/TypeScript-TmLanguage` commit from which it was built. Its JSX
  repository has distinct states for elements with and without attributes,
  nested children, embedded expressions, entities, attribute names,
  assignments, quoted values, and illegal attribute text. These state
  boundaries are a strong starting point for FECSX lexical behavior; the
  TypeScript expression state must be replaced with C# classification.
- `language-configuration.json` supplies bracket and quote pairing, surrounding
  pairs including `< >`, word boundaries, indentation rules, and Enter-key
  behavior. It does not account for all JSX editing behavior by itself.
- The installed Apache-licensed TypeScript server implements the semantic JSX
  behaviors through explicit operations whose code should be ported where the
  behavior is language-neutral: `getJsxClosingTagAtPosition`,
  `getLinkedEditingRangeAtPosition`,
  `getJsxClosingTagCompletion`, `tryGetJsxCompletionSymbols`, brace matching,
  JSX-aware comment toggling, and JSX-specific formatting rules.

The donor boundary should be deliberate:

```text
TypeScript TSX implementation       FECSX adaptation
-----------------------------       ----------------
JSX scanner modes                   FECSX scanner modes
JSX parser and recovery             FECSX syntax nodes and diagnostics
JSX tag/fragment tree               same structural model
TypeScript expression parser        Roslyn C# expression parser
TypeScript contextual prop type     HTML schema or managed C# props type
TypeScript symbol lookup            Roslyn symbol lookup
JSX formatting rules                FECSX formatting rules
TS language-service operations      shared FECSX editor services
VS TypeScript editor host           FECSX content type and LSP/VSIX host
```

The initial port should begin with TypeScript's JSX-specific parser functions,
including the equivalents of
`parseJsxElementOrSelfClosingElementOrFragment`, `parseJsxChildren`,
`parseJsxOpeningOrSelfClosingElementOrOpeningFragment`, `parseJsxAttributes`,
`parseJsxAttribute`, `parseJsxExpression`, and `tagNamesAreEquivalent`. These
functions contain mature error recovery for incomplete and mismatched markup,
which is valuable to both incremental generation and live editing. The
expression parser call inside `parseJsxExpression` is the primary substitution
point for Roslyn.

The next port should cover TypeScript's JSX-specific language-service methods.
For example, its attribute completion algorithm gets the contextual props type,
enumerates its properties, filters attributes already present, and chooses an
insertion shape based on the property type. FECSX should retain that algorithm
while obtaining the contextual type from `ButtonProps`, a managed component's
props type, or the canonical intrinsic schema through Roslyn.

Do not mechanically port the complete TypeScript parser or checker. Pin a known
upstream revision, copy only the JSX-specific slices and their relevant tests,
retain required Apache 2.0 or MIT notices, and record intentional divergences
next to the adapted code. This gives FECSX the accumulated TSX editing knowledge
without importing a JavaScript type system it cannot use.

Those operations establish a concrete editor behavior checklist:

- insert the matching closing element or fragment only when the parsed element
  is genuinely unclosed;
- edit matching opening and closing tag names together, including qualified
  component names;
- complete a partially typed closing tag from the corresponding opening tag;
- obtain attribute completion from the tag's contextual property type and
  omit attributes already present;
- distinguish bare boolean attributes from attributes requiring an initializer;
- use `{/* ... */}` when commenting markup children and normal C# comments
  inside embedded C#;
- match braces and format spacing differently inside expression containers;
- retain useful coloring and recovery while a tag or expression is incomplete;
- preserve fragments, self-closing elements, nested elements, entities, and
  malformed-tag diagnostics as separate cases.

Port applicable upstream TSX parser and service tests for each checklist item,
then add Visual Studio integration tests for the FECSX host. Interactive TSX
comparison remains useful for verifying the complete experience, but it is a
supplement to source reuse rather than the primary specification. FECSX should
inherit mature JSX behavior from the donor implementation and diverge only at
the documented C#/Roslyn and managed-props seams.

## Compatibility with UI libraries

FECSX knows about React nodes and managed component resolution, not individual
libraries.

The public library seam is the same member shape generated for an FECSX
component. An uppercase or member-qualified tag `Library.Widget` lowers to:

```csharp
Library.Widget.Create(
    props =>
    {
        props.Name = value;
    },
    children);
```

The receiver may be a generated static facade, a library-provided static type,
or an object exposed through a qualified member. Its `Create` method supplies
the props type to Roslyn and returns a `ReactNode`-compatible value. No Motion,
MobX, or other library name is reserved by the grammar.

- Motion composes through a typed `MotionElement` facade that ultimately calls
  its existing element factory and returns `ReactElement`.
- MobX React supports both whole-component wrappers and render regions. The demo
  keeps ordinary generated component facades and places each store-reading root
  inside `MobXReact.Observe(() => <markup>)`; observable state still does not
  introduce a MobX-specific FECSX grammar rule because wrapped-root lambdas are
  a library-neutral component-body form.
- Other React libraries should integrate through the same public component and
  element-factory contracts.

## First implementation slice

The completed end-to-end surface supports:

- one required `Default` component and zero or more named components per
  `.fecsx` file;
- intrinsic elements and text;
- fragments;
- embedded C# expressions;
- literal and expression attributes;
- typed managed components with object-initialized props;
- a small typed intrinsic catalog sufficient to rewrite the React demo;
- direct `React.CreateElement` lowering;
- original-file diagnostics and `#line` mappings;
- build/package integration and incremental regeneration;
- a Portable PDB and debugger breakpoint regression test.

Spread attributes, broad HTML coverage, specialized Motion conveniences,
refactoring, and a full language server remain outside this slice.

The explicit signature remains the canonical form. A future opt-in shorthand
could use a Razor-like directive to declare the props model and synthesize the
`props` and `children` parameters, but it must lower to the same declaration
contract and must not create a second component-identity rule. An attribute may
eventually expose ordinary `.cs` component methods to tooling; `.fecsx` itself
does not need an attribute to repeat its file-kind semantics.

## JSX text and entity semantics

The parser preserves authored JSX text, quoted attribute values, and their exact
source spans. Semantic cleanup happens only during lowering, following the pinned
TypeScript 5.2.2 React JSX transform:

- single-line child text, including whitespace-only text, remains exact;
- multiline indentation-only text contributes no React child;
- the first meaningful line keeps leading whitespace, the last keeps trailing
  whitespace, middle lines are trimmed, and non-empty lines join with one space;
- semicolon-terminated decimal, lowercase-`x` hexadecimal, and XHTML named
  entities decode in child text and quoted attributes;
- unknown names, missing semicolons, uppercase-`X` numeric forms, and invalid
  code points remain literal rather than being accepted by a general HTML parser;
- quoted attributes decode entities but do not apply child-text whitespace
  cleanup.

Keeping raw syntax text separate from its lowered value lets classification,
diagnostics, linked edits, completion, and expression formatting continue to use
authored coordinates. The real-Chromium FECSX composition gate verifies the final
DOM text so generator-only tests cannot conceal a React-boundary difference.

The Visual Studio delegation choice is also resolved. The thin VSIX host uses the
editor-neutral service plus Roslyn workspace documents: a generator-free projected
C# document for embedded expressions and a generator-enabled component catalog for
FECSX tag and property metadata. Projection buffers and an FECSX LSP remain possible
future hosts, not competing implementations in the current extension.

### Visual Studio verification

Build template-bearing VSIX projects with full Visual Studio MSBuild from a
Developer PowerShell. The template-manifest generator is a Visual Studio tool and
is unavailable to `dotnet build`. Then run the colocated tests without rebuilding
the package. To verify actual registration rather than only archive contents,
install it into Visual Studio's experimental hive; the C# harness builds the
package with the discovered Visual Studio MSBuild, installs it, refreshes the
configuration through `devenv.com`, and verifies both TextMate registry seams:

```powershell
MSBuild.exe Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX.Tests/FrontEndCSharp.Fecsx.VSIX.Tests.csproj /t:Build /p:DeployExtension=false
dotnet test Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX.Tests/FrontEndCSharp.Fecsx.VSIX.Tests.csproj --no-build
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- install-vsix fecsx Exp
```

After changing either installed project template, install both owning VSIX files
and exercise discovery, creation, build, F5 launch, and managed browser execution
through the real New Project dialog:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- install-vsix debugger Exp
dotnet run --no-build --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- install-vsix fecsx Exp
dotnet run --no-build --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
  verify-visual-studio-project-templates --root-suffix Exp --close-existing `
  --accept-project-security-warnings
```

Capture the actual rendered editor window for visual comparison without relying
on an interactive observer:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
  capture-editor-window Demo/FrontEndCSharp.ReactDemo/Components/EnergyPanel.fecsx "$env:TEMP/fecsx-editor.png" `
  --solution FrontEndCSharp.sln
```

The command launches the requested document in the experimental hive and uses
the rendered Visual Studio window itself as the screenshot source. It fails
closed for a missing, minimized, undersized, or empty window, and closes the
exact Visual Studio process it launched after capturing. Pass `--keep-open` only
when an interactive follow-up is useful. The helper never closes an existing
instance that it did not launch. Use `Integration/Fixtures/EnergyPanel.tsx` as
the same-theme TSX baseline.

A screenshot is presentation evidence only; it is not a lexical-classification
assertion. After changing the FECSX grammar, query the installed TextMate tagger
and require the expected scopes for both interpolation regressions:

```powershell
dotnet run --no-build --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
  verify-textmate-scopes Demo/FrontEndCSharp.ReactDemo/Components/EnergyPanel.fecsx "$env:TEMP/fecsx-energy-scopes.png" `
  --close-existing
dotnet run --no-build --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
  verify-textmate-scopes Demo/FrontEndCSharp.ReactDemo/Components/WeatherPanel.fecsx "$env:TEMP/fecsx-weather-scopes.png" `
  --close-existing
```

The verifier reads both `ITMScopeTag` and aggregated `ClassificationTag` spans
from Visual Studio's actual buffer. It requires every affected `className`,
`type`, and `onClick` token to carry
`entity.other.attribute-name.fecsx`. It also rejects any `props` token covered by
a `string.*` scope and requires the interpolation-hole `props` classification set
to equal an ordinary-expression baseline. Pixel colors never satisfy this gate.

Verify the live projected Roslyn document separately from the lexical TextMate
surface. The semantic verifier waits for the solution and project system, opens
`App.fecsx` through DTE, requires `ReactNode` and `OrbitStore` type
classification, and rejects the duplicate/ambiguous `AppProps.Store` diagnostics
CS0229 and CS0119:

```powershell
dotnet run --no-build --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
  verify-semantic-classification Demo/FrontEndCSharp.ReactDemo/Components/App.fecsx `
  FrontEndCSharp.sln "$env:TEMP/fecsx-app-semantic.png" `
  --edit-line 9 --accept-project-security-warnings
```

`--edit-line 9` first waits for the initial semantic pass, clears its trace,
inserts and later undoes a blank line, then requires a fresh project-aware pass.
This covers the post-edit projection path that must not surface CS0229 or CS0119.

The trust switch is appropriate only for a solution and projects already verified
to be inside the repository checkout. Without it, a project-security modal fails
the run instead of being accepted silently.

Verify the semantic completion surface separately. This opens the solution,
captures fresh EXP-hive popup images for a C# namespace (`ReactDemo`), instance
member (`EnergyLevel`), managed component (`TaskCard`), and keyword (`default`),
and functionally commits `ObservableCollection` to prove Roslyn's additional
`using System.Collections.ObjectModel;` edit maps back into FECSX:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
  verify-completion Demo/FrontEndCSharp.ReactDemo/Components/EnergyPanel.fecsx `
  FrontEndCSharp.sln "$env:TEMP/fecsx-completion-captures" `
  --accept-project-security-warnings --close-existing
```

Pass `--tailwind-only` to restrict this physical popup gate to the Tailwind
default and first-character filtering scenarios. This avoids making a focused
Tailwind check depend on Roslyn semantic completion readiness elsewhere in the
document.

Pass `--tailwind-retarget-only` to exercise only the retained-session sequence:
type `p-`, erase it without dismissing completion, type `blo`, and require
`block` plus its `display: block` description.

Pass `--tailwind-advanced-only` to type and physically verify the `hover:`,
`focus:`, `dark:`, and `md:` variants, the grouped `m-` family with purpose-first,
pixel-valued catalog tooltip and real expanded values, adjacent `grow`/`grow-`
entries, property-split `bg-auto`/`bg-clip-`/`bg-amber-` completion, the refreshed `md:block` utility list,
slash modifier, arbitrary bracket value, and custom-property examples. With the
dedicated Tailwind completion fixture, the verifier first builds a project v4
theme and additionally requires its `tablet:` breakpoint, the built-in `dark:`
media condition, and the `drop-shadow-card` utility to appear with their configured
values. The FECSX view controller restarts
only Tailwind sessions that cross `:`, cross `/`, enter `[]`/`()` grammar, or move
within an arbitrary value while completion remains open. This allows the source to
generate state- and caret-specific items and descriptions while leaving ordinary
FECSX/Roslyn completion under Visual Studio's default manager.
For focused Tailwind checks, pass
`Debugger/Tests/Fixtures/TailwindCompletionFixture/TailwindCompletionFixture.csproj`
as the solution argument instead of `FrontEndCSharp.sln`. The one-project fixture
links the real `EnergyPanel.fecsx` and exposes the same compiler-visible activation
properties without loading unrelated repository projects. The advanced verifier
builds this fixture automatically so its project-specific catalog is current.
When Visual Studio shows
a project security warning, the harness clears “Do this for every project in the
current solution” before opening the trusted fixture.

For a bounded navigation-only gate, pass `--navigation-only`. This presses F12
on an authored C# member, a type declared in the same FECSX document, a sibling
component tag, and a named component tag without running the completion popup
scenarios.

The trust switch is intentionally opt-in. Without it, an exact Visual Studio
project-security modal fails the run instead of being accepted silently. The
helper never saves its temporary completion edits and closes only the EXP
process it launched.

Verify solution-wide rename separately. This creates a disposable SDK project,
renames a props type through the real FECSX editor command, and checks the C#
declaration plus two FECSX components. It also rejects any projection identifier
or `.editor.g.cs` file that leaks into authored output:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
  verify-solution-rename "$env:TEMP/fecsx-rename-verification" `
  --close-existing
```

## References

- [React: Writing Markup with JSX](https://react.dev/learn/writing-markup-with-jsx)
- [JSX AST](https://github.com/react/jsx/blob/main/AST.md)
- [Babel JSX transform implementation](https://github.com/babel/babel/blob/main/packages/babel-plugin-transform-react-jsx/src/create-plugin.ts)
- [TypeScript JSX documentation](https://www.typescriptlang.org/docs/handbook/jsx.html)
- [Microsoft TypeScript TextMate grammar](https://github.com/microsoft/TypeScript-TmLanguage)
- [Microsoft TypeScript language service source](https://github.com/microsoft/TypeScript/tree/main/src/services)
- [JavaScript and TypeScript in Visual Studio](https://learn.microsoft.com/en-us/visualstudio/javascript/javascript-in-visual-studio)
- [Adding a Language Server Protocol extension to Visual Studio](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension)
