# FrontEndCSharp

FrontEndCSharp compiles pre-built .NET assemblies to JavaScript by reading and interpreting MSIL. The project aims to preserve C# and CLR semantics while providing explicit interoperability with the browser and JavaScript runtime.

## Architecture

The compiler operates on compiled assemblies rather than parsing C# source:

1. Read IL bytecode and metadata with `System.Reflection.Metadata`.
2. Decode the instructions into the compiler's intermediate representation.
3. Discover and recursively include referenced methods and types.
4. Emit interpreted type registrations, method registrations, method metadata, and method bodies into the versioned `msil.bin` package.
5. Bundle and minify the entry point's complete module graph into `index.js` with webpack while retaining `msil.bin` as a binary asset.
6. Decode the package to the runtime's existing instruction-block representation and execute it through the JavaScript MSIL runtime.

All emitted .NET types and methods live under `globalThis.DotNet`. Runtime values retain their .NET type information, for example `{ t: 'System.Int32', v: 42 }`, and method references include parameter signatures to preserve overload identity.

The runtime has three source categories:

- `Compiler/JsRuntime/msil.js` and `Compiler/JsRuntime/jit-engine/` contain the MSIL interpreter; the same directory owns universal execution bridges and debugger support, but no optional BCL or framework implementation.
- Generated BCL modules are caches produced from actual BCL MSIL and selected through dependency analysis.
- `Overrides/` contains typed `*.fecs.dll` companion projects for the small set of methods that require a genuine JavaScript-environment adaptation or cannot be supplied from MSIL. Browser-target projects select these companions through ordinary project or package dependencies; the compiler does not carry a global built-in override set.

Every emitted JavaScript method-override module uses a fixed-length `method.<sha256>.js` filename, where the lowercase SHA-256 digest is computed from the exact canonical method signature. Interpreted methods and types do not have individual JavaScript modules. Readable CLR identities remain in binary registrations, debugger metadata, and diagnostics rather than filesystem paths. Each `*.fecs.dll` embeds an `Overrides/manifest.json` that maps override filenames back to their readable signatures. The compiler validates the typed provider, manifest, digest, registration, uniqueness, missing resources, and unassigned resources before using an override. Companion-owned auxiliary JavaScript retains simple package-local filenames, but unbundled output isolates the transitive static-import closure of used overrides beneath `runtime-assets/<companion assembly>/`; bundled output consumes the same selected graph without publishing loose modules. Unused optional capabilities such as reflection are absent even when their companion assembly is available.

For a normal bundled build, the compiler emits `index.entry.js`, copied runtime sources, runtime-override modules, and the binary package into a temporary staging directory. Its packaged webpack runner produces the minified bundle there, remapping source-map entries to the stable requested-output namespace so debugger discovery does not depend on the disposable staging path. The compiler then publishes only `index.js`, `index.js.map`, and `msil.bin` to the requested output directory and removes obsolete compiler-owned output from older builds. Unrelated application files in that directory remain untouched. Initial page execution loads only the bundle and `msil.bin`; the loader appends the package's SHA-256 hash as a query parameter so one cache-busted entry URL selects one coherent runtime graph without accumulating renamed binary files. Edit and Continue reads changed registrations and bodies from the candidate binary package. The compiler's `--no-bundle` switch is reserved for tests and diagnostics; it emits the readable runtime graph directly to the requested directory, while interpreted registrations still exist only in `msil.bin`.

Bundled builds use the `managed` source-map profile by default. It maps the stable
`_executeInstruction.js` debugger anchor, the build-identity module when present,
and selected companion `runtime-assets`; it omits `node_modules`, generated managed
registration modules, and the rest of the compiler/interpreter implementation.
Managed C# source, locals, and runtime coordinates come from `msil.bin`, Portable
PDBs, and the debug manifest rather than JavaScript source maps. Compiler/runtime
development can opt into the complete JavaScript map with
`<FrontEndCSharpSourceMapMode>full</FrontEndCSharpSourceMapMode>` or the compiler
option `--source-map-mode full`. The selected mode participates in the incremental
browser-build fingerprint.

Normal MSBuild integration stores a content fingerprint under `obj/FrontEndCSharp` after successful staged publication. The fingerprint covers application and dependency assemblies, PDBs and dependency manifests, selected companion assemblies and their embedded resources, the complete compiler/runtime/bundler/build-task payload, and the effective compiler configuration. An unchanged build with all three published outputs present skips both compiler analysis and webpack; a changed input, changed configuration, missing output, or debugger build sentinel regenerates the complete coherent output set. The state candidate is committed only after compiler publication succeeds. This is whole-browser-build incrementality; it deliberately does not persist partially built call graphs or binary-package fragments.

Interpreted type and method registrations are stored once in `msil.bin`. The package encodes registration metadata, numeric CLI opcodes, typed operands, and one application-wide lossless UTF-16 string table using canonical WTF-8 bytes. The runtime validates it, populates `DotNet.Types` and `DotNet.Methods`, and invokes the managed entry point. Runtime identities use namespace-qualified managed names; compilation fails when different assemblies would contribute the same type or method key. See [`aidocs/compiler/MSIL_BINARY_FORMAT.md`](aidocs/compiler/MSIL_BINARY_FORMAT.md) for the wire format and compatibility rules.

Generated demo and test output is disposable. Do not edit output copies of the runtime or generated BCL modules.

## Repository layout

