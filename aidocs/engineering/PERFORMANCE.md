# Performance

This document records reproducible performance measurements and the decisions justified by them. Measurements are diagnostic baselines for this repository, not general application-throughput claims.

## Compiler phase benchmark

The C# integration harness generates a deterministic browser application with 256
routed React pages. Every page is a concrete `IRoutedPage` implementation with two
interface-dispatched entry points and eight managed computation stages. The entry
point constructs every page, invokes the interface methods, and renders the result
through the public Browser and React APIs. This produces 2,836 selected methods and
286 referenced types while exercising closed-world type flow, dependency selection,
debug metadata, the binary MSIL package, and browser output generation.

The harness builds the fixture once in Release, performs one unmeasured warmup, and
then runs five compiler processes. It uses `--no-bundle` so the measurements describe
the managed compiler and browser-output writer rather than Node.js startup or webpack.
Each process writes inclusive phase elapsed time and process-wide allocation deltas.
Nested phase allocations are therefore useful independently but must not be added
together. The harness rejects a run if graph metrics, phase names, `index.js`, or
`msil.bin` differ between samples.

Run the current benchmark with:

```powershell
dotnet run --project Compiler/Compiler.Tests/Integration/FrontEndCSharp.Compiler.Integration.csproj `
    --configuration Release -- compiler-performance --pages 256 --samples 5 `
    --label current
```

Reports remain under the ignored `Compiler/Compiler.Tests/Performance/reports`
directory beside the compiler performance harness. Generated fixtures, outputs,
profiles, and comparison compiler payloads use the same performance workspace.
On 2026-11-03, .NET 8.0.25 on Windows 10.0.19045 with six logical processors produced
this pre-optimization baseline:

| Phase | Median elapsed | Median allocated |
| --- | ---: | ---: |
| Compiler process | 13,505.9 ms | — |
| Read call graph | 6,650.2 ms | 1,967,445,208 bytes |
| Required methods | 3,532.1 ms | 1,203,383,472 bytes |
| Virtual dispatch discovery | 1,202.9 ms | 649,875,576 bytes |
| Write browser output | 6,619.8 ms | 154,139,648 bytes |

All five samples selected 2,836 methods and 286 referenced types. They produced
`index.js` SHA-256 `fd524709a1c1a9e9a2664ee5828fe7204dad104fb37e5fb3ce01ac68c011c206`
and `msil.bin` SHA-256 `328f2a08ca721cf74d6c5946592364a441da6e00f77afef091e16e0529c75277`.
The phase evidence identified the required-method pass as the first optimization
target: its validation entry point computed and discarded the complete required set,
then the caller immediately repeated the identical analysis to obtain that set.

The baseline is reproducible from instrumentation commit `778ab07`. Removing the
discarded first pass produced these five-sample medians with the same command and
fixture:

| Phase | Before | After | Change | Before allocated | After allocated | Change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Compiler process | 13,505.9 ms | 11,342.3 ms | -16.0% | — | — | — |
| Read call graph | 6,650.2 ms | 4,705.0 ms | -29.3% | 1,967,445,208 | 1,365,746,080 | -30.6% |
| Required methods | 3,532.1 ms | 1,739.1 ms | -50.8% | 1,203,383,472 | 601,689,024 | -50.0% |

Every optimized sample still selected 2,836 methods and 286 referenced types. The
optimized `index.js` and `msil.bin` hashes exactly match the baseline hashes above.
The remaining required-method pass performs the same reachable-receiver validation
before returning the selected set, so error behavior and graph selection retain one
authoritative implementation.

### Immutable BCL intrinsic inventory

A subsequent `dotnet-sampled-thread-time` profile showed `File.Exists` and Windows
file-attribute lookup in 54.4% of sampled stacks. `PathLocator.HasBcl` constructed a
reflection-derived filename and probed the compiler payload for every query across
call-graph discovery and four writer passes. The current payload has no legacy
`JsRuntime/bcl` directory, so all of that work returned false.

The compiler now inventories top-level BCL filenames once per process. A missing or
empty directory returns false without formatting a method signature or touching the
filesystem; a populated immutable compiler payload retains the same lookup behavior.
The benchmark's `--compiler` option ran the pre-change compiler from commit `425a7de`
and the candidate compiler against the same current fixture in adjacent five-sample
runs:

