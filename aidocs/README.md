# Agent documentation map

The documents in this directory are split into a small always-read core and
task-specific topic guides. Start with the core set, then load only the guides
whose contracts or code areas the current task can change. Do not read every topic
guide preemptively; that adds stale or unrelated constraints to the working context.

## Always-read core

- [`../README.md`](../README.md) — product architecture, repository layout, and
  common commands.
- [`../AGENTS.md`](../AGENTS.md) — non-negotiable repository instructions.
- [`GOALS.md`](GOALS.md) — scope, anti-goals, and implementation standards.
- [`WORKFLOW.md`](WORKFLOW.md) — development, regression, and bookkeeping process.
- [`TODO.md`](TODO.md) — agreed unfinished work only.
- [`DISCUSSION.md`](DISCUSSION.md) — unresolved proposals and decisions.
- [`LESSONS_LEARNT.md`](LESSONS_LEARNT.md) — durable mistakes and technical lessons.

## Task-specific guides

| Area | Read when the task touches | Guides |
| --- | --- | --- |
| Compiler and MSIL | IL decoding, opcode support, `msil.bin`, runtime identity, or package compatibility | [`compiler/MSIL_SUPPORT.md`](compiler/MSIL_SUPPORT.md), [`compiler/MSIL_BINARY_FORMAT.md`](compiler/MSIL_BINARY_FORMAT.md) |
| Runtime overrides | Companion discovery, manifests, managed dependencies, JavaScript adaptations, or interop packages | [`compiler/RUNTIME_OVERRIDE_ASSEMBLIES.md`](compiler/RUNTIME_OVERRIDE_ASSEMBLIES.md) |
| Browser HTTP and JSON | `HttpClient`, Fetch policy, streaming responses, `System.Text.Json`, or Newtonsoft.Json | [`features/BROWSER_HTTP.md`](features/BROWSER_HTTP.md), [`features/JSON.md`](features/JSON.md) |
| Validation | DataAnnotations, React form validation, or FluentValidation | [`features/VALIDATION.md`](features/VALIDATION.md) |
| Task behavior | `Task`, await lowering, continuations, scheduling, or cancellation boundaries | [`features/TASK_SUPPORT.md`](features/TASK_SUPPORT.md) |
| UI authoring and interop | FECSX, React markup, typed browser/UI adapters, MobX, Motion, or state-library integration | [`features/FECSX.md`](features/FECSX.md), [`features/MANAGED_UI_INTEROP.md`](features/MANAGED_UI_INTEROP.md) |
| Debugger product | DAP, CDP, symbols, stepping, breakpoints, exceptions, Edit and Continue, or Visual Studio launch integration | [`debugger/ARCHITECTURE.md`](debugger/ARCHITECTURE.md) |
| Debugging the debugger | Debugger diagnostics, the physical Visual Studio/Brave harness, extension mutation, acceptance evidence, or debugger-tool safety | [`debugger/DEBUGGING_THE_DEBUGGER.md`](debugger/DEBUGGING_THE_DEBUGGER.md) plus the architecture guide above |
| Performance | Compiler, interpreter, debugger, or startup profiling and benchmark evidence | [`engineering/PERFORMANCE.md`](engineering/PERFORMANCE.md) |
| Release packaging and publication | Staging, versioning, hashing, uploading, or verifying override NuGet packages and debugger/FECSX VSIX releases | [`engineering/RELEASES.md`](engineering/RELEASES.md) |
| Repository tooling | Test/generator language choices or migration of repository-owned JavaScript and PowerShell automation | [`engineering/REPOSITORY_TOOLING.md`](engineering/REPOSITORY_TOOLING.md) |

Read every guide that applies to a cross-cutting task, but do not infer additional
scope from a guide's API or feature inventory. The scope checkpoint in
[`WORKFLOW.md`](WORKFLOW.md) still controls broad work.

## Keeping the map useful

- Keep cross-cutting policy in the core documents; do not duplicate it in topic
  guides.
- Keep detailed current behavior and subsystem contracts in the relevant topic
  guide, not in `LESSONS_LEARNT.md`.
- Keep unfinished work in `TODO.md`, unresolved choices in `DISCUSSION.md`, and
  completed history in Git.
- Add or move a topic guide only when this map and the `.AI Docs` solution folders
  are updated in the same change.
