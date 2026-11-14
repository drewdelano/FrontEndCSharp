# Development Workflow

This document contains repeatable process guidance. Unlike `TODO.md`, these rules remain in force after any individual task is complete.

## Before changing code

1. Read `GOALS.md` and the relevant entries in `LESSONS_LEARNT.md`.
2. Review `TODO.md`, `DISCUSSION.md`, and the preceding test results for agreed
   unfinished work, unresolved decisions, or failing work.
3. Use [`README.md`](README.md) to select the task-specific guides for every
   subsystem or contract the work can change. Read those guides, but do not load
   unrelated feature documents preemptively.
4. Inspect the current working tree and preserve unrelated user changes.
5. Confirm that the file being edited is source rather than generated demo or test output.

Before changing focus, add agreed unfinished implementation work to `TODO.md`.
Put proposals and unresolved design choices in `DISCUSSION.md` until the user agrees
to a direction.

## Scope checkpoint for larger work

Pause for a short scope conversation before turning a request into implementation
work when any of these are true:

- the request is phrased as support for an entire library, framework, subsystem, or
  broad API surface;
- it would introduce a new companion, design document, or work across several
  components; or
- its completion cannot be stated as one bounded, end-to-end observable result.

Agree on these points in plain language before adding or expanding TODO entries:

1. **User outcome:** What application workflow or problem is this work enabling?
2. **Smallest vertical slice:** What is the least behavior that proves that outcome
   from the public API through the real runtime?
3. **Goal and architecture fit:** Which project goal does it serve, and where should
   the implementation boundary be?
4. **Non-goals:** Which adjacent APIs, compatibility surfaces, and generalizations
   are explicitly outside this slice?
5. **Completion gate:** What test or observable result lets the work stop?

After agreement, update the relevant goal or design document and add only the
smallest testable slice to `TODO.md`. Do not turn an API inventory into a roadmap or
let an enabling dependency become an independent compatibility program. Do not infer
that adjacent features are committed work. If implementation discovers a new
feature, abstraction, or subsystem requirement, put it in `DISCUSSION.md` and run a
new scope checkpoint instead of silently expanding the current item.

## Red-green-refactor

1. Add an integration or isolated runtime test that defines the missing behavior.
2. Run it and confirm it fails for the intended reason.
3. Implement the complete behavior without bypassing the MSIL pipeline.
4. Refactor while keeping the new and existing tests green.

Never skip or disable a test, weaken its assertion, or replace architectural work with a JavaScript stub merely to produce a green result.

## Repository tooling language

C# is the default language for repository-owned tests, integration drivers,
generators, validators, regeneration tools, and maintenance automation. Prefer an
NUnit test, a small C# console tool, an MSBuild task, or a source generator. A C#
test may launch Node.js, Chromium, Visual Studio, or another required process and
assert its observable results; the launched system does not determine the harness
language.

Do not add new `.js`, `.mjs`, or `.cjs` test and automation entry points merely
because the product emits or executes JavaScript. JavaScript remains appropriate
for the browser runtime and selected companion implementations. A narrowly scoped
JavaScript test is acceptable only when it directly isolates JavaScript-module
semantics that cannot be exercised with equivalent fidelity through a C# harness;
document that constraint beside the test.

Do not add project logic in PowerShell for source generation, manifest or provider
regeneration, artifact validation, browser/debugger orchestration, or test
assertions. Existing PowerShell automation is migration debt, not a template for
new work. If an external Windows host absolutely requires a bootstrap script, keep
it as a logic-free launcher for a C# executable and record the exception in
`TODO.md`.

The existing Node.js and PowerShell commands below remain required until the
tracked C# migration replaces them. Do not duplicate or extend those patterns.

## Runtime-override test ownership

Keep a fixture or integration harness that proves one runtime-override package's
public behavior under `RuntimeOverrides/<Package>/Tests`. Nest those projects
directly beneath the package's solution folder so the implementation and its gates
stay visible together. A vertical slice that composes several packages belongs to
the most specific public integration package; for example, `System.Net.Http.Json`
owns the HTTP-plus-JSON browser gate even though it depends on the HTTP and
System.Text.Json companions.