| Measurement | Before | After | Change |
| --- | ---: | ---: | ---: |
| Compiler process | 5,194.7 ms | 4,442.2 ms | -14.5% |
| Read call graph | 3,065.4 ms | 2,686.6 ms | -12.4% |
| Write browser output | 1,854.6 ms | 1,656.7 ms | -10.7% |
| Writer allocation | 155,580,064 bytes | 142,842,880 bytes | -8.2% |

Both sides selected 2,836 methods and 286 referenced types. Both emitted `index.js`
SHA-256 `140cae47e67b37418cce15dbcdc0073e52d43271bf4538f18a0cae0f0df52ed7`
and `msil.bin` SHA-256 `b91fb894d80b6e6f179bb2ab9a2a2feb0145f404c2c784ec9515e5811dbbe8bd`.
In the follow-up CPU profile, file existence and attribute lookup fell below the top
55 inclusive stacks; possible-type analysis became the dominant compiler-owned path.

### Runtime-override method resolution

The next profile showed repeated runtime-override resolution rebuilding the same
canonical reflection signature. `RuntimeOverrideCollection.TryGetEntry` occupied
12.21% of sampled inclusive stacks, including `GetSignatures` at 11.75%, fully
qualified method-name normalization at 10.95%, and `RuntimeMethodInfo.GetParameters`
at 13.45%. These percentages overlap because the methods are on the same stacks.

Each immutable runtime-override collection now caches the resolved entry and matched
canonical signature for an exact `MethodBase`, including negative results. The
process-wide empty collection bypasses the cache so it cannot retain arbitrary
reflection objects. This also lets filename lookup reuse the signature selected by
override resolution instead of repeating normalization.

An adjacent five-sample comparison preserved the pre-change Release compiler before
building the candidate and ran both against the same generated fixture:

| Measurement | Before | After | Change |
| --- | ---: | ---: | ---: |
| Compiler process | 4,585.3 ms | 4,181.2 ms | -8.8% |
| Read call graph | 2,806.2 ms | 2,380.4 ms | -15.2% |
| Read allocation | 1,360,210,720 bytes | 1,151,994,312 bytes | -15.3% |
| Virtual dispatch discovery | 1,442.7 ms | 1,045.6 ms | -27.5% |
| Virtual dispatch allocation | 649,873,448 bytes | 545,608,728 bytes | -16.0% |
| Write browser output | 1,699.2 ms | 1,685.8 ms | -0.8% |
| Writer allocation | 142,920,112 bytes | 132,966,840 bytes | -7.0% |

Both sides selected 2,836 methods and 286 referenced types. Both emitted `index.js`
SHA-256 `fb53fe3588ae36b51665bfaecbdee3bcba923fc59c02892a927b55b1fb978cfb`
and `msil.bin` SHA-256 `4371b862672441a255c7e2b9a537cdff40d4fc8ab90d5e9904762ff58338da59`.
In the follow-up sampled-thread-time profile, `TryGetEntry` fell from 12.21% to
3.93% inclusive and `GetParameters` fell from 13.45% to 8.25%. `GetSignatures` and
both method-name normalization routines fell outside the top 80 inclusive stacks.
The replacement dictionary lookup occupied 7.59% inclusive, confirming that the
cache still has measurable cost but is materially cheaper than repeated reflection
and signature construction.

### ReactDemo application profile

On 2026-11-11, .NET 8.0.25 on Windows 10.0.19045 with six logical processors
measured the real `Demo/FrontEndCSharp.ReactDemo` application. The integration
harness now accepts `--fixture react-demo` and `--bundle true|false`. It builds the
Release compiler and application before starting the compiler-process stopwatch,
performs one unmeasured warmup, writes every sample to a fresh directory, and
requires stable graph metrics, phase names, and `msil.bin`. No-bundle samples also
require byte-identical `index.js`. Bundled samples report `IndexSha256Stable: false`
because webpack assigns different numeric module IDs between otherwise equivalent
runs; the managed payload and graph invariants remain mandatory.

Run the two five-sample series with:

```powershell
dotnet run --project Compiler/Compiler.Tests/Integration/FrontEndCSharp.Compiler.Integration.csproj `
    --configuration Release -- compiler-performance --fixture react-demo `
    --samples 5 --bundle false --label react-demo-no-bundle

dotnet run --no-build --project Compiler/Compiler.Tests/Integration/FrontEndCSharp.Compiler.Integration.csproj `
    --configuration Release -- compiler-performance --fixture react-demo `
    --samples 5 --bundle true --label react-demo-bundled
```