- `Compiler/` — the compiler workspace: `Core/Read/` contains assembly and metadata loading, IL decoding, and call-graph discovery; `Core/PossibleType/` contains possible-type flow analysis, required-method selection, and dispatch resolution; the remaining `Core/` folders contain linking and code generation. `Compiler.Tests/` contains the compiler tests; `Fecsx/` contains the JSX-shaped C# authoring implementation, editor VSIX, FECSX React project template, and tests; `RuntimeOverrides/` contains shared override infrastructure; and `JsRuntime/`, `JsBundler/`, and `Packaging/` contain their named supporting components.
- `Debugger/` — the shared `netstandard2.0` debug-manifest and DAP protocol assemblies, standalone adapter, Edit and Continue host, Visual Studio 2022 VSIX with the simple browser project template, tracing support, and debugger-specific tests.
- `Overrides/` — `FrontEndCSharp.Overrides.*` companion assemblies, managed adapter assemblies, package-owned tests, and the focused override solution.
  Each override family has a `FrontEndCSharp.Overrides.<Target>/` folder. Package-owned fixtures and browser gates live under that family's `Tests/` directory; the shared companion packaging contract host is owned by the React family under `FrontEndCSharp.Overrides.Interop.JS.React/Tests/`. Browser and JavaScript-library adapters use paired managed API and implementation projects with the same `FrontEndCSharp.Overrides.*` prefix, while their public C# namespaces remain `Interop.JS.*`.
- `Demo/` — the shared `FrontEndCSharp.Demo` web host plus peer `FrontEndCSharp.SimpleDemo` and `FrontEndCSharp.ReactDemo` managed applications. The host serves their generated output from separate `wwwroot/js` subfolders.
- `Fecscn/` — the pinned Base UI/Nova shadcn-style registry, self-contained
  `fecscn init`/`add` tool, application-owned FECSX/C# component sources, Tailwind
  CSS v4 build package, and their C#, offline-build, and Chromium fixtures. Its
  upstream JavaScript boundaries live in the matching `Interop.JS.Shadcn.BaseUI`,
  `Interop.JS.Shadcn.Cn`, `Interop.JS.Shadcn.Recharts`, and independent
  `Interop.JS.LucideIcons` adapter families.
- `aidocs/` — current work, architectural boundaries, and accumulated lessons.

## AI development documents

The durable planning and decision-making documents for assisted development live in
[`aidocs/`](aidocs/). Start with the
[`agent documentation map`](aidocs/README.md), which identifies the small core set
and routes each task to only the relevant topic guides.

- [`TODO.md`](aidocs/TODO.md) — prioritized checklist of remaining work.
- [`DISCUSSION.md`](aidocs/DISCUSSION.md) — unresolved proposals and design choices
  that require agreement before becoming TODO items.
- [`GOALS.md`](aidocs/GOALS.md) — project goals, scope, and anti-goals.
- [`WORKFLOW.md`](aidocs/WORKFLOW.md) — repeatable development and regression process.
- [`LESSONS_LEARNT.md`](aidocs/LESSONS_LEARNT.md) — mistakes and discoveries that should inform future work.

Task-specific material is grouped under `aidocs/compiler`, `aidocs/features`,
`aidocs/debugger`, and `aidocs/engineering`. Agents should use the map instead of
loading every feature document for unrelated work.

Repository-specific development rules are in [`AGENTS.md`](AGENTS.md). Read that
file, the always-read core, and the routed topic guides before making changes.

Repository builds require the .NET 8 SDK, Node.js 22 or later, and npm. The debugger integration tests use Node's built-in `WebSocket` and `AbortSignal.timeout` APIs. Packaged compiler consumers require Node.js because output generation runs the packaged webpack tool; they do not need to install webpack or npm dependencies separately.

Compiler logging defaults to `normal`, which reports the beginning of each major
compilation phase. Pass `--log-level verbose` to the compiler to include per-method
call-graph progress. MSBuild consumers can select the same output with
`<FrontEndCSharpLogLevel>verbose</FrontEndCSharpLogLevel>`.

## Common commands

```powershell
dotnet build Compiler/Core/Compiler.csproj
dotnet test Overrides/Overrides.sln
dotnet build Demo/FrontEndCSharp.SimpleDemo/FrontEndCSharp.SimpleDemo.csproj
dotnet run --project Compiler/Core/Compiler.csproj -- Demo/FrontEndCSharp.SimpleDemo/FrontEndCSharp.SimpleDemo.csproj
dotnet test Compiler/Compiler.Tests/Compiler.Tests.csproj
dotnet test Debugger/Tests/Debugger.Tests.csproj
dotnet build Debugger/Adapter/FrontEndCSharp.DebugAdapter.csproj
dotnet pack Compiler/Packaging/FrontEndCSharp.FECS.Internals.Build/FrontEndCSharp.FECS.Internals.Build.csproj -c Debug
dotnet build Debugger/VisualStudio/FrontEndCSharp.VisualStudio.csproj -c Debug
dotnet test Fecscn/FrontEndCSharp.Fecscn.Tests/FrontEndCSharp.Fecscn.Tests.csproj
dotnet test Fecscn/FrontEndCSharp.Tailwind.Tests/FrontEndCSharp.Tailwind.Tests.csproj
dotnet run --project Fecscn/Tests/FecscnInitBuildFixture/FecscnInitBuildFixture.csproj
dotnet run --project Compiler/Compiler.Tests/Integration/FrontEndCSharp.Compiler.Integration.csproj -- fecscn-browser-smoke "C:\Program Files\Google\Chrome\Application\chrome.exe"
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- debug-adapter-smoke "C:\Program Files\Google\Chrome\Application\chrome.exe"
```