Keep package-neutral compiler mechanics in `Compiler.Tests`. Keep identical
manifest, resource, signature, and companion-assembly contract checks in the
shared `RuntimeOverrides.Tests` host rather than creating one NUnit process per
package. Test ownership follows the behavior under test; process consolidation is
an execution optimization, not a reason to place package-specific source in a
central compiler fixture directory.

## Completion checks

Run the complete .NET suite before completing implementation work:

```powershell
dotnet test Compiler/Compiler.Tests/Compiler.Tests.csproj
dotnet test Compiler/Fecsx/FrontEndCSharp.Fecsx.Tests/FrontEndCSharp.Fecsx.Tests.csproj
dotnet test Compiler/Fecsx/FrontEndCSharp.Fecsx.Editor.Tests/FrontEndCSharp.Fecsx.Editor.Tests.csproj
dotnet test Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX.Tests/FrontEndCSharp.Fecsx.VSIX.Tests.csproj
dotnet test Debugger/Tests/Debugger.Tests.csproj
dotnet test Fecscn/FrontEndCSharp.Fecscn.Tests/FrontEndCSharp.Fecscn.Tests.csproj
dotnet test Fecscn/FrontEndCSharp.Tailwind.Tests/FrontEndCSharp.Tailwind.Tests.csproj
dotnet test RuntimeOverrides/RuntimeOverrides.Tests/RuntimeOverrides.Tests.csproj
```

The compiler test project owns the JavaScript runtime scenarios and launches Node.js
through its bounded C# process harness. There is no separate JavaScript test command.

Every .NET test assembly links `Compiler/Tests/NUnitTestInfrastructure.cs`. NUnit
discovers its assembly-level action automatically, records the setup/test/teardown
elapsed time for every case, reports cases at or above five seconds as slow, and
fails any case that reaches 10 seconds. NUnit runs independent fixtures on four
workers and creates one fixture instance per case. Tests within a fixture remain
serialized, and compiler-heavy cases marked `NonParallelizable` run on NUnit's
exclusive queue so CPU contention cannot turn them into ceiling failures.
Repository-owned child-process harnesses retain their own kill bounds; do not rely
on an in-process timeout to terminate an uncooperative external process.