The first command's Release application build took 57.339 seconds with cold
dependencies. After that priming run, the final bundled series recorded an 8.255
second warm application build and a 1.817 second compiler-project build. Those
build times are informational and explicitly outside the compiler-process medians.
The compiler samples selected 745 methods and 269 referenced types in both modes,
and every sample emitted `msil.bin` SHA-256
`3a27e61504f344d36be5a29dda2793f86af3b9fd9fe340752438c6dffe960a4f`.
The no-bundle `index.js` SHA-256 was stable at
`05c2c5d469a121ed917de3d3318a48efa873cca812bf1ccb81eb4ff6fe78988a`.

| Measurement | No bundle median | Bundled median | Median allocated |
| --- | ---: | ---: | ---: |
| Compiler process | 73,277.2 ms | 50,514.0 ms | — |
| Read call graph | 28,699.0 ms | 28,938.6 ms | 22,507,128,648 bytes bundled |
| Decode methods | 3,488.4 ms | 4,072.7 ms | 2,146,113,552 bytes bundled |
| Managed-type demand | 3,732.5 ms | 3,880.2 ms | 3,070,169,496 bytes bundled |
| Reflection demand | 5,860.8 ms | 5,883.6 ms | 4,791,661,936 bytes bundled |
| Runtime-override dispatch | 3,667.8 ms | 3,470.7 ms | 3,070,231,888 bytes bundled |
| Virtual dispatch | 3,494.4 ms | 3,423.2 ms | 3,070,560,680 bytes bundled |
| Write output | 44,397.3 ms | 20,009.2 ms | 121,288,240 bytes bundled |
| Extract override packages | 42,514.5 ms | 6,808.3 ms | 60,475,232 bytes bundled |
| Node.js/webpack bundle | excluded | 10,235.4 ms | 171,888 managed bytes |

The no-bundle samples were 71.313, 72.774, 73.277, 74.059, and 88.808 seconds.
The accepted bundled samples were 49.122, 49.124, 50.514, 50.891, and 51.191
seconds. Bundling is faster here because it extracts packages into a local temporary
staging directory and publishes the compact bundle; no-bundle mode materializes the
complete raw package graph directly under the repository-backed output path.

An optional `dotnet-sampled-thread-time` trace tied method stacks to those phases:

```powershell
dotnet-trace collect --profile dotnet-sampled-thread-time --format Speedscope `
    --output react-demo-no-bundle.nettrace -- `
    dotnet Compiler/Core/bin/Release/net8.0/Compiler.dll --no-bundle `
    --performance-report react-demo-no-bundle-phases.json `
    Demo/FrontEndCSharp.ReactDemo/FrontEndCSharp.ReactDemo.csproj <output> `
    Demo/FrontEndCSharp.ReactDemo/bin/Release/net8.0/FrontEndCSharp.ReactDemo.dll
