# Repository Tooling Migration

This document inventories tracked non-C# tooling and records why each category is
retained or migrated. The implementation checklist remains in
[`../TODO.md`](../TODO.md).

## Boundary

C# owns repository tests, process orchestration, generation, validation, packaging,
and maintenance. JavaScript remains product source in the runtime, bundler, and
runtime-override resources. A JavaScript scenario may remain when it directly tests
JavaScript module or runtime semantics, but a C# test or tool must own its process,
timeout, assertions, and cleanup. PowerShell may remain only as a logic-free host
bootstrap where a Windows application cannot be invoked directly from C#.

## Inventory

The inventory below reflects tracked source after the completed repository-tooling
migration.

| Category | Count | Disposition |
| --- | ---: | --- |
| JavaScript runtime scenarios under `Compiler/Compiler.Tests/JsRuntime` | 19 | Retain as direct JavaScript-runtime inputs. The .NET test host owns 18 compiler-runtime behavioral scenarios; `FrontEndCSharp.Compiler.Integration` owns the interpreter benchmark process and result contract. |
| Package-owned runtime-asset scenarios under `Overrides/*/Tests/JsRuntime` | 11 | Keep scenarios beside the companion that owns the asset. A package-neutral .NET test host discovers and executes them through the owner-qualified output contract. |
| Compiler browser integration `.mjs` entry points and helpers | 0 | Migrated to `FrontEndCSharp.Compiler.Integration`; the motion-evidence unit scenarios are ordinary C# tests. |
| Debugger integration `.mjs` entry points | 0 | All orchestration is C#. The retained Edit-and-Continue `.runtime.mjs` file only applies an emitted module inside the product runtime and is not an entry point for repository orchestration. |
| FECSX Visual Studio PowerShell scripts | 0 | Visual Studio orchestration and verification are C# integration commands. |
| Debugger and Visual Studio PowerShell scripts (`.ps1` and `.psm1`) | 0 | Focused C# commands own debugger, browser, Visual Studio, VSIX, trace, and fault orchestration. |
| `Compiler/JsBundler/bundle.cjs` | 1 | Retain as product bundler input; it is compiled into the compiler payload and is not repository orchestration. |
| Runtime and runtime-override `.js` modules | many | Retain as product source or explicit JavaScript environment adaptations. |

## Completed migrations

- Runtime-override manifest generation is the compiler's
  `--generate-runtime-override-manifest` C# command.
- Typed runtime-override provider generation remains the compiler's
  `--generate-runtime-override-providers` C# command; its PowerShell host wrapper
  was removed.
- Compiler payload archive creation is owned by the C#
  `FrontEndCSharp.Build.Tasks create-compiler-payload` command. Both NuGet and VSIX
  packaging call it directly; the shared PowerShell archive script was removed.
- Nested VSIX, project-template, compiler-payload, debugger-registration, and NuGet
  archive validation is owned by the C# `FrontEndCSharp.Debugger.Integration
  verify-vsix` command.
- Installed `FrontEndCSharp.FECS.Internals.Build` package behavior is exercised by the C#
  `verify-build-package` command, including explicit compiler selection and a
  runtime-only incremental rebuild.
- Project-template extraction, token replacement, package restore, build, web-host
  startup, and real-browser execution are owned by the C#
  `verify-project-template` command.
- Combined Browser, React, MobX, and Motion fixture compilation, static hosting,
  Chromium execution, assertions, timeouts, and cleanup are owned by the C#
  `javascript-interop-browser-smoke` command.
- FECSX demo compilation, CDP transport, computed Motion sampling, browser
  assertions, and cleanup are owned by the C# `fecsx-react-demo-browser-smoke`
  command. The former JavaScript motion-verifier tests are C# unit tests.
- Interpreter benchmark process execution, timeout, JSON contract, and invariants
  are owned by the C# `interpreter-performance` command; the remaining `.mjs` file
  is the JavaScript-runtime workload itself.
- Real debug-adapter startup and missing-launch-input failure validation use the C#
  DAP transport and `debugger-adapter-failure` command.
- Edit-and-Continue fixture mutation, Roslyn-host protocol, compiler translation,
  invalid-edit assertions, process bounds, and cleanup are owned by the C#
  `edit-and-continue-smoke` command. Its retained JavaScript runtime input only
  loads and applies the emitted update module.
- Debugger hot-boundary benchmarking, browser ownership, flattened CDP sessions,
  conditional breakpoints, pause counts, result invariants, and cleanup are owned
  by the C# `debugger-performance` command.
- Async adapter launch, breakpoint binding, logical async stacks, merged locals,
  step-out, unhandled exceptions, DAP timeouts, and cleanup are owned by the C#
  `debug-adapter-async-smoke` command.
- Live Edit-and-Continue adapter coordination, torn/active/stale update rejection,
  breakpoint rebinding, committed symbols and locals, and post-update stepping are
  owned by the C# `debug-adapter-edit-and-continue-smoke` command.
- Synchronous DAP behavior, web-host lifetime, owned and external browsers,
  attach-only sessions, foreign CDP pauses, crash cleanup, and authored FECSX
  mapping and stepping are owned by the C# `debug-adapter-smoke` command.
- F5 trace parsing, milestone timing, startup acceptance, module-load counting,
  and JSONL baseline recording are owned by the C# `measure-f5-startup` command.
- Debugger and FECSX VSIX build, uninstall/install, Visual Studio configuration,
  and installed registration verification are owned by the C# `install-vsix`
  command.
- Visual Studio editor-window capture, visible-content detection, bounded process
  ownership, readiness logs, and semantic-classification evidence are owned by
  the C# `capture-editor-window` and `verify-semantic-classification` commands.
- Disposable solution creation, DTE/UI Automation rename invocation,
  cross-document authored-file assertions, screenshot evidence, exact process
  ownership, and validated fixture cleanup are owned by the C#
  `verify-solution-rename` command.
- Roslyn/FECSX completion discovery, popup evidence, completion commit and import
  edits, bounded Undo, definition navigation, UI Automation, and exact Visual
  Studio process ownership are owned by the C# `verify-completion` command.
- Standard Visual Studio F5 composition, debugger-owned browser navigation,
  exact authored breakpoint validation, screenshot/trace evidence, and startup
  project and breakpoint restoration are owned by the C#
  `verify-visual-studio-f5` command. The redundant general-purpose PowerShell
  state machine, COM worker, Pester suite, and fault drivers were removed; their
  adapter/browser/VSIX/editor/trace contracts are covered by the focused C#
  commands documented in
  [`../debugger/DEBUGGING_THE_DEBUGGER.md`](../debugger/DEBUGGING_THE_DEBUGGER.md).

## Migration order

1. Replace the remaining archive and package verification invoked by MSBuild.
2. Replace compiler browser integration entry points with bounded C# harnesses.
3. Replace debugger adapter and Edit-and-Continue JavaScript orchestration.
4. Move Visual Studio and COM automation into C# tools, leaving only proven
   host-required bootstraps.

Every slice removes its superseded entry point, updates all documented commands and
MSBuild callers, and retains equivalent observable assertions before proceeding.