After changing compiler-payload generation, the build integration package, or the
debugger VSIX packaging graph, verify fresh, no-op, and rebuild modes:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    verify-vsix-build-modes .
```

This gate verifies both nested compiler payloads and requires the generated
JavaScript bundler to have the same content in compiler output, the direct debugger
payload, and the build package.

Publishing debugger or FECSX VSIX files is a separate external-state operation.
Follow [`engineering/RELEASES.md`](engineering/RELEASES.md) for version/tag
alignment, Release builds, GitHub authentication, unsigned-package disclosure,
upload, and remote digest verification. Never infer permission to publish merely
from permission to build or validate a package.

After changing the Browser, React, MobX, or Motion adapters, run their combined fixture in an installed Chromium browser:

```powershell
dotnet run --project Compiler/Compiler.Tests/Integration/FrontEndCSharp.Compiler.Integration.csproj -- javascript-interop-browser-smoke "C:\Program Files\Google\Chrome\Application\chrome.exe"
```

After changing the browser `HttpClient` transport or its public overload adapters,
run the bounded representative HTTP fixture in an installed Chromium browser:

```powershell
dotnet run --no-build --project RuntimeOverrides/System.Net.Http/Tests/HttpClientBrowserSmoke/HttpClientBrowserSmoke.csproj -- "C:\Program Files\Google\Chrome\Application\chrome.exe"
dotnet run --no-build --project RuntimeOverrides/System.Net.Http/Tests/HttpStreamingBrowserSmoke/HttpStreamingBrowserSmoke.csproj -- "C:\Program Files\Google\Chrome\Application\chrome.exe"
```

The C# harnesses own compiler, server, Chromium, assertion, and cleanup lifetimes.
The buffered/content gate and response-streaming gate stay separate so each
closed-world fixture remains below the 10-second maximum. Both classify completion
at or above five seconds as slow and kill or reject the entire operation at 10
seconds. Keep detailed HTTP policy and content
cases in focused C# tests instead of expanding either routine browser gate back into
an all-features fixture.

After changing a JSON companion, managed-type reachability metadata, or the JSON
reflection payload, run the affected package's bounded direct-deserializer gate.
Changes that affect `System.Net.Http.Json` or its shared transport/serializer path
also require the primary HTTP JSON vertical slice:

```powershell
dotnet run --no-build --project RuntimeOverrides/System.Text.Json/Tests/SystemTextJsonBrowserSmoke/SystemTextJsonBrowserSmoke.csproj -- "C:\Program Files\Google\Chrome\Application\chrome.exe"
dotnet run --no-build --project RuntimeOverrides/Newtonsoft.Json/Tests/NewtonsoftJsonBrowserSmoke/NewtonsoftJsonBrowserSmoke.csproj -- "C:\Program Files\Google\Chrome\Application\chrome.exe"
dotnet run --no-build --project RuntimeOverrides/System.Net.Http.Json/Tests/HttpJsonBrowserSmoke/HttpJsonBrowserSmoke.csproj -- "C:\Program Files\Google\Chrome\Application\chrome.exe"
```

The independent direct fixtures prove that each serializer entry point executes
without relying on the other package's references or success marker. Keep detailed
option and edge-case parity in fast focused C# tests; do not turn either routine
browser gate back into an all-features fixture.

After changing FECSX generation, intrinsic properties, or the React demo, run
the FECSX composition fixture as well:

```powershell
dotnet test Compiler/Fecsx/FrontEndCSharp.Fecsx.Tests/FrontEndCSharp.Fecsx.Tests.csproj
dotnet test Compiler/Fecsx/FrontEndCSharp.Fecsx.Editor.Tests/FrontEndCSharp.Fecsx.Editor.Tests.csproj
MSBuild.exe Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX.Tests/FrontEndCSharp.Fecsx.VSIX.Tests.csproj /t:Build /p:DeployExtension=false
dotnet test Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX.Tests/FrontEndCSharp.Fecsx.VSIX.Tests.csproj --no-build
dotnet run --project Compiler/Compiler.Tests/Integration/FrontEndCSharp.Compiler.Integration.csproj -- fecsx-react-demo-browser-smoke "C:\Program Files\Google\Chrome\Application\chrome.exe"
```

Run the `MSBuild.exe` command from a Visual Studio Developer PowerShell. VSIX
project-template manifests require Visual Studio's full-framework template tools;
`dotnet build` cannot generate them.

The FECSX browser regression records actual computed Motion values before rendering,
during the entrance transition, after settling, and during/after task-card hover.
It reports `prefers-reduced-motion` and the separate CSS orbit animation settings;
disabled decorative CSS animation is not a Motion failure. Its evidence-parser
regressions run as part of `Compiler.Tests` through the bounded C# Node.js harness.
When the matching compiler and demo binaries were already built, `--no-build` avoids
rebuilding shared assemblies. It still recompiles the browser fixture and replaces
its debug manifest: run both forms sequentially with live F5 scenarios.

Run Visual Studio and repository commands from the current checkout root and use
repository-relative arguments where a command supports them. A mapped drive is
supported but not required. Avoid invoking Visual Studio through the checkout's
UNC backing path because Visual Studio can assign a different project identity;
internal path-identity code still recognizes the UNC alias Windows may return for
an existing mapped-drive file.

After changing FECSX Visual Studio registration, assets, or adapters, also
install into and verify the experimental hive:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- install-vsix fecsx Exp
```

Use the dedicated installer command for every Visual Studio hive so uninstall,
install, configuration refresh, and installed-payload verification remain one
bounded operation. Always pass the intended root suffix explicitly.

After changing the simple or FECSX project template, build with Visual Studio
MSBuild, install both owning packages, and run the physical New Project gate:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- install-vsix debugger Exp
dotnet run --no-build --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- install-vsix fecsx Exp
dotnet run --no-build --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    verify-visual-studio-project-templates --root-suffix Exp --close-existing `
    --accept-project-security-warnings
```