```

`RuntimeOverrideCollection.ExtractJavaScriptPackages` and
`JavaScriptPackageArchiveExtractor.Extract` occupied 59.42% of sampled inclusive
stacks. File-handle close was 23.24% exclusive, `CreateFile` 19.21%, file-attribute
lookup 12.50%, and `WriteFile` 5.10%. The extracted graph contained 10,060 files in
546 directories totaling 73.02 MB, including 2,843 maps (25.43 MB), 2,006 TypeScript
or declaration files (20.85 MB), and 110 Markdown files (4.07 MB).

The graph side was CPU- and allocation-heavy rather than method-count-heavy.
`PossibleTypeGraphAnalyzer.FindVirtualReceivers` occupied 21.38% inclusive,
`PossibleTypeMethodAnalyzer.Analyze` 20.31%, `FlowState.Clone` 10.89%, and
`Array.Resize` 12.06% exclusive. Other overlapping paths included reflection-demand
discovery at 7.33%, canonical method-name construction at 5.91%, and runtime-override
managed dependency resolution at 4.50%. The 22.51 GB phase allocation is therefore
consistent with repeated state cloning and collection growth, not with the final
745-method graph size.

The bounded optimization candidates, in recommended investigation order, are:

1. Reduce JavaScript package materialization for the normal managed source-map
   profile. Generate a reviewed runtime-file manifest or content-addressed extracted
   cache so webpack does not recreate source maps, declarations, documentation, and
   other resolver-irrelevant files on every compile. The directly addressable budget
   is the 6.8-second bundled extraction phase, with possible secondary benefit in
   webpack. Risks are package `exports` conditions, dynamic imports, license files,
   concurrent cache publication, and degrading the explicitly requested full source
   map. Validate managed and full map modes, package conflict detection, all package
   browser gates, ReactDemo Chromium behavior, and stable managed graph/MSIL output.
2. Replace eager possible-type `FlowState` cloning and repeated array growth with a
   copy-on-write or mutation-local representation, pre-sized from method metadata
   where possible. The target is the 21% inclusive possible-type path and the 22.51
   GB read-phase allocation. Risks are branch/loop aliasing, exception-flow merges,
   by-reference arguments, and generic receiver precision. Validate identical
   selected-method/type counts and output hashes on both performance fixtures, then
   run focused possible-type tests and the complete 704-case NUnit suite.
3. Extend immutable per-method metadata caching only at still-hot seams: reflection
   demand, normalized names, managed dependency resolution, and companion catalog
   reads. Do not duplicate the existing exact-method override lookup cache. Risks are
   constructed-generic substitutions, negative-result invalidation, load-context
   retention, and hiding companion conflicts. Validate catalog/conflict tests,
   generic override tests, exact graph/output invariants, and an adjacent five-sample
   before/after trace.

The standard Debug ReactDemo Chromium acceptance run separately measured 123,246.3
ms for custom compilation and 1,685.1 ms from Chromium launch through the ready
state and settled entrance. The later hover and form assertions were excluded from
that startup stopwatch. Browser startup is therefore not a leading optimization
target. A Release-built ReactDemo artifact could not provide the same browser sample
because it exposed the release-only runtime failure recorded in `DISCUSSION.md`;
that correctness issue does not alter the compiler phase measurements above.

## NUnit test-duration review

On 2026-11-11, the first serialized NUnit run executed 704 discovered cases across
the eight repository test assemblies. The run used .NET 8.0.25 and NUnit 5.0.0 on
Windows 10.0.19045. The code-owned assembly action recorded setup, test, and teardown
elapsed time for every case. Cases below five seconds were excluded from this
review. Git-churn counts below are distinct commits touching the named test and its
direct implementation area among the 100 commits preceding the run; they are a
relative regression signal, not a probability estimate.

An isolated discovery run at the pre-migration commit `e2c2923` found the same
per-assembly counts as NUnit: 391 Compiler, 58 FECSX, 53 FECSX Editor, 34 FECSX
VSIX, 84 Debugger, 47 Fecscn, 3 Tailwind, and 34 runtime-override contract cases.
The total remained 704 before and after migration.

| Case or focused group | Initial elapsed | Churn | Value, duplication, and exhaustive-shape review | Disposition |
| --- | ---: | ---: | --- | --- |
| `BuildTargetsTests.CrossCompanionRuntimeAssetImportsHaveDirectProjectDependencies` | 45.675 s | 100/100 | High-churn cross-companion assets make the ownership gate valuable and unique. Exhaustively checking tracked JavaScript imports is justified, but recursively walking build output and reopening a project for each import was not. | Query Git for tracked candidates, read candidates concurrently, and load each project once. Focused rerun: 3.040 s. |
| `RuntimeOverrideAssemblyCatalogTests.CombinesCompatibleJavaScriptPackageRequirementsAcrossSelectedAdapters` | 27.134 s | 65/100 | Package conflict/promotion semantics are valuable, but reparsing four production companions duplicated the separate companion-contract and catalog tests. Exhaustive production override parsing is unrelated to dictionary merge mechanics. | Extract and directly test the production merge routine, including peer-to-direct promotion and incompatible versions. Focused rerun: 0.052 s. |
| `TailwindCompilerTests.Packaged_payload_builds_without_package_manager_or_network_access`; `Compile_discovers_fecsx_and_cs_and_is_incremental` | 13.582 s; 7.240 s | 2/100 | Offline archive consumption and incremental rebuilds are distinct, valuable contracts. Recompressing the immutable payload inside the first test duplicated package preparation rather than exercising consumption. | Prepare the archive incrementally during the build. Focused rerun: 4.110 s and 4.252 s; final full-run incremental case: 6.091 s. |
| `AsyncIntegrationTests.ContinueWithSupportsEveryCancellationOptionsAndSchedulerCombination`; `TaskWhenAllAndWhenAnyEnumerateNonArrayInputs`; `TaskWhenAllAndWhenAnyAcceptEnumerables`; `AsyncTaskConfigureAwaitOptionsHonorSupportedFlagsAndValidation` | 10.116 s; 5.727 s; 5.192 s; final-only 5.130 s | 20/100 | High-churn task overrides justify the coverage. `ContinueWith` deliberately exercises its overload matrix because each overload has a distinct managed signature; the two `WhenAll`/`WhenAny` cases distinguish arrays from general enumerables; configure-await flags exercise validation and scheduling branches. They are not duplicates. | Retain. One compile-and-run per case is already the narrowest end-to-end boundary; the final `ContinueWith` sample was 10.472 s, below the 15 s ceiling but still a target for compiler-wide startup improvements. |
| `AsyncIntegrationTests.AsyncIteratorBreakRunsFinallyAndDisposesEarly`; `AsyncIteratorEnumeratorCancellationTokenFlowsWithoutLinking`; `AsyncIteratorsYieldValuesInOrder`; `AsyncIteratorExceptionsReachAwaitForeachConsumer` | 7.242 s; 7.081 s; 6.455 s; 6.274 s | 22/100 | High-churn async-iterator lowering and runtime code has four independent state-machine contracts: disposal/finally, cancellation-token flow, ordering, and exception propagation. The shapes are complementary rather than exhaustive permutations. | Retain the four focused vertical slices. Each compiles and executes once and has no repeated setup to remove. |
| `TemporalFormattingIntegrationTests.StandardFormatsAndInterpolationMatchDesktopDotNet`; `InvalidFormatsThrowManagedFormatException` | 9.452 s; 5.759 s | 7/100 | Formatting changes moderately often. Success parity across standard formats and managed exception identity cover different branches; the format set is representative rather than combinatorial. | Retain. Both share comparisons inside one generated program where useful and avoid one process per format. Final standard-format sample: 6.621 s. |
| `EnumFormattingIntegrationTests.StandardFormatsAndInterpolationMatchDesktopDotNet` | final-only 5.084 s | 4/100 | Enum formatting changes infrequently, but named, flags, numeric, interpolation, and invalid-format paths are distinct public behaviors. The set is representative and consolidated into one generated program. | Retain the single compile/run; it does not duplicate temporal formatting despite the shared test method name. |
| `RuntimeOverrideToolingTests.GeneratedCoreLibProvidersMatchTheCompiledTargetRuntime` | 6.692 s | 10/100 | Generator/provider drift is a unique build-time contract in a moderately changing area. Exhaustive provider inventory comparison is the point of the gate. | Retain; it performs one generation/compile comparison and has no duplicated production-companion scan. |
| `ValidationIntegrationTests.DataAnnotationsExecutesCustomValidationAttributeAsManagedCode`; `DataAnnotationsValidatesRequiredProperty`; `DataAnnotationsSupportsPropertyValueAndThrowingApis`; `DataAnnotationsValidatesPracticalBuiltInAttributesAndOptions`; `DataAnnotationsSupportsInheritanceAndIValidatableObject`; `DataAnnotationsRejectsNullMismatchedContextsAndMalformedAttributes` | 6.496 s; 5.816 s; 5.667 s; 5.522 s; 5.108 s; 5.061 s | 5/100 | Churn is low, but the cases protect separate reflection/reachability, API-shape, inheritance, custom-code, built-in-attribute, and error contracts. They do not repeat the same assertion, and the built-in set is representative rather than exhaustive. | Retain. Each case compiles one consolidated scenario; merging them would obscure failures without removing compiler startup work from the suite. |
| `MemoryStreamIntegrationTests.MemoryStreamValidatesBoundsAndSupportsByteArrayAsyncOperations` | 6.373 s | 2/100 | Churn is low, but synchronous bounds and byte-array async overloads share state and provide unique managed-runtime parity. The case is representative, not an exhaustive stream matrix. | Retain its single consolidated compile/run. |
| `TimerIntegrationTests.TimerSupportsOneShotPeriodicChangeStateAndDispose` | 6.229 s | 1/100 | Churn is low. One-shot, periodic, change, state, and disposal form the small public timer state machine and are not duplicated elsewhere. | Retain the single bounded state-machine scenario. |
| `PortablePdbDebugManifestTests.CompilerBundlesTheCompleteModuleGraphIntoThePublicIndex` | 6.081 s | 21/100 | Bundler/debug-output code changes frequently. The complete public module graph, stale-file removal, and manifest publication are a unique integration boundary; exhaustive module inclusion is the asserted mechanic. | Retain one real bundle invocation; narrower unit assertions would not prove publication. |
| `TaskDelayIntegrationTests.TaskDelaySupportsCancellationBeforeAndDuringDelay` | 5.227 s | 1/100 | Churn is low, but pre-cancellation and cancellation during a pending delay take distinct runtime paths. The two-path scenario is small and not duplicated. | Retain its single compile/run. |

The review removed expensive setup and unrelated exhaustive work without dropping a
discovered case or weakening a behavioral assertion. The final full run accounted
for all 704 cases and passed all 704. Its slowest case was the retained `ContinueWith`
matrix at 10.472 seconds; no case reached the 15-second ceiling. A new full run is
required after any subsequent test or infrastructure edit, and routine additions
should remain below 10 seconds.

### Ten-second ceiling follow-up

On 2026-11-11 the repository ratcheted the test ceiling to 10 seconds and retained
five seconds as the slow-test review bar. A focused rerun of the former
`ContinueWithSupportsEveryCancellationOptionsAndSchedulerCombination` case took
15.211 seconds and exhausted its compiler-process bound. Profiling the exact cached
fixture separately took 10.025 seconds: `read-call-graph` consumed 8,324.5 ms and
allocated 4,448,723,640 bytes, including 2,518.6 ms in runtime-override dispatch,
1,701.5 ms in virtual dispatch, and 1,664.1 ms in required-method analysis. The
fixture selected only 61 methods and 49 types; its cost came from forcing many
nearly identical overload paths through the same whole-program passes.

The repository already verifies that every public `Task` and `Task<T>.ContinueWith`
overload has a runtime override, and the adjacent integration scenario covers state,
cancellation, continuation filtering, synchronous execution, and scheduler behavior.
The expensive case now executes the eight distinct full-parameter shapes: Task and
Task<T>, action and function, each with and without state. This removes redundant
short-form overload execution without losing signature or behavioral coverage. The
first cold focused run took 8.619 seconds; three subsequent isolated runs took
5.031, 4.521, and 5.183 seconds. The final serialized Compiler suite passed all 391
cases in 504.625 seconds. Nine cases reached the five-second slow bar, the slowest
case took 6.542 seconds, and the optimized `ContinueWith` case took 3.982 seconds.
Together with the other seven NUnit assemblies, the repository passed all 704 cases
without a 10-second ceiling violation.

### Five-minute suite follow-up

On 2026-11-11 the repository replaced serialized execution with bounded NUnit
fixture parallelism. Four workers execute independent fixtures, methods within a
fixture remain serialized, and `FixtureLifeCycle.InstancePerTestCase` preserves
case isolation. The four async-iterator compiler gates and the temporal standard-
format parity gate run on NUnit's exclusive queue because each is safe in isolation
but can approach or cross the 10-second ceiling under shared CPU contention.

The worker-count experiment used the 391-case Compiler assembly. Two workers passed
in 324 seconds of NUnit execution but could not meet the repository target. Three
workers reached 241 seconds, but one async-iterator case took 10.378 seconds; moving
the five contention-sensitive cases to the exclusive queue passed in 253 seconds.
Four workers with the same exclusive queue passed in 233 and 225 seconds across the
two final runs. The higher per-case slow count is expected resource contention, not
additional test work; the complete-suite interval is the optimization metric while
the unchanged 10-second ceiling remains the safety gate.

Two complete eight-assembly validations retained all 704 discovered cases and took
291.268 seconds (4:51.268) and 287.764 seconds (4:47.764), measured as the sum of the
TRX start-to-finish intervals used by the earlier 564.634-second baseline. All cases
passed. The slowest cases took 8.774 and 9.200 seconds respectively, so neither run
violated the 10-second maximum.

The first full timing pass also exposed a separate race in the timer integration
fixture: a fixed 30 ms sample occasionally observed the third periodic callback in
the following 10 ms window. The fixture now polls for the expected third callback
for at most 100 ms before making the same state, count, disposal, and post-disposal
assertions. The corrected case took 3.846 seconds in the final full run.

Applying the same ceiling to the standalone Chromium gates exposed duplicate work
in `HttpJsonBrowserSmoke`. Its original GET-plus-POST fixture spent 9.037 seconds
and allocated 5.90 GB in call-graph analysis; the compiler alone took 9.824 seconds,
leaving no reliable browser budget. Focused tests already cover GET transport and
the direct `ReadFromJsonAsync` baseline, so the browser gate now retains only its
unique JSON POST echo. That single round trip still exercises Web-default
serialization, HTTP transport, response deserialization, a POCO, a list, and a
string-keyed dictionary. The compiler sample fell to 8.657 seconds, 7.860 seconds
of call-graph analysis, and 4.94 GB allocated. Complete Chromium runs then passed
twice at 9.17 and 9.11 seconds under the 10-second ceiling.

## Visual Studio F5 startup

The VSIX writes a milestone trace to `%TEMP%\FrontEndCSharp\browser-composition.log`. Writes are queued and flushed on a worker so the priority F5 command target and other Visual Studio UI-thread callbacks never synchronously append to disk. The trace includes the standard-F5 command and build boundary, ASP.NET listening output when it passes through DAP, root and child js-debug adapter launches, browser navigation requests, managed runtime context selection/configuration, and detected Browser Link/browser-refresh injection failures involving Brotli responses.

After each physical F5 run, record a cold or warm sample with:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    measure-f5-startup $env:TEMP\FrontEndCSharp\browser-composition.log warm `
    --baseline artifacts\f5-startup.jsonl `
    --acceptance-ms 5000
```

Do not retain the example 5,000 ms threshold without measurement. First collect at least three cold and three warm runs, choose the acceptance value from that evidence, and commit only the resulting summarized baseline. An optional `--vs-output` path classifies ordinary CoreCLR `Loaded ... System/Microsoft*.dll` lines separately; their presence is not counted as blocking time. The command fails when build completion or managed runtime configuration is absent, and fails the run when the measured build-to-configuration interval exceeds a nonzero acceptance threshold.

Historical trace evidence from 2026-10-24 predates the build/listening/navigation markers. In the cleanest recorded warm-shaped launch, root js-debug launch completed at `09:52:49.855Z`, two child launches completed at `09:52:50.959Z` and `09:52:51.992Z`, runtime context selection began at `09:53:01.026Z`, and managed configuration completed at `09:53:01.627Z`. Thus the known unexplained interval is about 9.0 seconds after the final child launch; context selection itself took about 223 ms and configuration about 378 ms. This evidence narrows the investigation but is not a new acceptance baseline because it lacks the newly added build, host-listening, and navigation markers.

A 2026-11-03 physical React-demo trace narrowed the attached-only delay further.
The child js-debug launch request occurred at `14:34:18.453Z`, the `/react`
target was announced 2.472 seconds later, the generated `index.js` was recognized
after another 0.961 seconds, the first mapped source did not arrive for another
3.309 seconds, and managed runtime configuration completed 0.478 seconds later.
The effective js-debug configuration enabled source maps and
`pauseForSourceMap`. The corresponding React bundle map contained 371 sources,
including 276 under `node_modules`; 265 embedded vendor `sourcesContent` entries
advertised another `sourceMappingURL`. The js-debug log recorded 265 failed nested
map reads and 521 `loadedSource` events. This makes recursive vendor-map discovery
the strongest bounded explanation for the 3.309-second map-processing interval;
it does not explain the earlier target-discovery intervals.