The gate searches the actual dialog, creates both multi-project solutions, rejects
repository-relative dependencies, builds and launches through Visual Studio, and
then verifies each managed browser marker. Keep smoke project names short because
Visual Studio and the .NET SDK still encounter 260-character paths in generated
intermediate filenames.

For editor presentation changes, capture the document from a loaded solution so
Roslyn semantic classification has project context. A screenshot is not proof of
TextMate classification: grammar changes must also run `verify-textmate-scopes`
against both `EnergyPanel.fecsx` and `WeatherPanel.fecsx` and assert the actual
`ITMScopeTag` and aggregated `ClassificationTag` spans. The helper closes the EXP
instance it launches; `-CloseExisting` also clears stale EXP instances first.
It polls the code surface until visible editor content is rendered, so a short
initial render delay is sufficient even while a large solution is loading.

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    capture-editor-window Demo/FrontEndCSharp.ReactDemo/Components/EnergyPanel.fecsx "$env:TEMP/fecsx-editor.png" `
    --solution FrontEndCSharp.sln --render-delay 2 --close-existing

dotnet run --no-build --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    verify-textmate-scopes Demo/FrontEndCSharp.ReactDemo/Components/EnergyPanel.fecsx `
    "$env:TEMP/fecsx-energy-scopes.png" --close-existing
dotnet run --no-build --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    verify-textmate-scopes Demo/FrontEndCSharp.ReactDemo/Components/WeatherPanel.fecsx `
    "$env:TEMP/fecsx-weather-scopes.png" --close-existing
```

After changing FECSX completion or source-edit projection, run the real
completion surface in a loaded EXP-hive solution. The verifier captures and
checks a namespace, instance member, managed component, and C# keyword, then
commits an unimported Roslyn type and verifies both its replacement and added
`using`. Its focused `--tailwind-advanced-only` mode verifies the `hover:`,
`focus:`, and `dark:` variants, the grouped `m-` family with its purpose-first,
catalog-derived pixel tooltip and expanded real values, adjacent `grow`/`grow-`
entries, property-split background families such as `bg-amber-`, commits the `md:` media variant, and
requires the refreshed `md:block` source set before exercising the other dynamic
Tailwind grammar states. Against the dedicated fixture it first builds the v4 theme and
physically requires the project-defined `tablet:`, built-in `dark:` with its
`prefers-color-scheme: dark` media condition, and `drop-shadow-card` completions.
`--accept-project-security-warnings`
remains available for diagnosing an
unexpected project-security prompt, but ordinary repository work must launch from
the same local checkout path used by the build.

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    verify-completion Demo/FrontEndCSharp.ReactDemo/Components/EnergyPanel.fecsx `
    FrontEndCSharp.sln "$env:TEMP/fecsx-completion-captures" `
    --accept-project-security-warnings --close-existing
```

After changing FECSX symbol rename or projection edit mapping, run the
solution-wide rename verifier. It creates a disposable solution, invokes the
installed extension's real Rename dialog, and proves that a C# declaration and
two FECSX uses change without writing projection scaffolding to disk:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    verify-solution-rename "$env:TEMP/fecsx-rename-verification" `
    --close-existing
```

After changing FECSX source mappings or debugger behavior, prove that the
adapter binds and steps in the authored `.fecsx` document:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- debug-adapter-smoke "C:\Program Files\Google\Chrome\Application\chrome.exe" --fecsx
```

Also verify that the patch has no whitespace errors and that generated output has not been added as source. All existing tests must remain green.

## Bookkeeping

- Add concrete unfinished work to `TODO.md` before changing focus.
- Remove completed TODO entries; rely on Git history rather than keeping a completed checklist indefinitely.
- Record durable mistakes, discoveries, and surprising behavior in `LESSONS_LEARNT.md`.
- Update `GOALS.md` when an architectural or scope decision changes.
- Keep the task-routing table in `README.md` and the `.AI Docs` solution folders in
  sync when adding, moving, or renaming a topic guide.
- Report every failing test by name when handing work back. Explicitly state when none fail.