The webpack post-processing step now masks nested source-map directives only in
vendor `sourcesContent`. It replaces every non-line-ending character in each
directive with a space, preserving source length, line structure, and all existing
mapping coordinates. Application source content is untouched. A rebuilt React map
contained 360 sources, including 266 vendor sources, and zero nested vendor-map
directives; the C# bundler regression verifies vendor-only masking and coordinate
preservation, and the real-Chromium React demo regression remained green. The
different total source counts came from rebuilding the current checkout and are
not attributed to masking.

The follow-up bounded optimization changed the default from a full-bundle map to a
`managed` profile generated at the webpack module boundary. The profile retains
the mapped `_executeInstruction.js` anchor required by the debugger composition
proxy, `debug-build.js`, and selected companion `runtime-assets`; it excludes
vendor modules, generated managed registration modules, and the rest of the
interpreter core before they contribute mappings or embedded source. The complete
map remains available through `FrontEndCSharpSourceMapMode=full` and
`--source-map-mode full`. On 2026-11-05, the debugger-armed React map fell from
2,418,336 bytes and 370 sources to 51,576 bytes and 9 sources, a 97.9% byte
reduction. The reduced map contained five runtime assets, the debugger anchor,
the build identity, and two small webpack scaffolding sources; it contained zero
`node_modules` sources and no `msil.js` mapping. The real Chromium React regression
remained green, and the standalone debug-adapter smoke bound and hit an authored C#
breakpoint, inspected locals, stepped in/over/out, paused, and projected thrown and
unhandled managed exceptions through the reduced map. A new physical F5 cold/warm
sample is still required to measure the end-to-end effect and decide whether the
remaining attached delay needs another bounded optimization.

The 2026-11-05 physical verifier attempt did not produce such a sample: Visual
Studio launched, but the harness could not expose the debugger-owned browser address
bar and therefore never navigated to `/react`. That pre-navigation harness failure
is neither positive nor negative performance evidence; repeat the measurement only
after the browser-selection surface is observable.

## Interpreter mixed-instruction benchmark

`Compiler/Compiler.Tests/JsRuntime/interpreter-performance.mjs` executes one managed invocation containing a 20,000-iteration loop of constants, local loads and stores, addition, comparison, branching, and return. Each sample executes 220,007 MSIL instructions and verifies the resulting checksum. It records seven samples after warmup in two modes:

- Managed debugger disabled.
- Managed debugger enabled with an idle breakpoint that cannot match the workload.

Run it with an exposed garbage collector so every sample begins after a forced collection:

```powershell
dotnet run --project Compiler/Compiler.Tests/Integration/FrontEndCSharp.Compiler.Integration.csproj -- interpreter-performance
```

The elapsed-time median measures interpreter throughput. The median `heapUsed` growth from immediately before to immediately after a sample is a coarse allocation-pressure signal, not a count of total allocated bytes: V8 may collect during the workload. Behavioral tests must directly enforce allocation-removal invariants where possible.

### Stable invocation-context change

On 2026-10-17, Node.js v22.22.2 on the development Windows machine produced these medians. The before measurement was taken by restoring the prior hot-loop implementation locally; the fix was then reapplied before verification.

| Mode | Before time | After time | Change | Before heap growth | After heap growth | Change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Debugger disabled | 43.596 ms | 22.575 ms | -48.2% | 1,591,552 bytes | 1,011,536 bytes | -36.4% |
| Debugger enabled, idle | 128.878 ms | 114.784 ms | -10.9% | 1,655,064 bytes | 494,920 bytes | -70.1% |

The change constructs the invocation context and managed-frame array once per `ExecuteMSIL` invocation instead of once per instruction. The managed stack-trace string is now exposed through a lazy getter that formats the current managed coordinates only when consumed. The C#-owned `managed-debugger.runtime.mjs` scenario independently verifies that two different instructions observe the same context and frame-array identities while receiving their own current stack coordinates.

Reusing the context also made generic-call metadata lifetime explicit. `invokeMethod` now restores `ctx.genericParams` in a `finally` block so a generic JavaScript override cannot leak its substitutions into a later call in the same managed invocation.

### Direct opcode dispatch

On 2026-10-17, the instruction dispatcher was changed from fourteen sequential category predicates followed by a category-local opcode switch to one immutable opcode-to-handler table. Each instruction module now publishes its final opcode handlers, and the dispatcher validates and combines those tables once on first use. Duplicate and unknown opcodes still fail explicitly.

With Node.js v22.22.2, the clean pre-change debugger-disabled median was 23.361 ms. Five post-change benchmark runs produced run medians of 22.282, 22.793, 22.236, 21.915, and 25.581 ms. Their median is 22.282 ms, a 4.6% reduction. Debugger-enabled idle results varied from 91.523 to 148.462 ms, so this benchmark does not support a debugger-enabled throughput claim for the dispatch change.

### Startup-selected debugger instrumentation

Normal execution now selects a debugger-free interpreter loop once at managed method entry. A configured debug session installs immutable hooks before releasing the managed entry-point rendezvous; debug invocations select the instrumented loop and retain instruction, exception, stepping, and update behavior. Mid-session attachment after managed execution begins is intentionally unsupported.

Five debugger-free runs after this change produced medians of 18.847, 17.411, 18.717, 20.261, and 19.118 ms. Their 18.847 ms median is 15.4% below the preceding direct-dispatch median and 19.3% below the original 23.361 ms dispatch baseline. Debugger-enabled idle results remained noisy, ranging from 87.733 to 132.639 ms, so no debugger-enabled throughput claim is made.
