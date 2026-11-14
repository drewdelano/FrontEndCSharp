# Visual Studio Debugger for the Browser Runtime

This document records the debugger research, implemented architecture, validation status, and durable lessons from building it. The objective is source-level debugging in Visual Studio 2022 for C# code executing in the browser through FrontEndCSharp. Users should set breakpoints, step, inspect managed call stacks and variables, and stop on managed exceptions through Visual Studio's existing debugger UI without using the browser's JavaScript developer tools or a custom debugger UI.

## Recommendation

Treat Portable PDB metadata as the source of truth, use the Chrome DevTools Protocol (CDP) to suspend the JavaScript VM at managed instruction boundaries, and expose managed debugger semantics to Visual Studio through the Visual Studio Debug Adapter Host. Do not attempt to make the generated JavaScript itself appear to Visual Studio as C#.

The implementation should have four separable layers:

1. A compiler-side symbol reader that validates a Portable PDB against its assembly and emits a compact debug manifest.
2. A small interpreter debugger hook that decides whether the current method and runtime instruction coordinate is a managed stop and executes a JavaScript `debugger` statement when it is.
3. A CDP client that owns browser launch, true VM suspension and resumption, paused call-frame inspection, and remote-object lifetime.
4. A debugger controller and standalone C# Debug Adapter Protocol process, packaged with a VSIX, which translate between Visual Studio's built-in Debug Adapter Host, managed debugger concepts, and CDP.

The first foundational experiment is proving that a CDP pause at the `ExecuteMSIL` instruction boundary preserves every nested interpreter call frame and permits safe inspection of each frame's `opts`, arguments, locals, evaluation stack, method, and IL offset. A successful experiment avoids refactoring all managed calls into explicit resumable frames solely for debugging.

Visual Studio should own all debugger presentation: source documents, breakpoint glyphs, execution markers, Call Stack, Locals, Autos, Watch, Output, and Exception Settings. FrontEndCSharp should not build replacements for those surfaces.

Edit and Continue is a product requirement and an architectural constraint from the start, even where its delivery follows the baseline debugger. Build, module, method, source-map, breakpoint, stack-frame, and variable identities must all admit explicit code generations; runtime method replacement and its matching debug metadata must be publishable atomically. Every debugger design decision must preserve a credible path to applying supported edits without reloading the page or losing managed application state. Do not choose a shortcut that assumes code, Portable-PDB mappings, breakpoint bindings, or callable method bodies are immutable for the lifetime of a browser session. The later Edit and Continue section defines the supported-edit boundary, transaction model, failure behavior, and Visual Studio command integration in detail.

## Non-negotiable product constraints

- Ordinary ASP.NET/.NET debugging, Visual Studio's native JavaScript/TypeScript browser debugging, and FrontEndCSharp debugging must form one seamless Visual Studio debugging experience. F5 or the green Debug button on the normal `https`, IIS Express, or other web launch profile must start the web host under Microsoft's .NET debugger, preserve the native JavaScript debugger for the browser, and also attach the FrontEndCSharp engine to the browser runtime. The developer uses the same Visual Studio breakpoint, Call Stack, Locals, stepping, Continue, and Stop surfaces regardless of which engine is currently stopped. Breakpoints in server-side C#, native JavaScript/TypeScript, and browser-executed FrontEndCSharp code must all bind and stop during that session; installing the VSIX must not replace, bypass, or disable either native debugger.
- The normal interactive experience must use Visual Studio's built-in debugger UI. Developers should not need a separate debugger UI or a browser DevTools workflow for routine debugging.
- Starting the combined debugging experience should require only selecting the web project and pressing F5 or the green Debug button, just like ordinary ASP.NET debugging.
- Interactive sessions must open a visible Chrome or Brave window. Headless Chromium is appropriate only when explicitly requested, such as for automated tests.
- Chromium/CDP is the suspension substrate: when Visual Studio reports a breakpoint or step stop, JavaScript execution must actually be paused until the user continues or steps.
- FrontEndCSharp's CDP connection must coexist with Visual Studio's native JavaScript debugger. It must not require exclusive ownership of the browser or page target, consume native JavaScript breakpoint events, or overwrite native JavaScript pause and exception policy. Browser launch, target attachment, pause ownership, and resume coordination must be designed for both engines to remain functional.
- Source locations must come from compiler/debug metadata such as Portable PDBs and the emitted debug manifest. Do not add a public per-instruction `ilOffset` property to runtime instruction objects merely to support debugging; keep debugger-only IL offsets and source mappings out-of-band unless no viable alternative exists.
- Use supported public Visual Studio extension points. Do not depend on private CPS contracts that can break extension loading or vary between Visual Studio installations.
- Edit and Continue is an architectural requirement, so method identity, source maps, active frames, and code-version transitions must remain compatible with future implementation.
- A debug session must cleanly own and release the browser, adapter, transport, and web-host processes that it starts.
- Match Visual Studio's ordinary C# debugger presentation and implement only requested managed behavior. Do not invent convenience rows, pseudo-variables, or other debugger affordances merely because the JavaScript adapter or DAP can provide them. A projected managed Locals scope contains only its PDB-defined arguments, locals, and fields; in particular, discard JavaScript-adapter variables marked with Visual Studio's `returnValue` attribute instead of exposing synthetic `<method> returned` rows.

## Implemented foundation

The compiler now emits the module MVID and MethodDef token for each interpreted method. The runtime exposes an opt-in controller as `globalThis.__FrontEndCSharpDebugger`, accepts complete replacement breakpoint sets keyed by module, method token, block index, and instruction index, consumes safe-boundary pause requests, and executes `debugger;` before the matching instruction. It deliberately does not copy an IL offset onto every emitted instruction. A compiler-side Portable PDB reader validates separate and embedded symbols and emits a build-identified manifest beside the input assembly, outside the browser-served output. That manifest maps Portable-PDB IL offsets and sequence points to the existing runtime coordinates and includes document checksums, parameters, local scopes, state-machine metadata, and module/PDB identity. Disabled execution takes only the guarded branch in `ExecuteMSIL`.

The initial CDP suspension spike proved that a `debugger;` boundary retains nested live `ExecuteMSIL` frames and permits inspection followed by exact resumption in both Chrome and Brave. That disposable harness was removed after equivalent breakpoint, stack, inspection, stepping, and lifecycle behavior gained maintained adapter and Visual Studio coverage.

`FrontEndCSharp.DebugAdapter` is the Visual Studio debugger engine. It uses Microsoft's `Microsoft.VisualStudio.Shared.VSCodeDebugProtocol` package, loads the compiler's debug manifest, validates the requested source checksum, binds a requested line to an executable sequence point, and translates that point to a module/token/runtime-coordinate location. Because the inline runtime hook accepts coordinates before the corresponding generated method module executes, manifest-resolved breakpoints can be verified immediately; late script-load rebinding is unnecessary for this design. It launches an isolated visible Chromium instance for interactive sessions, serves the compiler's real generated output, captures every live `ExecuteMSIL` frame through CDP, and maps each frame back to its C# method, source location, arguments, and in-scope Portable-PDB locals. Automated launches set `browserHeadless` to `true`; the default is deliberately visible. Frame and variable handles are monotonic and discarded on resume so a stale request cannot alias a later suspension. The adapter supplies all visible manifest sequence-point coordinates to a one-shot runtime step plan: step-in accepts the next visible point at any managed depth, step-over requires the same or a shallower depth, and step-out requires a shallower depth. Visual Studio can independently enable thrown and unhandled managed-exception filters. The runtime assigns each managed exception one weakly held identity and captures immutable origin frames before filter execution or unwind; thrown and unhandled stops each occur at most once, and an unhandled stop retains the origin source stack after its live JavaScript frames are gone. Native JavaScript failures are not reported as managed exceptions. The C# `FrontEndCSharp.Debugger.Integration debug-adapter-smoke` command compiles the Portable PDB fixture and exercises DAP breakpoint binding, nested stacks, scopes and variables, step over/out/in, safe-boundary pause, caught thrown and escaping unhandled exceptions, continue, and termination without Visual Studio.

The packaged manifest-backed adapter was validated in a Visual Studio 2022 Community 17.11.5 experimental instance with Debug Adapter Host 17.11.10613.1. The installed VSIX's **Debug with FrontEndCSharp** command built a real SDK-style project, established its Roslyn baseline, invoked the packaged compiler from the short system-drive cache, launched an isolated Chrome target, and entered Visual Studio's ordinary debugging mode. A source breakpoint bound at the requested C# line; F11, F10, and Shift+F11 navigated nested managed calls; Call Stack, Autos/Locals, source execution markers, and Output were populated through the built-in debugger UI; and Continue produced `exited`, `terminated`, and `disconnect` cleanly. A deliberately broken disposable project was also launched through the command to exercise the standard Build output plus the command's Output-pane and message-box failure diagnostics.

The packaged adapter presents parameters and active lexical locals together in the single Visual Studio `Locals` scope; it must not add an `Arguments` pseudo-variable or a second expandable scope. Roslyn display-class locals are identified from compiler metadata rather than filtered by their generated names: the storage container is hidden, while each captured source local is projected through its recorded field path and remains editable. In the composed native-JavaScript path, the underlying adapter may append its own Step Out return-value variable to an evaluated object. The proxy removes only variables carrying Visual Studio's explicit `presentationHint.vsAttributes: ["returnValue"]` marker; it does not guess from display names. Wrapped reference values retain a suspension-scoped object identity instead of being flattened to a display string. Their recursively captured field graph is exposed through DAP `variablesReference` values, reuses references for cycles, and is discarded when execution resumes. Runtime field storage keys are translated through packaged field metadata: source names are displayed while assignment paths retain the metadata-identity key, and hidden fields with the same name are declaring-type-qualified. Compiler-generated auto-property backing fields are shown by both their source property name and raw backing-field name until property-getter evaluation exists. Capture is bounded to 1,024 objects per frame so inspecting a pathological graph cannot indefinitely block suspension.

The standalone adapter and composed native-JavaScript path advertise DAP hover evaluation and resolve an exact source-level argument or local name against the selected managed frame. Hovered reference values retain their expandable `variablesReference`. General C# expression evaluation in Watch, Immediate, or REPL contexts remains unsupported.

The VSIX registers the adapter language as C#, registers thrown and unhandled filters with a **FrontEndCSharp Managed Exceptions** category, and maps `exceptionInfo` identifiers of the form `FrontEndCSharp/<managed-type>` back to that category. With the default unhandled setting, the experimental-instance fixture ignored a caught `InvalidOperationException`, stopped on the final unhandled throw, displayed Visual Studio's Exception Helper at the exact source statement, requested the managed exception details, and showed the retained origin stack. The Debug Adapter Host log contained no missing-language-mapping warning after the C# language GUID registration was added.

The former checked-in `DebugAdapterHost.Launch` JSON was a machine-local development spike. It was removed once maintained smoke tests and the composed Visual Studio F5 path covered the same adapter behavior without fixed checkout and browser paths.

For attach-oriented testing, launch configuration may replace `browserPath` with `browserWebSocketUrl`. Only a loopback `ws://` browser endpoint is accepted. In this mode the adapter owns the target it creates but not the browser process: disconnect clears all runtime debugger state, resumes a managed suspension, detaches the exact flattened CDP session, and leaves the browser alive. The full DAP smoke path and lower-level paused-detach path have both passed against the installed Chrome 154 and Brave/Chromium 155 builds.

The compiler writes the manifest build identity into `debug-build.js`, and the adapter's bootstrap refuses to start user code unless it matches the selected manifest. The adapter attaches to the exact page it creates and enables Runtime and Debugger before navigation. Execution-control commands are serialized; navigation clears suspension-scoped handles, while the bootstrap reinstalls runtime breakpoints and exception policy and revalidates build identity. Reload/rebinding and clean detach pass in both installed Chromium families. An unexpected browser or CDP transport exit invalidates handles, reports a stderr diagnostic, and terminates the DAP session; the external-browser smoke path kills Chromium during a managed stop to cover this cleanup.

The adapter also has a strict attach-only mode for a browser and page owned by another launcher. It requires a loopback browser WebSocket endpoint, exact page target ID, expected loopback application URL, and matching FrontEndCSharp build identity. It neither creates nor navigates a page and does not resume or terminate the browser during detach. Runtime configuration is atomic whether `managed-debugger.js` is already loaded or is about to load, so an existing target can accept the manifest identity, breakpoint set, exception policy, and Edit-and-Continue baseline without a reload. The attach-only smoke navigates the page before starting the adapter and still hits the managed entry-point breakpoint.

Every CDP pause is classified before it becomes a DAP stop. FrontEndCSharp requires the expected runtime build identity, a non-null managed `currentStop` marker, and at least one inspectable managed frame. A pause owned by another CDP client is logged and otherwise ignored: the adapter emits no false managed stop, does not resume the target, and rejects FrontEndCSharp Continue/step control while it lacks a managed suspension. The dual-client attach-only smoke pauses through an independent CDP session, proves the FrontEndCSharp adapter leaves it suspended, and resumes it through the owning client.

Breakpoint publication is a serialized, session-wide configuration transaction rather than a one-time launch action. Visual Studio can issue concurrent `setBreakpoints` requests for unrelated documents and can repeat them after `configurationDone`. The adapter therefore stores breakpoint state per source, resolves the complete enabled set, and republishes that complete set in request order. Before `managed-debugger.js` loads, publication replaces the injected pending configuration; after the runtime exists, it calls the live controller directly. Updating adapter-side state alone is insufficient because an already navigated page would continue using stale coordinates.

Visual Studio and the Portable PDB can name the same local source file through different Windows aliases. Mapped-drive checkouts are supported: in the validated development layout, Visual Studio sent a mapped path such as `R:\FrontEnd.Program\Program.cs`, while Windows identified that file through the mapped drive's UNC backing path. Lexical `Path.GetFullPath` comparison therefore left an otherwise valid breakpoint unowned in both Chrome and Brave. Source ownership now compares the ordinary normalized paths first and, on Windows, resolves existing files through `GetFinalPathNameByHandle` before deciding that they differ. This also handles extended-length `\\?\` names at the filesystem boundary. UNC normalization is an internal consequence of resolving a mapped drive, not a product requirement to launch Visual Studio solutions directly from arbitrary UNC paths. Path aliasing must never bypass the independent active-build identity and Portable-PDB document-checksum checks.

Visual Studio can retain the same JavaScript adapter launcher instance across multiple F5 sessions, and it does not reliably flush a toolbar browser change to the per-hive `browsers.xml` before the next JavaScript root launch. The proxy must therefore query the live `IBrowserDebugTargetSelectionService2` selection for every root launch; the per-hive file is only a fallback. Otherwise switching from Chrome to Brave can open Brave through the web-project launch while leaving the JavaScript debugger attached to a separately launched hidden Chrome target. Breakpoint composition then works in Chrome while the visible Brave page appears unable to bind. The compatibility controller temporarily selects Chrome to obtain Visual Studio's supported launch path; once the root proxy consumes the Brave executable override, it must immediately restore the matching Brave toolbar selection so later F5 sessions do not silently launch Chrome. Child-session launch JSON must remain untouched because it identifies the target created by that root launch.

Visual Studio republishes every enabled breakpoint when a child JavaScript session starts, including stale breakpoints whose source directories no longer exist. Manifest discovery must treat an inaccessible or missing source directory as an unowned source and forward that breakpoint unchanged to Microsoft's JavaScript adapter. Filesystem discovery failures must never escape the DAP transform: doing so terminates the child adapter while it is processing its initial requests and Visual Studio reports only the misleading top-level error "Failed to launch debug adapter. The operation was canceled." The bounded browser-composition trace records root/child launcher boundaries so a pre-launch failure can be separated from a breakpoint-transform failure without logging launch payloads.

`Page.navigate` returning does not prove that CDP evaluation is targeting the new application document. The adapter enables the Page domain and waits for the root `Page.frameNavigated` commit before evaluating bootstrap or runtime state. Without that barrier, `Runtime.evaluate` can still run in the old `about:blank` execution context, which previously produced a visible browser whose application page never completed loading.

### Current end-to-end validation

The installed `0.1.16` VSIX was exercised through the real Visual Studio 2022 experimental instance rather than only through the DAP smoke harness. With the project now named `FrontEndCSharp.Demo` selected as the startup project and the normal `https` target visible on the toolbar, F5 launched a visible isolated Chrome window at the rendered demo application. An enabled source breakpoint in what is now `Demo/FrontEndCSharp.SimpleDemo/Program.cs` was present in Visual Studio's Breakpoints window, published after configuration, and hit before the matching managed instruction executed. Visual Studio showed the breakpoint and Current Statement glyphs together, exposed the managed application assembly and logical managed thread, and changed the standard debug target to **Continue**. Stopping debugging closed the owned browser and web-host session cleanly while leaving Visual Studio usable.

The automated C# `debug-adapter-smoke --web-host` path deliberately sends its source breakpoint after `configurationDone`, verifies that the rendered application reaches that breakpoint, resumes, and then proves that completion of the managed entry point does not end the browser-hosted debug session. That regression passes in both Chrome and Brave. The standard synchronous, async-stack, and Edit-and-Continue smoke paths also pass against the current adapter; the web-host case must remain separate because its process lifetime and navigation ordering differ from a one-shot managed program.

Visual Studio may title this stopped custom-engine state **(Debugging)** rather than **(Break)**. The reliable evidence of suspension is the **Continue** command, disabled **Break All**, a Current Statement marker, and suspension-scoped stack/thread/variable state. Window-title text is not a debugger-state contract.

Experimental and normal Visual Studio hives do not provide a reliable shared breakpoint set. Before diagnosing an adapter failure, verify that the intended breakpoint is enabled in the same hive and solution instance used to start debugging. During the final validation, the experimental hive initially contained only unrelated compiler-test breakpoints; once the simple-demo breakpoint was added there, the installed adapter stopped correctly.

## Repository findings

### Useful foundations already exist

- The compiler already depends on `System.Reflection.Metadata`, which is the preferred API for reading Portable PDBs.
- `ILInstruction` retains the original IL offset while decoding.
- Runtime locals are explicit mutable cells and arguments are explicit arrays, so their values are inspectable once frames remain reachable.
- Wrapped values retain .NET type identity, which gives the variable renderer better information than a JavaScript debugger would have.
- Exception dispatch is already modeled in the interpreter rather than delegated entirely to JavaScript.
- The runtime goals already require instruction-level cooperative scheduling and preserve the possibility of WebWorker execution.

### Blocking constraints in the current runtime

- `ExecuteMSIL` owns a synchronous `while (true)` loop.
- A managed call invokes another generated JavaScript function directly, so managed frames exist primarily as nested JavaScript calls.
- Returning, throwing, and delegate invocation all assume a synchronous JavaScript call result.
- The writer drops individual instruction offsets from emitted data. Only block-start offsets remain.
- Source-path remapping and Source Link acquisition are not implemented. Local matching source, embedded source, build-identity validation, immediate manifest-backed binding, and generation rebinding are implemented.
- A continuation after an `await` starts a new physical JavaScript stack. The runtime therefore snapshots logical managed callers at await registration, and the adapter appends those immutable frames without changing physical step depth.

While a synchronous loop is running, page-side UI commands and WebSocket handlers cannot be processed. Throwing a special "pause" exception would unwind the JavaScript calls that currently hold caller continuations. A CDP `debugger` pause does neither: V8 suspends the JavaScript thread while retaining its live call frames, and the out-of-process adapter can inspect those frames and later issue `Debugger.resume`. The debugger must not depend on page-side messaging while the VM is paused.

## Why source maps and browser DevTools are not the managed debugger

Source maps relate locations in generated JavaScript to source locations. In this compiler, a C# method becomes an array of IL instruction data interpreted by shared JavaScript code. The JavaScript instruction performing an `add`, branch, or call is the same instruction-handler source for every C# method. A source map therefore cannot represent the dynamic relation between a particular IL offset and a C# sequence point.

CDP is useful as the suspension and object-access substrate, but it does not supply managed semantics. The adapter must recognize nested `ExecuteMSIL` call frames, read their method and runtime instruction coordinates from `opts`, map those coordinates through compiler-emitted Portable-PDB debug data, and present only the resulting C# frames and values to Visual Studio. CDP therefore remains an implementation detail rather than the user-facing debugger. The initial debugger is intentionally Chromium-specific; Chrome and Brave compatibility must both be tested, and other browser engines would require another suspension backend.

## Symbol and source model

### Supported symbol format

Start with matching Portable PDBs only. Both separate and embedded Portable PDBs should eventually be accepted through `PEReader.TryOpenAssociatedPortablePdb` and `PEReader.ReadEmbeddedPortablePdbDebugDirectoryData`. A missing, unsupported, or mismatched PDB must produce an explicit diagnostic rather than guessed source information.

The compiler should key debug data by module identity and MethodDef token, not by formatted method name. Formatted names remain presentation data; metadata tokens provide the stable join between assembly metadata and the PDB's `MethodDebugInformation` row.

For each included user method, emit:

- module identity and MethodDef token;
- parameter names and indices from assembly metadata;
- non-hidden sequence points with IL offset, document, and source range;
- local names, slot indices, attributes, and lexical IL ranges from `LocalScope` and `LocalVariable`;
- state-machine kickoff/`MoveNext` relationships;
- the information needed later for hoisted locals and async stepping;
- source document names, language, checksum algorithm, and checksum.

Hidden sequence points use the reserved `0xFEEFEE` line and are never valid breakpoint or step destinations.

### Source acquisition and validation

The adapter and Visual Studio run on the development machine, so ordinary project source should be reported using the exact local path from the Portable PDB. Visual Studio can then open the existing `.cs` document directly. Before reporting a location, the adapter must verify that the local source checksum matches the PDB document record.

For remote or unavailable source, use this fallback policy:

1. Prefer embedded source when the Portable PDB contains it.
2. Add Source Link retrieval later, with checksum verification before display.
3. Only as an explicit packaging option, copy source into a separate debug-source area after checksum validation.

Do not silently display a source file whose checksum differs from the PDB. Machine-local paths belong in the adapter-to-Visual-Studio response and must not be published to browser clients as URLs.

Generated BCL code is not an initial source-debugging target. The first debugger should bind breakpoints only in user modules for which matching symbols and source are available. Runtime and BCL frames can be marked external and collapsed by the UI.

## Chromium suspension model

The primary design adds a cheap check immediately before `executeInstruction(opts)`. When the current method plus block/instruction index matches a managed breakpoint or step plan, or a pause request or exception policy requires a stop, the hook records the managed stop reason and executes `debugger;`. With CDP's Debugger domain enabled, V8 then emits `Debugger.paused` and retains the complete nested JavaScript call stack.

The adapter reconstructs the synchronous managed call stack from the live `ExecuteMSIL` frames and inspects `opts` through `Debugger.evaluateOnCallFrame` and `Runtime.getProperties`. CDP call-frame IDs and remote-object IDs are valid only for that suspension and must be discarded, with object groups released, when execution resumes. Runtime and override frames are implementation details and are not shown as C# frames.

Continue and managed stepping use `Debugger.resume`; CDP's JavaScript `stepInto`, `stepOver`, and `stepOut` commands must not define C# stepping because they would step through interpreter implementation code. The managed controller resumes normally and causes the next qualifying instruction boundary to execute `debugger;` again.

An arbitrary user Pause request requires a safe-boundary bounce. The adapter first uses `Debugger.pause`, sets a `pauseRequested` flag while V8 is paused, then resumes. The interpreter hook consumes that flag and immediately pauses again before the next IL instruction. Only the second pause is reported to Visual Studio. This prevents Visual Studio from observing partially executed opcode state.

The browser must be launched in a dedicated temporary profile with remote debugging enabled, never against the user's normal browser profile. The adapter must discover and attach to the intended page or worker target, survive navigation and reload where supported, and close the isolated profile cleanly. Opening browser DevTools against the same target is initially unsupported because a second debugger client can interfere with ownership of pause and resume.

### Performance evaluation: a conditional CDP breakpoint

The primary hook performs a small managed-stop check before every instruction. Measure that cost in representative release workloads with the debugger attached, both with no enabled breakpoints and with typical breakpoint sets. If it makes execution unacceptably slow, evaluate moving source-breakpoint matching into V8:

1. Use `Debugger.setBreakpoint` or `Debugger.setBreakpointByUrl` to place one JavaScript breakpoint on the stable `executeInstruction(opts)` call site inside `ExecuteMSIL`.
2. Supply a generated, side-effect-free CDP breakpoint condition that tests the in-scope method identity, `opts.currentBlockIndex`, and `opts.instructionIndex` against the currently resolved managed breakpoint locations.
3. Remove and recreate that CDP breakpoint whenever Visual Studio replaces the enabled breakpoint set.
4. On `Debugger.paused`, use `hitBreakpoints` plus the live frame state to identify the managed source breakpoint and report the corresponding DAP stop.

The generated condition could have this conceptual shape:

```javascript
(methodInfo.debugMethodId === "app:0x06000012"
    && opts.currentBlockIndex === 2
    && (opts.instructionIndex === 0 || opts.instructionIndex === 3))
|| (methodInfo.debugMethodId === "app:0x06000019"
    && opts.currentBlockIndex === 1
    && opts.instructionIndex === 2)
```

CDP conditions are ordinary JavaScript expressions evaluated in the breakpoint location's lexical scope; CDP has no native concept of a managed method or IL offset. The emitted runtime data therefore exposes stable module/method identity while the interpreter's existing block and instruction indices identify the current instruction. The debug manifest must provide a stable mapping between those coordinates and Portable-PDB IL offsets; no per-instruction offset field is required in the runtime payload. The call-site script must also have a stable discoverable URL or script identity across the supported development build pipeline.

The C# `FrontEndCSharp.Debugger.Integration debugger-performance` command makes this comparison reproducible. It measures a loop with no debugger hook, the same loop with the inactive inline guard, the inactive guard after attaching CDP's Debugger domain, one ordinary inline breakpoint, one conditional CDP breakpoint on the shared instruction call site, and 100 instruction stops. It verifies the computation checksum and exact pause count so a fast result caused by a breakpoint that did not bind cannot pass. This is deliberately a hot-boundary microbenchmark, not an application-throughput claim: its trivial instruction body magnifies the cost of the guard, while real instruction handlers do more work.

The decision is to retain the inline runtime guard for configured debug execution and not install a conditional CDP breakpoint on the shared `executeInstruction(opts)` site. Normal execution uses a separate debugger-free interpreter loop. Debugger configuration installs immutable runtime hooks before the managed entry-point rendezvous is released, and each managed invocation selects its loop once at method entry. Attaching after managed entry-point execution has begun is a non-goal. In isolated sequential runs over 2,000,000 baseline boundaries, 5,000 breakpoint boundaries, and 100 step boundaries, Chrome 154.0.8037.98 measured 2.1 ms without the hook, 10.9 ms with the inactive hook, 11.5 ms with an idle CDP debugger attached, 3.7 ms for the inline one-stop case, 1,658.9 ms for the equivalent conditional CDP breakpoint, and 171.1 ms for 100 stops. Brave/Chromium 155.0.8059.40 measured 2.5 ms, 13.9 ms, 15.0 ms, 3.5 ms, 1,739.3 ms, and 143.1 ms respectively. The conditional breakpoint was approximately 448 times slower than the inline path in Chrome and 497 times slower in Brave while producing the same one pause. Absolute timings vary with JIT and host load, but the several-orders-of-magnitude result is decisive and both supported Chromium families agree.

The earlier conditional-CDP design remains documented above because it explains the alternative and the required coordinate mapping, but it is not a latent automatic fallback. Reconsider it only if a future V8 implementation removes the hot-site instrumentation cost and the cross-browser benchmark demonstrates a clear improvement. Step plans, safe-boundary pause, and managed exception policy continue to use the runtime controller.

A CDP condition would be internal implementation machinery, not support for user-authored conditional C# breakpoints. Do not advertise DAP conditional-breakpoint capability until there is a real C# expression parser, binder, and evaluator with defined side-effect behavior.

## Session lifecycle and invariants

### Attach before user code

The adapter must gain debugger control before any managed startup code can execute. Browser launch should create or select the intended target, enable target discovery and auto-attachment, and hold new page or worker targets with `waitForDebuggerOnStart` where supported. The adapter then enables the Debugger and Runtime domains, installs or resolves breakpoints, completes the DAP configuration sequence, and only then uses `Runtime.runIfWaitingForDebugger` to release execution. This is required for breakpoints in module initializers and application startup.

Use flattened CDP sessions and route every command and event by session ID. A page, worker, iframe, service worker, or unrelated browser tab must never be mistaken for the managed execution target. The first release should support exactly one selected page or worker execution target and one logical managed thread; encountering multiple eligible execution targets should produce a diagnostic instead of silently choosing one.

### Identity, reload, and stale handles

Every debug session needs an immutable baseline build identity that joins the loaded assembly, matching Portable PDB, emitted debug manifest, and browser runtime payload. Include at least the module MVID, Portable PDB identity, and a manifest or payload hash. Refuse source binding when these disagree; browser caching must never cause breakpoints to bind against stale IL or symbols.

Edit and Continue extends that baseline with a strictly ordered generation chain. Each accepted update must name the baseline, expected prior generation, new generation, and content hash. The adapter and runtime must reject skipped, duplicated, out-of-order, or cross-build updates. A source document, method body, sequence-point map, breakpoint binding, stack frame, or variable handle is meaningful only with its generation; a module token alone is insufficient once code can change during the session.

Treat navigation, reload, worker replacement, and a new `Debugger.scriptParsed` generation as invalidating all CDP script IDs, call-frame IDs, remote-object IDs, and installed JavaScript breakpoint IDs. Increment an adapter-side generation, discard old handles, rediscover the stable interpreter call site, reinstall internal CDP breakpoints, and rebind managed breakpoints. Breakpoints that cannot bind before their module or script loads should be returned to Visual Studio as unverified and later updated with a DAP breakpoint event when binding succeeds.

DAP frame IDs and variable references must also encode or be checked against the current suspension generation. Requests using a frame or variable reference from a previous stop must fail predictably rather than inspecting a different object that reused the same numeric ID.

### Stop ownership and event ordering

The adapter is the single owner of pause and resume. Model the session explicitly as launching, running, pausing-to-boundary, stopped, resuming, terminating, or terminated. Correlate each reported managed stop with one CDP pause and ignore stale `paused` or `resumed` events from an earlier generation. If a breakpoint, step target, pause request, and exception coincide, apply a documented priority and report one stable DAP stop reason while preserving all relevant diagnostic data.

Do not acknowledge a DAP continue or step as complete until CDP accepts the resume command. Emit DAP continued and stopped events in protocol order, and serialize commands that mutate breakpoints or execution state. Locals and stack requests are valid only in the stopped state.

### Disconnect, termination, and failure

Define launch and attach ownership separately. Disconnecting from a launched session should clear internal breakpoints and stop flags, resume if paused, terminate the launched target and browser process, and delete its temporary profile. Disconnecting from a future attach session must not kill a browser the adapter does not own. If cleanup cannot complete, report the retained profile path rather than deleting an uncertain target recursively.

A browser exit, target crash, target destruction, CDP transport loss, adapter failure, or malformed runtime debug state must end the session with a useful diagnostic and a DAP terminated event. No failure path may leave the debuggee permanently paused. Adapter cancellation and Visual Studio shutdown need dedicated integration tests.

### Security boundary

A CDP endpoint can evaluate arbitrary JavaScript and read page state, so treat it as a code-execution capability. Prefer a private debugging pipe when supported and reliable. If TCP is required, bind only to loopback, use an ephemeral port, never publish it to the page, and accept only the browser process and targets created for that session. Always use a fresh non-default profile without the user's cookies, credentials, history, or extensions.

Do not interpolate source paths, user breakpoint text, object display strings, or other untrusted values into CDP JavaScript expressions. Internal conditional-breakpoint predicates must be generated exclusively from validated module/method identities and runtime instruction coordinates, or use correctly serialized literal data. Protocol logs should redact values and URLs that may contain application secrets.

### Compatibility and diagnostics

CDP tip-of-tree changes over time. Use the smallest stable command subset, query the connected browser version and available protocol domains, and maintain an explicit tested Chrome/Brave version matrix. Unsupported commands or target types should fail with actionable diagnostics rather than falling back silently.

Keep an optional bounded protocol trace containing DAP/CDP message names, sequence IDs, target/session IDs, state transitions, and timings without application values. This is important for diagnosing event-ordering and version-specific failures without corrupting DAP standard output or leaking debuggee data.

### Debugger overhead

Normal non-debug execution does not import the managed debugger or perform an instruction-boundary debugger check. Debugger configuration installs the instrumented loop before managed entry-point execution. Record separate benchmarks for no-debug output, debug-enabled output without an attached adapter, an attached idle adapter, typical breakpoints, and stepping.

## Breakpoints and stepping

The controller checks for stops before executing an instruction associated with a visible sequence point.

### Breakpoint binding

- Resolve a requested document and line to a visible sequence point.
- Prefer a sequence point whose source range contains the requested line; otherwise move to the next executable line in that document and return the actual bound location.
- Store the resolved module/method/IL offset and its manifest-mapped block/instruction coordinate, not only the source line.
- Suppress the same stop location exactly once after resume so `continue` does not immediately re-hit it without executing.

### Source stepping

Stepping is defined in terms of the next distinct visible sequence point and explicit frame depth:

- Step in: stop at the next distinct visible sequence point in any eligible user frame.
- Step over: stop at the next distinct visible sequence point at the starting depth or a shallower depth.
- Step out: stop at the next visible sequence point shallower than the starting depth.

Hidden sequence points and external frames do not become stops. The controller must still allow breakpoints to win while a step plan is active. Loops, multiple sequence points on one source line, calls on the same line, exceptions, and method return all need dedicated tests; line-number comparison alone is insufficient.

## Call stacks, locals, and values

Synchronous call stacks come from the live nested `ExecuteMSIL` call frames reported by CDP, never `Error.stack`. Each interpreter call frame exposes its managed method identity and current or last visible sequence point through `opts`. Parent frames suspended at calls should continue to show the call-site source location. Async logical causality remains separate because a continuation after `await` no longer has its physical caller on the JavaScript stack.

The initial variable scopes should be:

- Arguments, including `this` when applicable;
- Locals active at the frame's current IL offset;
- Exception, when stopped for a managed exception.

Local names come from Portable PDB local scopes, while values come from the frame's slot cells. A local is visible only when the current IL offset is within its lexical scope. Complex wrapped values should be expanded lazily and cycle-safely. Debugger object references must expire on resume so stale UI handles cannot mutate or inspect a later suspended state.

Expression evaluation and arbitrary property getters are deliberately deferred. Calling user code merely to display a value can change program state. The first formatter should inspect wrapped primitives, strings, arrays, and fields without invoking managed methods.

## Managed exceptions

Exception stopping belongs inside managed exception dispatch. At minimum, support:

- thrown: before the first managed handler changes state;
- unhandled: after determining that no managed frame can handle the exception, but before discarding the useful stack snapshot.

Do not implement this with CDP `Debugger.setPauseOnExceptions`: the browser cannot distinguish wrapped managed exceptions from interpreter failures, library JavaScript exceptions, and exceptions that are part of normal runtime control flow. Put the hook in managed exception dispatch and tag each propagation with a stable exception identity so the same throw is not reported again as it crosses each nested `ExecuteMSIL` boundary.

An unhandled decision may require executing managed exception filters and unwinding several interpreter calls. By the time the decision is known, the original JavaScript frames may no longer exist. Capture the managed exception-origin stack before the first unwind and retain it in runtime-owned state until the exception is caught or declared unhandled. That snapshot cannot consist only of CDP call-frame or remote-object IDs because those become invalid on resume; it must retain safe runtime references or immutable debugger values with an explicit lifetime. Release it as soon as disposition is known.

Later work can distinguish user-unhandled exceptions. A resumed thrown stop must continue the exact pending search/unwind rather than throw a second exception. JavaScript interpreter failures must remain distinguishable from wrapped managed exceptions and should be reported as debugger/runtime failures, not as C# exceptions.

## Async behavior

Portable PDB `StateMachineMethod` and state-machine hoisted-local-scope records are emitted into the debug manifest. At a stop in `MoveNext`, the adapter projects the frame to its kickoff C# method, obtains kickoff parameters and hoisted locals from the state-machine instance fields, and applies their Portable-PDB lifetime ranges. Generated awaiter and spill fields remain hidden.

There are two separate async goals:

1. Source mapping: a physical `MoveNext` frame should display the kickoff method's C# source sequence points and meaningful state-machine locals.
2. Logical causality: after an `await`, the debugger should be able to show the async caller chain even though the physical frame was released.

Await registration captures immutable managed-frame snapshots before the physical stack unwinds. A resumed continuation starts with a fresh physical debugger depth and carries those snapshots as logical parents; the adapter appends and de-duplicates them after the live CDP frames. Logical frames do not inflate the runtime depth used by step-over and step-out. The C# `FrontEndCSharp.Debugger.Integration debug-adapter-async-smoke` command proves a real stop after `Task.Yield()` as `AddOneAsync -> Caller -> Main`, including the kickoff parameter and a hoisted local, while the ordinary smoke continues to cover synchronous stepping and thrown/unhandled exceptions.

## Edit and Continue

Edit and Continue is a post-baseline feature, but the debugger, compiler, manifest, and runtime must be designed so it does not require replacing their identity and lifetime models later. The product goal is to apply a supported C# edit without reloading the page or losing application state, then execute subsequent calls with the new code while Visual Studio continues to own source navigation, breakpoints, and diagnostics.

The end-to-end implementation is present. A resident Roslyn host owns the compilation and `EmitBaseline`, emits ordered metadata/IL/Portable-PDB differences, and conservatively accepts only supported method-body edits. The compiler translates each accepted generation into a content-hashed, synchronous runtime update plus a matching normalized debug manifest. The adapter verifies the package and generation while stopped, rejects active methods, applies the runtime transaction, publishes the matching manifest only after acknowledgement, and rebinds every requested breakpoint—including requests that were temporarily unverified against the previous source checksum. The VSIX exposes **FrontEndCSharp: Apply Code Changes** and coordinates prepare, compile, translate, apply, and baseline commit while preserving the current debug suspension.

The browser initializes `managedDebugger.updateState` from the validated manifest/build identity at generation zero. `applyUpdate` requires that baseline identity, the exact expected generation, the immediately following generation, a non-empty package hash, and a complete set of existing replacement methods. It validates every replacement and rejects duplicates, unknown methods, active methods, cross-build packages, and stale or out-of-order generations before mutating `DotNet.Methods`. The synchronous registry swap cannot yield between assignments, clears any old step plan, and returns the committed generation/hash acknowledgement. Tests cover atomic rejection, inactive-method replacement, delegate invocation through the updated registry, source-line movement and breakpoint rebinding, build and layout failures, stale replay, and stepping after an update. In the Visual Studio experimental instance, **FrontEndCSharp: Apply Code Changes** applied generation 1 while stopped, retained the debug session, emitted the successful generation diagnostic to Output, and rebound the existing source breakpoint.

### Compilation and edit analysis

Use Roslyn's Edit and Continue model rather than inventing a second C# edit classifier. `Compilation.EmitDifference` can emit metadata, IL, and Portable-PDB deltas from an `EmitBaseline` and a set of semantic edits, returning the next baseline for later generations. FrontEndCSharp cannot apply CLR deltas directly because its execution target is JavaScript; it must consume the updated method IL and PDB information and translate them into an interpreter-specific update package.

Do not assume that metadata tokens from two independent full builds identify the same symbol. Prefer one Roslyn baseline and its ordered deltas. If the supported public Roslyn surface does not expose all of the IDE's rude-edit analysis, the first implementation must define a smaller conservative edit set rather than approximate C# legality. Compilation errors or unsupported edits leave the current runtime generation untouched.

The first supported set should be intentionally narrow:

- method-body edits only;
- the edited method is not active in any managed frame;
- no method signature, generic arity, inheritance, interface, field layout, or static-initialization change;
- no update that requires migrating an existing closure, delegate target, iterator, async state machine, exception object, or runtime type instance;
- no change to compiler/runtime features that the normal build cannot already execute correctly.

This is stricter than current .NET Hot Reload and can expand only with a defined state-migration rule and regression coverage. In particular, Visual Studio and Roslyn treat active statements as a first-class edit constraint. Supporting edits to an active `ExecuteMSIL` frame would require remapping its block, instruction index, evaluation stack, locals, pending exception regions, and step plan; reject those edits initially.

### Update package and atomic application

An update package should contain the baseline identity, prior and next generation, updated method identities and blocks, changed debug-manifest records, and a hash covering the complete package. Added methods and types should remain unsupported until their metadata identity, initialization, and runtime registration semantics are designed.

Apply an update as a transaction while execution is stopped at a managed-safe boundary:

1. Compile and validate the entire update without changing runtime state.
2. Verify the runtime baseline and current generation through CDP.
3. Reject the update if any changed method is active or another compatibility rule fails.
4. Stage all replacement method bodies in the runtime and the matching next-generation debug mappings in the adapter.
5. Commit the runtime method registry, then require an acknowledgement containing the new generation and hash.
6. Publish only the matching adapter-side manifest generation, rebind source breakpoints, and clear obsolete step plans before allowing any user-visible debugger request or execution resume.

A timeout, browser navigation, CDP disconnect, validation error, or failed stage must leave the old generation fully usable. Never expose a mixture of old code with new symbols, or new code with old sequence points. If commit acknowledgement is ambiguous, stop the debug session rather than guess which generation is running.

Replacing `globalThis.DotNet.Methods` entries affects future method lookup but does not rewrite an existing `ExecuteMSIL` frame, which still owns its original blocks, locals, and instruction position. That makes inactive-method replacement a natural first boundary. Audit delegates and other cached callable references: an object that captures an old JavaScript function must either resolve the logical managed method at invocation time or deliberately retain its original generation. Do not silently choose inconsistent behavior.

Existing object and static state remains intact for a supported method-body edit. Static constructors must not rerun. Type-layout changes, field additions, and generated closure or state-machine layout changes require explicit migration semantics and are rejected until those semantics exist.

### Debugger state across generations

After a successful update, resolve every source breakpoint against the new PDB/manifest generation and report changed verification or source locations to Visual Studio. A deleted or no-longer-executable source location becomes unverified rather than remaining bound to a stale coordinate. New calls use the new generation's source map; any retained old-generation diagnostic snapshot must continue to use the map captured with that snapshot.

All DAP frame IDs, variable references, CDP remote-object handles, and cached runtime coordinates remain suspension-scoped as before. Applying an update invalidates cached method/sequence-point lookups even when execution remains paused. A pending step request must be canceled and recomputed from the updated source map rather than carrying an old-generation target forward.

### Visual Studio integration

The core Debug Adapter Protocol does not define a standard Apply Code Changes request. The supported integration is therefore the dedicated **FrontEndCSharp: Apply Code Changes** VSIX command, which uses the ordinary editor, debugger suspension, and Output/diagnostic surfaces. It communicates with the resident Roslyn host and adapter over authenticated per-session local channels and does not depend on undocumented private CPS or debugger APIs.

The first workflow should apply edits only while stopped. Applying while running would require the adapter to request a safe-boundary pause, distinguish that internal pause from a user-visible stop, apply the transaction, and resume only if the target was previously running. That can follow after stopped-state updates are correct.

Required integration tests include a state-preserving inactive-method edit, breakpoint movement and rebinding, build failure, a rejected active-method edit, a rejected layout change, stale/out-of-order update rejection, atomic failure without a mixed generation, delegate behavior, and continued stepping after a successful update. Async and iterator edits remain rejected until their state-machine migration behavior is explicitly implemented.

## Visual Studio 2022 integration

Visual Studio 2022 includes the Visual Studio Debug Adapter Host, which integrates standalone DAP adapters into Visual Studio's debugger. This is the preferred integration surface. Implementing the older COM `IDebugEngine2` family directly would add substantial Visual Studio-specific lifecycle and marshaling work without improving the interpreter debugger.

The deployed components should be:

```text
Visual Studio debugger UI
        |
        | DAP over adapter stdin/stdout
        v
FrontEndCSharp.DebugAdapter.exe
        |
        | Chrome DevTools Protocol over an isolated debugging endpoint
        v
Chrome / Brave target -> MSIL interpreter debugger hook
```

The adapter is a standalone .NET executable using `Microsoft.VisualStudio.Shared.VSCodeDebugProtocol`. Visual Studio launches it and owns its standard input/output streams. Protocol diagnostics go to standard error or a file because writing logs to standard output would corrupt DAP framing.

For standalone adapter tests and non-web launches, the adapter should launch the selected Chromium browser with a dedicated, non-default user-data directory and connect directly to its CDP endpoint. It must select only the target created for the debug session and prevent unrelated pages from being exposed through that endpoint. Chrome 136 and later require a non-default `--user-data-dir` before honoring remote-debugging switches; this isolation should be mandatory for Chrome, Brave, and any later Chromium target regardless of version. In the seamless ASP.NET workflow, however, Visual Studio's normal web and native-JavaScript launch owns the browser, profile, page, and navigation. FrontEndCSharp attaches to that exact target and must not launch, navigate, or terminate a second browser. Visual Studio's non-debug web launch honors the browser selected through **Browse With**, but its JavaScript debug launch can still omit `runtimeExecutable` and let `vscode-js-debug` fall back to Chrome. The seamless debugger launcher therefore queries the current instance's live browser-selection service for each root launch, falls back to that instance's `browsers.xml` located through `VSSPROPID_AppDataDir`, and adds only the selected supported Chromium executable after Microsoft's normal option transformation. It must not change attach configurations, child adapter sessions, navigation, profiles, or browser lifetime.

Do not make the interpreter itself speak DAP's `Content-Length` wire framing. The adapter owns DAP session sequencing and maps requests and events to CDP plus the small managed debugger hook. Keep breakpoint and stepping policy independently testable without Visual Studio or a live browser wherever practical.

DAP requires a thread even for single-threaded debuggers, so the adapter exposes one stable logical managed thread. Capabilities advertise only implemented behavior; conditional breakpoints, mutation, general expression evaluation, restart, and multi-thread support remain absent until they are real. Exact-identifier hover evaluation is supported for source-level managed arguments and locals; Visual Studio's Watch and Immediate experiences remain unavailable rather than invoking arbitrary managed code or pretending to support general C# expression evaluation.

### VSIX packaging and launch

Package the adapter in a VSIX and register it under `AD7Metrics\Engine` with the Debug Adapter Host engine CLSID, a unique engine GUID, its display name, language, executable path, and supported capabilities. Visual Studio-specific exception category registration can project managed thrown and unhandled settings into the standard Exception Settings window.

Build a development VSIX with `dotnet build Debugger/VisualStudio/FrontEndCSharp.VisualStudio.csproj -c Debug`. The project creates `Debugger/VisualStudio/bin/Debug/net472/FrontEndCSharp.VisualStudio.vsix` and runs the C# `FrontEndCSharp.Debugger.Integration verify-vsix` command automatically after packaging. A successful build is not enough by itself: retain the verifier because it checks both the outer VSIX entries and the embedded compiler payload. `extension.vsixmanifest` is generated build output; version and packaging changes belong in `source.extension.vsixmanifest`.

Install development builds into the experimental hive with `VSIXInstaller.exe /quiet /rootSuffix:Exp <path-to-vsix>`, wait for the installer and its child process to finish, then run and wait for `devenv.com /RootSuffix Exp /UpdateConfiguration` before launching the hive. A stale `VSMenus.ctmenu` cache can otherwise hide correctly packaged commands. The colon in `/rootSuffix:Exp` is significant; passing the suffix as a separate argument causes VSIX Installer to show its usage dialog instead of installing the extension. Verify the installed payload under the matching `17.0_*Exp` hive rather than assuming that a completed installer process selected the intended Visual Studio instance.

An experimental hive can retain a package codebase that names an old random VSIX directory after that directory has been replaced. The resulting package-load dialog is environmental rather than an assembly-build failure. Confirm the stale path in a fresh activity log, close that experimental instance, preserve or remove only its `ComponentModelCache`, run `/RootSuffix Exp /ResetSkipPkgs` and `/RootSuffix Exp /UpdateConfiguration`, and verify that a fresh log both names the current extension directory and records `End package load [FrontEndCSharpPackage]`. Do not reset the user's normal Visual Studio hive to repair an experimental-hive cache.

Keep the extracted compiler payload in a deliberately short cache root without rewriting any path inside the payload. Prefer `C:\FrontEndCSharp\<payload-hash>` (more generally, the system-drive root) and fall back to `%LOCALAPPDATA%\FEC\<payload-hash>` when the root is not writable. Filesystem operations that may cross the legacy `MAX_PATH` boundary should use Windows extended-length paths (`\\?\C:\...`, or `\\?\UNC\...` for a network share) only at the I/O boundary. Keep ordinary absolute paths in launch configuration, diagnostics, containment checks, and user-visible output. The .NET Framework `ZipFile.ExtractToDirectory` helper rejects an extended-length destination, so the VSIX must validate and extract each ZIP entry individually through extended-length file paths while preserving its exact relative path.

The launch work was completed in two milestones:

1. Prove the adapter with an explicit launch JSON using the Debug Adapter Host's `$adapter` property or launcher utility. This validates `.cs` breakpoint binding, source navigation, stack frames, locals, output, and exception stops before changing project launch behavior.
2. The VSIX provides an explicit **Debug with FrontEndCSharp** diagnostic command, which builds the selected project, invokes FrontEndCSharp, starts an isolated visible Chromium target, and launches the standalone registered adapter. This command is intentionally separate from Visual Studio's standard Start command and may use `FRONTENDCSHARP_BROWSER` or its Chrome/Brave discovery fallback. The package must not replace Visual Studio's standard Start command: ordinary green-button/F5 debugging belongs to the transparent proxy path, so Visual Studio remains responsible for the ASP.NET host, selected browser, native JavaScript debugger, server-side C# debugger, and unified session lifetime. The `FrontEndCSharpProject` property names the browser-executed C# project, and `FrontEndCSharpOutputPath` names the web project's generated-JavaScript directory. The standalone command remains useful for isolating adapter failures, but it is not the seamless product launch path and must not override the browser selected in Visual Studio.

That interception is a transitional implementation, not the required final UX. A marked web project's ordinary ASP.NET launch must remain intact and become a combined, multi-engine session: Visual Studio starts the configured web profile and debugs the server with Microsoft's .NET debugger, retains its native JavaScript/TypeScript debugger for the browser, and starts or attaches the FrontEndCSharp engine to that browser runtime. This must extend Visual Studio's existing seamless C# plus JavaScript web debugging rather than replace either half of it: one F5 launch, one application/browser instance, shared built-in debugger surfaces, automatic context switching when any engine stops, coordinated Continue/step commands, and one Stop action that ends the complete session. Breakpoints owned by the .NET, native JavaScript, and FrontEndCSharp engines must work throughout. The extension must not start a replacement Kestrel process or a second independent application launch when it can augment the native web launch, and it must not infer that the presence of `FrontEndCSharpProject` authorizes replacing the project's normal debug path.

### Retired post-launch composition path

The earlier design attempted to append a separate FrontEndCSharp debug adapter to an already-running ASP.NET debug session. Physical Visual Studio tests rejected both public post-launch forms: `DLO_CreateProcess` left a stale synthetic debugger program after adapter exit, and `DLO_AlreadyRunning` could not attach another engine to a process already owned by the native debugger. The private CPS launch-provider contracts and prohibited `IVsDebugLaunchHook` are not acceptable alternatives.

That path's composition probe, coordinator, browser discovery/correlation, one-shot arm record, reproduction VSIX, and automation were removed under YAGNI after the transparent JavaScript-adapter proxy passed its identity controls. Git history and the durable lessons retain the evidence. The explicit **Debug with FrontEndCSharp** command remains a useful standalone diagnostic path, but the seamless product path is the transparent DAP proxy and must not recreate the retired second-adapter architecture.

### Native JavaScript debugger reuse investigation

Visual Studio 2022 17.11 ships Microsoft's open-source `vscode-js-debug` adapter as its **JavaScript and TypeScript** browser engine. The installed engine is registered through Debug Adapter Host with one Microsoft `AdapterLauncher` and one Microsoft `CustomProtocolExtension`. Inspection of the installed registration and assemblies found no supported third-party participant chain for either component:

- the engine registration has one `CustomProtocolExtension` CLSID, so registering another value would replace Microsoft's bridge rather than augment it;
- the bridge's custom messages cover Visual Studio integration details, not a symbol-provider or breakpoint-provider contribution;
- `ILaunchConfigTransformer` is a public CLR interface used by manually constructed internal transformers, not a MEF-exported extension point;
- the public Debug Adapter Host interfaces allow an extension to customize its own registered engine, not inject messages, frames, scopes, or breakpoints into Microsoft's existing JavaScript engine; and
- the adapter's DWARF support is a hard-coded WebAssembly symbol backend (`@vscode/dwarf-debugging` / `ms-vscode.wasm-dwarf-debugging`), not a general language-symbol plug-in surface.

A technical decorator is possible but is not the missing middleware contract. A VSIX could replace the JavaScript engine's single `CustomProtocolExtension` metric with its own CLSID, instantiate Microsoft's still-registered original CLSID, forward `Initialize` and `RegisterCustomMessages` to it, and then add its own registrations and event subscriptions. That would preserve Microsoft's custom-message handlers while giving the decorator visibility into Debug Adapter Host lifecycle and protocol-preview events. It is nevertheless an unsupported override of another installed engine's registration and a versioned dependency on Microsoft's concrete bridge.

More importantly, the public interface documentation explicitly says that modification of the request arguments, response body, and event objects exposed by `PreviewProtocolRequest`, `PreviewProtocolResponse`, and `PreviewProtocolEvent` is not supported. The preview event arguments have no cancel, handled, replacement, or response facility. `IProtocolHostOperations` can send additional requests to the JavaScript adapter, and `ICustomMessageRegistry` can register additional custom adapter-to-client events and reverse requests, but neither can replace the IDE's standard `setBreakpoints`, `stackTrace`, `scopes`, `variables`, `evaluate`, `next`, or `stepIn` transaction. Such a decorator could observe a C# breakpoint request and send a second generated-JavaScript breakpoint request, for example, but it could not turn the original response into a bound C# breakpoint or replace later JavaScript frames and scopes with managed ones through the supported contract.

A full DAP proxy could perform those transformations, but it is a materially different design: it would need to interpose on the actual byte stream, forward every standard and custom message to Microsoft's adapter, preserve child sessions and Visual Studio-specific requests, and rewrite correlated requests, responses, and events. Achieving that for ordinary ASP.NET F5 would require replacing or wrapping Microsoft's `AdapterLauncher`/adapter transport as well as its custom-protocol bridge. Treat that as replacement of the native JavaScript debugger integration, not as a supported plug-in, unless a separately proven host contract permits the proxy without changing Microsoft's engine registration.

The DAP proxy is the expected fallback if artifact-only reuse fails, not a discarded approach. It should preserve Microsoft's installed `vscode-js-debug` as the implementation behind the proxy and forward native JavaScript behavior unchanged while translating only FrontEndCSharp-owned sources, breakpoints, frames, scopes, variables, evaluation, exceptions, and step state. This is preferable to immediately forking the adapter because it retains Microsoft's JavaScript implementation and narrows the code that FrontEndCSharp must maintain. A fork or complete replacement is justified only if the transport cannot be wrapped faithfully.

Edit and Continue is the principal known risk of proxying. The proxy can version mappings and redirect new breakpoint/stack traffic, but it does not automatically gain Visual Studio's managed Edit-and-Continue transaction or JavaScript live-edit semantics. Preserve generation-aware identities and atomic method/PDB updates in the protocol design, but permit a v1 proxy debugger to ship without Edit and Continue if breakpoint, stack, locals/evaluation, stepping, exception, reload, native JavaScript, and unified-lifetime behavior all pass. That is a staged delivery exception, not removal of Edit and Continue from the product goal.

The supported input surface that remains is ordinary JavaScript output plus Source Map v3 data. `vscode-js-debug` can bind a breakpoint in an original source file to a statically mapped executable JavaScript location. FrontEndCSharp could therefore translate Portable PDB sequence points into a generated JavaScript source map; it cannot give the JavaScript debugger a Portable PDB directly. This is artifact production, not debugger injection.

The current interpreter output cannot use that facility as-is. Every managed method and IL offset passes through the same `executeInstruction(opts)` implementation location, while a source map maps one static generated location to one static original location. A viable spike must emit a distinct executable JavaScript probe location for each visible managed sequence point and invoke only the matching probe at that boundary. Mapping instruction-data literals, mapping the shared interpreter line, or attaching different source locations to one `debugger;` statement cannot express the dynamic method/IL coordinate.

Even if source breakpoints bind, that proves only breakpoint and current-statement projection. Source maps do not synthesize managed scopes, reconstruct the filtered nested `ExecuteMSIL` stack, evaluate C# expressions against `opts.locals` and `opts.args`, implement managed exception categories, or apply method-version Edit and Continue. The native adapter can rename real JavaScript variables through source-map names and can customize JavaScript object descriptions/properties, but those features do not create arbitrary managed frames or writable PDB locals. Native JavaScript stepping may also enter interpreter implementation code unless source-map stepping over the emitted probe locations happens to preserve managed step-in/over/out semantics.

The installed Visual Studio bridge adds another testable constraint. Its default language map contains TypeScript for `.ts`/`.tsx` and a JavaScript wildcard for other extensions; it does not register the C# language GUID with the generic JavaScript engine. The upstream adapter's breakpoint-language contribution likewise omits C#. Blazor uses a separate engine registration and `DebugEngineType=blazor`, which is evidence that C# projection is not automatically contributed to the generic JavaScript engine. Therefore do not assume that a `.cs` source entry in a source map causes Visual Studio to route a C# breakpoint to this engine.

The reuse spike is deliberately bounded:

1. Emit one small generated JavaScript fixture with two independently executable locations and a Source Map v3 file whose original sources are two lines in a real `.cs` file. Derive those mappings from the existing Portable PDB reader rather than hand-maintaining a second symbol model.
2. Run the fixture under the native ASP.NET JavaScript debugger, with no FrontEndCSharp DAP engine or second CDP client, and determine whether Visual Studio sends and binds the `.cs` breakpoint and displays the mapped C# current statement.
3. If binding succeeds, extend only the fixture to nested synchronous managed calls and named `opts` locals, then measure stack presentation, Locals/Watch behavior, step-in/over/out, managed exception behavior, and breakpoint-update behavior.
4. Accept unchanged JavaScript-engine reuse only if it can provide the required C# breakpoint, stack, locals/evaluation, stepping, exception, reload, and versioned-code semantics without replacing Microsoft's adapter or modifying its engine registration. A breakpoint-only demonstration is not sufficient. Edit and Continue may remain a post-v1 requirement if every other semantic gate passes and the chosen design preserves generation-aware identities.
5. If `.cs` routing fails or the required managed semantics cannot be represented, close the unchanged-adapter route and begin a bounded transparent-DAP-proxy spike. The proxy must forward native JavaScript debugging and Microsoft's custom protocol behavior, transform only FrontEndCSharp-owned traffic, and prove ordinary ASP.NET F5 lifetime before product integration. Preserve the explicit diagnostic command and independent adapter/manifest/Edit-and-Continue work; do not jump directly to a maintained `vscode-js-debug` fork.

The source-map-only approach is closed with a negative physical result. In an ordinary ASP.NET F5 session, a checksum-validated Source Map v3 fixture loaded and executed two generated statements mapped to distinct C# lines, but Visual Studio did not stop at the existing C# breakpoint. This confirms that unchanged `vscode-js-debug` plus an ordinary source-map artifact does not meet the required Visual Studio C# breakpoint-routing contract. The fixture and its generator were removed under YAGNI after the result was recorded; Git history retains them. The transparent DAP proxy is the selected path.

Earlier automated UI attempts were inconclusive for environmental reasons: the loaded repository solution had a persisted multiple-startup configuration with Start disabled in a second instance, the isolated root-suffix hive stopped at first-run sign-in/setup, and a disposable one-project normal-hive `/Run` process never produced a targetable main window. The later user-driven physical F5 run supersedes those attempts and supplies the J1 verdict.

The transparent proxy begins at the actual adapter transport rather than at the custom-protocol preview API. Inspection of the installed 17.11 JavaScript integration confirms that Microsoft's `AdapterLauncher` starts the installed `DebugAdapter/src/vsDebugServer.js`, reads its loopback server port, and returns a `JavaScriptDebugAdapterBridge.exe` process whose standard streams carry DAP between Visual Studio and that server. The engine independently retains Microsoft's single `JavascriptDebuggersProtocolExtension`. The selected design delegates launch-option transformation and process creation to Microsoft's installed launcher, wraps only the returned `ITargetHostProcess` DAP streams, and leaves the registered Microsoft custom-protocol extension in place. This preserves Microsoft's adapter bundle, server/child-session launch behavior, browser ownership, and bridge while providing a byte-stream interception point.

`Debugger/Protocol/DapFrameTransformer.cs` is the first transport artifact. It incrementally parses fragmented or coalesced DAP frames, enforces bounded headers and payloads, leaves an untouched frame byte-for-byte identical, preserves unknown headers and Visual Studio custom messages, and recomputes the UTF-8 `Content-Length` only when an owned payload is changed. Its isolated tests cover fragmentation, multiple frames per chunk, transformed lengths, opaque custom traffic, and malformed duplicate length headers. It does not yet alter engine registration or prove a successful installed-adapter proxy; the next bounded step is the delegated launcher/process-stream wrapper followed by a native JavaScript no-transform control.

The delegated launcher and stream wrapper are now implemented. `JavaScriptDebuggerProxyLauncher` implements the public Debug Adapter Host launcher/component interfaces, loads the exact installed Microsoft JavaScript debugger assembly from the active IDE, delegates `Initialize`, Microsoft's normal `UpdateLaunchOptions`, and `LaunchAdapter`, and wraps the returned `ITargetHostProcess` while forwarding its handle, lifetime, termination, and diagnostic events. After Microsoft's launch-option transformation, it may add the current Visual Studio instance's selected Chrome, Brave, or Edge executable to a root browser launch; it does not alter attach or child-session configurations, including the `__pendingTargetId` browser-child launch where breakpoint composition belongs. The incremental stream tests prove partial writes are withheld until a complete frame, fragmented inner reads are reconstructed, multiple frames retain order, small consumer buffers work, and a truncated adapter frame fails explicitly. The build uses Microsoft's build-time-only `Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces` package; its assembly identity matches the installed 17.11 host (`1.6.0.0`) and the binary is not copied into the VSIX.

Brave's reused Visual Studio WebTools profile can restore old `about:blank` and application tabs on later F5 launches. For the extension-owned Brave compatibility path only, the root launch supplies a unique inert `data:` bootstrap target as both its positional argument and `urlFilter`. This forces js-debug to select the newly launched target, keeps its post-attachment `Page.navigate` barrier, and prevents an already restored application URL from winning main-target selection. Each restored blank child, plus an exact legacy `data:,` child left by the superseded implementation, is closed with `window.close()` through its already composed DAP session after `configurationDone`; the injected response is consumed by the proxy. The uniquely named current bootstrap is never treated as stale. The feature is gated by the exact Brave executable plus injected sentinel and therefore does not run for Chrome, Edge, attach sessions, or a user-supplied positional browser argument. The physical harness rejects both `about:blank` and the debugger-owned `data:` sentinel anywhere in the complete Brave accessibility tree at a managed stop. Earlier runs that checked only for `about:blank` were insufficient: a later manual run exposed a visible `data:,` tab and attachment to an old application tab. The harness also sets and verifies the full address-bar value through UI Automation before submitting navigation, preventing a dropped first keystroke from masquerading as debugger failure. The corrected installed VSIX passed four consecutive cold physical runs in alternating Simple, React, Simple, React order; the third run specifically covered the previously reported recurrence point, and every run selected the expected fresh application target with no surviving bootstrap tab.

Do not register this launcher over the native JavaScript engine in the ordinary product VSIX yet. Experimental activation must retain Microsoft's existing `CustomProtocolExtension` registration and prove identity-mode native controls covering JavaScript breakpoint binding, reload, child sessions if created, browser and server ownership, and unified Stop. Only those controls can justify adding the first FrontEndCSharp-owned request/response correlation.

The product VSIX registers the proxy CLSID and changes only the ASP.NET JavaScript engine's `AdapterLauncher` value; it contains no `CustomProtocolExtension` registration. Build verification requires the proxy assembly and registration, rejects a packaged Debug Adapter Host interface assembly, and rejects any active custom-protocol override. The C# `FrontEndCSharp.Debugger.Integration install-vsix debugger Exp` command builds and installs it into the named root suffix and removes the obsolete reproduction extension during migration.

The installed identity control passed in the Visual Studio 2022 `Exp` hive. `VsRegEdit` confirmed that the ASP.NET JavaScript engine's `AdapterLauncher` resolved to the proxy CLSID and that the CLSID resolved to the installed diagnostic assembly. Standard Visual Studio Debug Start launched one native ASP.NET lifetime, stopped at the recorder-owned server C# sentinel, continued, launched Microsoft's installed `vsDebugServer.js`, two `JavaScriptDebugAdapterBridge.exe` processes, and a visible Visual Studio-owned Chrome profile, then stopped at the recorder-owned native JavaScript sentinel. Reloading that same page produced a second native JavaScript sentinel stop and Continue in the same lifetime. At both JavaScript stops Visual Studio reported three debugger programs: the server program and two synthetic-PID JavaScript programs. That observation proves the identity proxy preserved the multi-program/bridge topology this ordinary ASP.NET profile created; it does not by itself identify which DAP messages belong to a parent or child adapter session. Ordinary Visual Studio Stop returned to design mode with `serverHits=1`, `javaScriptHits=2`, zero debugger processes/programs, and no remaining debug adapter, browser, or debuggee process from the session. The activity log contained no proxy/package load failure. This proves that identity stream interposition preserves initial and reload native-JavaScript breakpoint binding plus the unified Stop path. The package-mediated solution opener is unsuitable for this control because its projects become unloaded and `Debug.Start` becomes unavailable; pass the solution directly to a clean experimental instance and allow the recorder package to auto-load from `SolutionExists` instead of demand-loading it through the custom command-line switch.

The identity remainder is complete for the selected ordinary ASP.NET Chrome profile: reload rebound and hit the native JavaScript breakpoint, and the observed topology included two Microsoft bridge processes and two synthetic-PID JavaScript programs. The value-safe trace then captured two wrapped adapter lifetimes for each ordinary F5 run. The smaller root lifetime sent an `attachedChildSession` request; the larger browser-child lifetime emitted the native browser `process`, `thread`, and hundreds of `loadedSource` events. Visual Studio sent correlated `setBreakpoints` requests to both lifetimes, and both returned successful responses. This establishes that FrontEndCSharp source transformation belongs only on the launch configuration containing `__pendingTargetId`; the root stream must remain an identity. The trace recorded only direction, message type, command/event, correlation sequence, success state, and per-wrapped-stream identity—never expressions, source contents, locals, cookies, launch payloads, or complete protocol bodies.

The first owned transform is implemented for the browser-child stream. It recognizes C# sources only when a matching build-identity debug manifest owns the canonical source path and its checksum still matches. Manifest discovery is bounded to project `bin` output and `obj/FrontEndCSharp` debugger-session output around the web root. The latter is required because an Edit-and-Continue-generated browser payload names the manifest produced in its `enc-*` session rather than the baseline manifest in `bin`; ignoring that valid manifest makes the source appear unowned even though the payload and symbols match exactly. It resolves requested lines through Portable-PDB sequence points and aggregates all managed documents because DAP `setBreakpoints` replaces the complete set for one runtime source. All active managed locations are multiplexed through one conditional native JavaScript breakpoint at the first executable line inside `executeInstruction(opts)`; the condition is the disjunction of the existing out-of-band module ID, MethodDef token, block index, and instruction index comparisons. Multiple physical breakpoints at that same JavaScript line are invalid because `vscode-js-debug` binds only one and reports later duplicates as unbound, which made breakpoints added during a live session appear unsupported. The proxy retains the adapter's shared physical breakpoint ID internally but assigns a distinct stable client-facing ID to each logical C# location. A stop shared by multiple locations performs the existing identity lookup to select the exact C# frame; until then, the proxy omits the ambiguous physical `hitBreakpointIds` from the client event. That resolved logical binding becomes authoritative for the entire suspension so repeated Visual Studio stack refreshes cannot fall back to the group's first breakpoint. When the JavaScript adapter initially reports the shared breakpoint as pending and later emits a verified breakpoint-change event, the proxy fans that update out to every logical C# location using its own ID. This does not add an IL-offset property to runtime instruction objects. Responses, later breakpoint-changed events, stopped breakpoint IDs, and matching stack frames are correlated and projected back to the original C# source location. Unowned sources, uncorrelated responses, unrelated events, and native JavaScript frames remain byte-for-byte unchanged. The transform refuses root sessions, mismatched build identities, changed sources, and missing generated runtime artifacts. Unit coverage owns single-breakpoint multiplexing, stable logical breakpoint identity, live response projection, response/event/stack correlation, exact unrelated-frame preservation, baseline and Edit-and-Continue build-identity discovery, and manifest checksum validation.

The bundled source map defaults to the `managed` profile because JavaScript source
maps are only a rendezvous mechanism for managed debugging, not the representation
of C# application code. The profile retains `_executeInstruction.js`,
`debug-build.js`, and selected companion `runtime-assets`, while excluding vendor
packages, generated managed registrations, and other interpreter implementation
modules from source-map generation. The proxy still receives the exact mapped
runtime-source `loadedSource` event required to start configuration and continues
to obtain application semantics from the Portable PDB/debug manifest. The `full`
profile is an explicit compiler/runtime-development option rather than the product
default.

VSIX reinstall changes the extension's randomized installation directory. The experimental configuration cache can retain the old expanded `$PackageFolder$` CodeBase even when VSIX Installer reports success, causing adapter launch to fail before managed code loads. A quiet `VSIXInstaller.exe` process can also return before its worker processes finish; running `/UpdateConfiguration` immediately can therefore rebuild the hive against the installation that is being removed. The debugger installer requires the experimental instance to be closed, cleanly uninstalls the prior reproduction VSIX, waits for each uninstall/install process to finish, runs `devenv /RootSuffix <suffix> /UpdateConfiguration`, reads the effective CLSID CodeBase with `VsRegEdit`, and fails unless that exact assembly exists. A successful VSIX build or the presence of the DLL in a newer extension directory is not sufficient installation evidence.

Physical browser-selection runs exposed an earlier point in the ASP.NET F5 pipeline. Both Chrome and Brave selections launch the requested visible browser, but the observed Chrome path contributes a JavaScript debug target to the combined session while the observed Brave path does not. No proxy launcher, DAP stream, or JavaScript-adapter log is created in the latter case, so changing `runtimeExecutable` inside `UpdateLaunchOptions` cannot repair it. This is evidence of different browser-specific launch handling, not evidence that Visual Studio intentionally excludes Brave. The compatibility shim therefore observes ordinary `Debug.Start` in the priority command-routing stage, before the standard handler asks WebTools for its debug targets. It never claims or replaces that command: after preparing compatibility state it returns `OLECMDERR_E_NOTSUPPORTED`, allowing ordinary F5 routing to continue. Only when Brave is selected does it temporarily ask `IBrowserDebugTargetSelectionService2` to select the installed Chrome entry so WebTools composes its proven JavaScript-debug target. A DTE `BeforeExecute` event is too late because WebTools has already snapshotted its target path by then. The package is loaded at shell initialization, but it resolves the WebTools browser-selection service lazily on each F5 attempt because that service may not exist yet when an asynchronous package initializes. A failed lookup is retried rather than cached as permanent absence. The shim records the exact installed Brave executable in process-scoped launch state; the existing root-launch proxy substitutes that executable after Microsoft's launch transformation, and the package restores the user's Brave selection when run mode begins or the launch ends. Visual Studio continues to own the single F5 transaction, web host, browser profile, JavaScript adapter, server debugger, and unified Stop. The shim does not launch a second browser or modify child/attach sessions. Chrome and Edge remain unchanged. Physical acceptance must confirm that Brave opens, the JavaScript debug program exists, and server C#, raw JavaScript, and FrontEndCSharp breakpoints all bind in that one session.

Visual Studio creates separate `IAdapterLauncher` instances for the parent JavaScript session and its attached browser child; the two metadata traces both begin at stream 1. The parent launch carries the web-root information, while the observed child launch carries `__pendingTargetId` but not a usable runtime root. Breakpoint composition must therefore retain candidate roots process-wide between the parent and child launches, reset them when a new parent launch begins, and still validate the runtime build identity and debug manifest before claiming a C# source. Instance-local composition state leaves the child at `runtime-source-not-found` and forwards C# breakpoints unchanged.

Breakpoint-response projection alone is insufficient. In the first successful composed stop, the child configured, the C# request resolved, the adapter changed the projected breakpoint from unverified to verified, and execution stopped at `executeInstruction`; Visual Studio then issued `stackTrace` and opened the unmodified JavaScript runtime frame. The proxy must correlate `stopped.body.hitBreakpointIds` with the projected breakpoint binding, correlate subsequent `stackTrace` requests and responses, and rewrite only the matching `executeInstruction` frame to the original C# source/sequence point. Native JavaScript frames below it remain untouched. This frame projection is what makes an already-working conditional runtime breakpoint appear as a C# debugging experience.

The first managed-inspection slice reuses that projected frame ID instead of opening a second CDP connection. A `scopes` request for the correlated managed frame is translated into an `evaluate` request on the same paused JavaScript frame. The proxy resolves PDB parameter names, active lexical-local names, and async state-machine hoisted fields from the active build manifest, then generates a side-effect-free, null-prototype object whose properties read the corresponding `opts.methodArgs`, `opts.locals`, or state-machine fields. The JavaScript adapter returns a native `variablesReference` for that live object; the proxy exposes it as the managed `Locals` scope and keeps subsequent `variables` transactions native. It tracks only references descended from that managed scope and removes the JavaScript debugger's synthetic `[[Prototype]]` property from their responses. This preserves the JavaScript debugger's own primitive formatting, lazy expandable-object references, paging, and remote-object lifetime without leaking JavaScript object plumbing into managed Locals. Requests for ordinary JavaScript frame IDs and variable references remain unchanged.

Unit coverage proves current-frame scope correlation, recursive managed-variable-reference tracking, removal of `[[Prototype]]` at both the Locals root and nested managed objects, and byte-for-byte preservation of unrelated JavaScript scope and variable requests. Physical Visual Studio validation proves that the projected `Program.cs` stop displays `a = 2`, `b = 3`, `c = 5`, `hello = "world"`, and an expandable `complex` object in the built-in Locals window. Arguments belong directly in the single Locals scope; neither debugger path should create an `Arguments` pseudo-variable or second scope. C# expression evaluation remains bounded inspection work. Apply YAGNI: do not build a parallel object-inspection protocol while native JavaScript object references satisfy the acceptance cases.

The nested-frame slice adds a duplex DAP transform rather than a second debugger connection. The JavaScript adapter's stack remains authoritative for native JavaScript frames, but it is not the source of truth for managed callers: every live `ExecuteMSIL` frame already contributes itself to the active instruction's `opts.ctx.debugFrames`. Ordinary projected C# breakpoints are conditional JavaScript breakpoints at `executeInstruction`, so the runtime's `currentStop` marker is deliberately null there; runtime-owned step and exception stops expose the same context through `currentStop.frame.ctx`. Every stack, parent-scope, and step probe must therefore read `opts.ctx` first and use `currentStop.frame.ctx` only as the fallback. A correlated `stackTrace` response is withheld while the proxy reads the bounded physical/async counts from that context and sends one small identity-only `evaluate` request per managed parent. Physical parents are read from `ctx.debugFrames`; only callers lost when JavaScript resumes after an `await` come from the immutable `ctx.asyncParents` snapshots. This avoids reconstructing one managed stack from paged JavaScript stack responses and hides duplicate `executeInstruction` implementation frames. The identity requests carry only module ID, MethodDef token, block index, and instruction index and therefore do not cross the adapter's truncated presentation-string boundary with managed values. The proxy inserts manifest-resolved C# parents after the active managed frame and assigns proxy-owned frame IDs. A `scopes` request for a physical parent reads that live context frame; an async parent reads its immutable snapshot. Both produce a null-prototype Locals object and return only the adapter's native `variablesReference`. The manifest resolver maps source-bearing identities to C# method/source coordinates and falls forward to the method's first visible sequence point when a frame is suspended in compiler-hidden prologue IL before any visible point. A runtime identity whose method has no visible PDB source is confirmed as an implementation frame and omitted rather than mislabeled as user C#. Ordinary JavaScript frames are retained. Unit coverage proves context-stack parent projection, async-parent extension, implementation-frame filtering, managed-parent scope routing, and later-page duplicate suppression. Physical Visual Studio validation proves the synchronous path: the built-in Call Stack showed exactly `Program.NestedLeaf` → `Program.NestedCaller` → `Program.Main`, selecting `Program.NestedCaller` navigated to its C# sequence point, and the built-in inspection window showed `value = 5` and `callerValue = 6`. The post-`await` logical caller and snapshot-backed Locals behavior are accepted for the current seamless-debugger scope.

The managed-stepping bridge reuses the runtime's existing `requestStep` plan. While a manifest-owned C# stop is active, the proxy translates DAP `next`, `stepIn`, or `stepOut` into a side-effect-free `evaluate` bound to the paused `executeInstruction` frame. That frame binding is required because an ordinary conditional-breakpoint stop has no runtime `currentStop`, and only the paused frame exposes the authoritative context. The baseline and candidate depth are the physical `ctx.debugDepth` plus the number of accepted `ctx.asyncParents`; using physical depth alone makes every post-`await` frame depth 1 and causes Step Over to enter logical children and Step Out never to reach a logical caller. If Visual Studio has not requested `stackTrace`, the proxy first performs and consumes an internal stack lookup, locates the runtime frame, and only then arms the runtime with all visible PDB coordinates. A missing runtime frame fails the original step request without resuming. The proxy then sends an adapter-owned `continue`; no second CDP client is added. The next adapter `stopped` event is withheld only long enough for one global probe of `currentStop`; that result contains both the managed reason and the exact module, method, block, and instruction identity selected by the runtime. The proxy must use that authoritative identity to project the new active frame. It must not depend on rediscovering the location from the physical `managed-debugger.js` frame at the `debugger;` statement: that redundant lookup exposed the helper source after an otherwise successful F10. Visual Studio requests the top stack repeatedly with different formatting options during one suspension, so this projection must remain stable for every request until `continued`, not only the first full-stack response. A raw JavaScript stop is released unchanged and leaves the managed plan armed. A managed breakpoint, pause, or exception stop is terminal for the pending step; the runtime clears the plan before reporting that stop, and the proxy clears its correlation for managed breakpoint and exception stops. Continue therefore cannot later produce a stale step stop. Unit coverage proves Step Into/Over/Out mapping with and without a prior client stack request, frame-lookup failure, the arm/continue/stop transaction, native-stop preservation, managed-stop cancellation, repeated authoritative stop-identity projection, and physical plus logical depth comparisons. Browser-backed adapter smoke tests exercise synchronous Step Over, Step Out, and Step Into and post-`await` Step Out into the logical caller against generated code. Physical Visual Studio acceptance in the Brave composed session confirms that the corrected repeated-stack projection keeps managed stepping in C# instead of exposing `managed-debugger.js`; this closes the 2026-10-16 Brave stepping regression.

The first managed-exception bridge composes two FrontEndCSharp filters into the JavaScript adapter's existing exception capabilities and `.pkgdef` category mappings. The standalone and composed engines share one category GUID, the same visible filter labels, and the same namespaced `frontendcsharp-*` DAP IDs, so Visual Studio presents one coherent **FrontEndCSharp Managed Exceptions** tree without confusing a managed setting with a native JavaScript filter. It also advertises `supportsExceptionInfoRequest`, because the proxy owns the managed `exceptionInfo` implementation even if the underlying JavaScript adapter does not advertise that request. On `setExceptionBreakpoints`, the proxy removes only the namespaced FrontEndCSharp filter IDs from both legacy `filters` and structured `filterOptions` before forwarding the request, leaving native JavaScript policy such as `all` or `unhandled` and native options intact, then uses a global adapter `evaluate` to configure the existing runtime exception controller. If the runtime module has not loaded yet, the evaluation stages the filter set for `managed-debugger.js` to consume at initialization. The proxy commits its effective managed policy only after both the native adapter request and runtime evaluation succeed; either failure retains the previous policy. Microsoft's JavaScript adapter remains solely responsible for deciding whether JavaScript caught an exception. While a managed exception filter is active, a native `stopped` event is withheld only long enough to probe the runtime's value-free stop identity; if the runtime does not claim it, the proxy releases the original event byte-for-byte. A managed exception stop is relabeled with its managed type and thrown/unhandled stage. Each new stop also invalidates Exception Helper state from the preceding suspension even if Microsoft's adapter omitted a `continued` event, preventing a later native JavaScript exception from inheriting stale managed details. Async methods retain their immutable first-throw snapshot by wrapped exception identity; only a faulted entry task crossing the JavaScript host boundary is reported as unhandled, so ordinary awaited-and-caught task faults remain managed behavior. Browser generation uses a dedicated full-generation stamp, rather than mutable `index.js`, so debugger build-identity work cannot hide a newer runtime payload and omit that host-boundary hook. The factory preloads matching build manifests from bounded project `bin` and `obj/FrontEndCSharp` directories around the web root, so an exception can map its module/token/runtime coordinates back to C# source even when the user set no C# breakpoint first. `exceptionInfo` is synthesized as `FrontEndCSharp/<managed-type>` for Visual Studio's registered exception category. Unit coverage proves capability composition, combined native-and-managed policy preservation, native filter-option preservation, transactional runtime configuration including both failure paths, source-independent managed-stop correlation, exception details, suspension-scoped exception state, and native-only byte-for-byte pass-through. Browser-backed smoke coverage proves synchronous thrown/unhandled stops and post-`await` unhandled entry-task projection at the original C# throw line. A control test in an unrelated MVC project showed that the installed Visual Studio JavaScript debugger also failed to pause for the same uncaught timer-callback exception. Repairing that upstream debugger behavior is outside FrontEndCSharp's scope; the exception slice is accepted on managed behavior plus exact preservation of native configuration and stop traffic.

The first physical inspection attempts also established an important transport boundary. Microsoft's adapter truncates the display string in a successful DAP `evaluate` response at 1,000 characters. Plain JSON, a compact positional schema, and a temporary versioned Base64 wrapper all crossed that presentation boundary and were truncated; encoding was not a reliable or desirable debugger transport. The Base64 design and its synthetic variable store were removed. The selected design carries only the adapter's native `variablesReference` and never serializes managed values into `evaluate.result`. Value-safe diagnostics record only transaction state and never expressions, payloads, or managed values.

Every transition needs structured, value-safe tracing: launch nonce, state, timestamp, project hash, build identity, process/target IDs, and API result codes are useful; source values, locals, cookies, complete command lines, and CDP payloads are not. A failed augmentation must say which evidence was absent or ambiguous and must never silently start the old replacement Kestrel/browser path.

### Current execution plan

The transparent DAP proxy is the only active seamless-debugger architecture. Keep Microsoft's installed `vscode-js-debug`, its custom-protocol extension, browser ownership, and ASP.NET launch lifetime intact. Interpose only on the installed launcher's DAP streams and transform only transactions that belong to FrontEndCSharp sources.

The proxy is not a new debugger implementation. The standalone FrontEndCSharp adapter and runtime already implement and test managed breakpoint binding, source frames, scopes and variables, stepping, managed exceptions, async stacks, and Edit and Continue. The proxy is the composition layer that bridges those existing managed semantics into the ordinary ASP.NET plus native-JavaScript F5 session while preserving Microsoft's browser ownership and debugger behavior. Reuse the existing manifest, runtime, and debugger-kernel behavior; do not create parallel implementations of those features in the proxy.

The debug manifest has one assembly-level ownership boundary across both debugger
paths. `FrontEndCSharp.DebugManifest` is a `netstandard2.0` assembly containing the
immutable schema, single loader and validation policy, path/checksum identity, and
neutral breakpoint, frame-layout, and step-location indexes. Both the `net472`
Visual Studio extension (through `FrontEndCSharp.Debugger.Protocol`) and the
`net8.0` standalone adapter reference it normally. The composed protocol assembly
retains multi-manifest discovery and JavaScript-accessor projection; the standalone
adapter retains CDP object graphs, suspension-scoped handles, assignment targets,
and Edit and Continue coordination. Its thin projection composes the shared
manifest result and no longer reparses or reindexes the schema.

The identity proxy has passed the required baseline in the Visual Studio 2022 experimental hive: ordinary F5 hit the server C# breakpoint and native JavaScript breakpoint, reload rebound and hit the JavaScript breakpoint again, and one ordinary Stop removed all observed programs, bridge processes, browser processes, and the debuggee.

The early metadata-only DAP envelope tracer established that manifest-owned breakpoint traffic crosses the browser-child stream. Once routing and owned transformations had focused regression coverage, the tracer and its file output were removed; operational diagnostics remain value-safe and do not record DAP payloads, expressions, source contents, locals, cookies, or complete command lines.

The initial physical breakpoint validation passed in the Visual Studio 2022 experimental hive with Chrome. In one ordinary ASP.NET F5 lifetime, the user successfully debugged server-side C#, a FrontEndCSharp breakpoint projected back to what is now `Demo/FrontEndCSharp.SimpleDemo/Program.cs`, and an ordinary native JavaScript breakpoint in `index.js`. The same session retained the visible browser, server console output in Visual Studio's Output window, Microsoft's JavaScript adapter/bridge topology, and the ordinary Visual Studio debugger UI. A subsequent 2026-10-15 Brave failure established that WebTools launched the selected Brave executable with only `https://localhost:7120/`; the process had no isolated debug profile or remote-debugging pipe, and Visual Studio created no new JavaScript-adapter log, proxy DAP trace, or debugger-owned browser launch. The ten-second FrontEndCSharp rendezvous therefore expired exactly as designed. Inspection of the installed Visual Studio bits explains the browser-specific split: `Microsoft.VisualStudio.Web.Application.dll` contains a private `DebugCfg.BrowserType` enum with `IE`, `Chrome`, `MSEdge`, legacy `Edge`, `WcfTestClient`, and `Unknown`, together with `BrowserIsChrome`, `BrowserIsMSEdge`, `BrowserSupportsScriptDebugging`, `GetChromiumBrowserArgs`, and `GetBrowserTypeFromPath`; the assembly contains no Brave classification. The installed `vscode-js-debug` bundle likewise auto-discovers only Chrome and Edge and exposes only `pwa-chrome` and `pwa-msedge`, but its browser launcher returns an explicit non-quality `runtimeExecutable` path unchanged and applies the ordinary Chromium default arguments plus `--remote-debugging-pipe` or `--remote-debugging-port`. Therefore Brave does not require an adapter fork: it enters WebTools through a supported Chromium composition path and then replaces only the adapter's explicit executable with Brave. Later physical acceptance closed that compatibility path.

The pre-F5 Brave composition hook now produces the intended single JavaScript-debugger lifetime and the proxy receives the C# breakpoint request, but the next physical run exposed a later startup failure. The proxy waited for both the interpreter source and the adapter's successful `configurationDone`, sent its internal runtime-configuration evaluation, and received a successful DAP response, yet `managed-debugger.js` still timed out the matching build rendezvous. Microsoft's installed adapter performs frame-less `evaluate` through its selected execution context; Brave can expose more than one context, so a successful evaluation does not by itself prove that the expression ran in the application page's `globalThis`. Before publishing the FrontEndCSharp configuration, the proxy now sends the adapter's existing REPL command `cd top`, consumes that internal response, and only then evaluates the build-bound configuration. The transaction fails closed if context selection fails. Focused regression coverage proves the ordering, success path, failure path, and suppression of internal responses.

The runtime source can be removed while that two-request transaction is still in flight when Chromium replaces or reloads the document. A `loadedSource` removal now invalidates the selected execution context and abandons the outstanding internal request; its eventual response remains suppressed. When the matching runtime source is added for the replacement document, the proxy starts a new context-selection and configuration transaction instead of allowing the stale failure to strand the managed rendezvous.

Browser reload creates a new JavaScript runtime even though the composed DAP session and its successful `configurationDone` state survive. The proxy therefore treats a later load of the interpreter source, after the preceding runtime-configuration transaction has finished, as a new runtime generation: it selects the application context again and republishes the build-bound configuration. Repeated source events while a transaction is still in flight remain coalesced. Without this generation boundary, refresh leaves the new `managed-debugger.js` instance waiting until its ten-second rendezvous rejects even though the original page configured successfully.

Physical keyboard-F5 validation in the Experimental hive closes the Brave startup slice for the standard `Debug.Start` route. Visual Studio launched one visible Brave process with its normal isolated WebTools profile and `--remote-debugging-pipe`; the child-session trace recorded successful `runtime-context-selection-complete` and `runtime-configuration-complete`, correlated the managed breakpoint stop, and projected the C# stack frame and Locals scope. The built-in editor stopped in what is now `Demo/FrontEndCSharp.SimpleDemo/Program.cs` before the `ComplexType` constructor assignment, so `complex = null` was the correct pre-statement value; the ordinary lowercase `args` parameter appeared directly in Locals without a synthetic `Arguments` group.

The web-profile green Debug button is a distinct command route, not merely another input binding for keyboard F5. The Visual Studio automation model identifies keyboard F5 as `Debug.Start` (`{5EFC7975-14BC-11CF-9B2B-00AA00573819}:295`) and the toolbar button as `Debug.StartDebugTarget` (`{6E87CFAD-6C05-4ADF-9CD7-3B7943875B7C}:257`). A physical failure run confirmed that recognizing only `Debug.Start` lets the toolbar launch Brave without the pre-launch compatibility hook, remote-debugging transport, JavaScript adapter, or proxy. The priority command target recognizes both identities, applies the same preparation, and still returns `OLECMDERR_E_NOTSUPPORTED` so Visual Studio owns either launch. Physical validation now proves both keyboard F5 and the toolbar route create the native JavaScript target and bind FrontEndCSharp breakpoints.

The completed Brave acceptance pass also covered repeated startup, live add/disable/re-enable/remove breakpoint mutation, server-side C#, raw JavaScript, and FrontEndCSharp breakpoints in one composed lifetime, reload, Break All, Continue, browser close, native-JavaScript-disabled behavior, and unified Stop. These checks produced one coherent Visual Studio debugging experience without a second browser, adapter, or debug lifetime.

The subsequent YAGNI pass removed the standalone proxy-reproduction VSIX, CDP suspension spike, command-line launch fixture, breakpoint-target fixture, completed DAP envelope tracer and its tests, and duplicated install/verification scripts. The product VSIX now owns the single proxy registration and the installer verifies that effective registration directly. A temporary method audit confirmed the required Visual Studio and launcher paths, then was removed after its synchronous `QueryStatus` logging demonstrated why blanket UI-thread instrumentation is not production-safe.

Physical managed-value presentation now matches the material behavior of Microsoft's C# debugger for the exercised frame. Primitive locals use C# aliases, an empty `string[]` appears as `{string[0]}` without an expansion node, nonempty arrays expose indexed children rather than JavaScript's `length`, and managed objects appear as `{Type}` with auto-property names rather than compiler backing fields or JavaScript prototype/symbol entries. The proxy changes only variables references created for projected managed scopes; native JavaScript variables remain unchanged. It emits DAP's standard `presentationHint.kind = property` for projected auto-properties, but Visual Studio's JavaScript debug UI ignores that hint and continues to use its generic member icon instead of the native C# property's wrench icon. Do not add a Visual Studio-specific icon workaround unless the icon becomes a functional requirement.

Command-line `/Run` and startup `/Command Debug.Start` are not substitutes for that physical acceptance test. In the experimental hive they executed before the package's priority command target was active, launched the web application and Brave without a JavaScript-debugger target, and produced no proxy DAP trace. Only a toolbar or keyboard F5 issued after package initialization exercises the pre-F5 compatibility hook and the ordinary composed debugger path.

Treat FrontEndCSharp Edit and Continue as post-v1 if necessary, while preserving generation-aware identities so it is not designed out.

Do not add a second browser, CDP client, web host, debug adapter, launch coordinator, target-discovery layer, or one-shot build protocol unless a failing seamless-debugger test demonstrates that the proxy cannot supply a required behavior. Do not fork `vscode-js-debug` unless faithful stream proxying is disproven.

### Verification layers for the plan

- Unit tests own DAP framing, partial reads/writes, identity forwarding, owned transforms, and malformed-frame rejection.
- MSBuild integration tests own normal generated artifacts and PackageReference package consumption.
- Experimental-hive tests own native JavaScript preservation, browser ownership, breakpoint routing, reload, debugger command routing, and unified Stop.
- Interactive evidence must remain value-safe: record stream/message metadata and process/program counts, never source contents, expressions, locals, cookies, launch payloads, or complete protocol bodies.

Proxy ownership is a correctness boundary:

- Transform a request only when its canonical source belongs to a validated FrontEndCSharp debug manifest.
- Preserve native JavaScript and Visual Studio custom traffic byte-for-byte.
- Correlate every transformed response/event to the originating request and stream; never infer ownership from whichever source or program was most recently active.
- Do not issue independent CDP execution-control commands from the proxy. Continue, pause, and stepping remain transactions of Microsoft's adapter unless and until a tested managed projection requires a narrowly owned translation.

Edit and Continue has two independent owners in the combined session. Microsoft's .NET debugger continues to own server-side Hot Reload/Edit and Continue without interception. FrontEndCSharp owns browser-executed C# updates through its generation-checked update channel. The proxy must not merge generations or treat a successful update in one program as success in the other. Acceptance must cover server-only, FrontEndCSharp-only, and simultaneous valid edits, plus rejection in either engine while the other remains debuggable. Native JavaScript live editing remains Visual Studio/browser behavior and must not be disabled by FrontEndCSharp attachment.

Each spike is a decision gate. If Debug Adapter Host cannot publicly join the existing session, or Visual Studio's native JavaScript engine cannot safely share pause control, do not disguise separate sessions as seamless and do not restore the private CPS dependency. Preserve the explicit diagnostic command, record the blocker, and take the minimal reproduction to the Visual Studio Debug Adapter Host/extensibility owners. The older conditional-breakpoint fallback at `executeInstruction(opts)` may improve runtime overhead, but it does not solve multi-engine composition and is not a substitute for these gates.

The acceptance matrix for the completed feature is:

- With native JavaScript debugging enabled, one F5 launch hits server-side .NET, native JavaScript/TypeScript, and FrontEndCSharp breakpoints in either order without losing or duplicating stops.
- With native JavaScript debugging disabled, the same profile still provides .NET plus FrontEndCSharp debugging without requiring a different launch command.
- Continue, Break All, Step Into, Step Over, Step Out, exception settings, reload, Edit and Continue, and Stop affect the correct active engine and leave the others consistent.
- Exactly one web host, browser profile, browser window, and application page are created. Stopping the session leaves no adapter, compiler host, or extension-owned rendezvous process behind.
- Chrome is the first required native-JavaScript coexistence target. After Chrome passes the seamless-debugger acceptance matrix, run the same native-JavaScript coexistence matrix against Brave. Visual Studio documents native browser debugging for Chrome and Edge, not Brave, so any Brave limitation must be reported explicitly rather than silently falling back to a second browser or losing JavaScript debugging. This is a compatibility-validation requirement, not a separate implementation architecture.

The compiler now supports `--debugger-rendezvous-timeout-ms`. Debug launches generated with that option import the debugger controller and wait at the managed entry-point boundary until the matching build configuration arrives. Static module loading may continue, and the wait does not block the document or unrelated native JavaScript. Successful configuration releases the entry point only after breakpoints and exception filters are installed. Failure rejects after the configured bound, writes a browser-console diagnostic, and cancels managed entry-point execution rather than leaving the page hung. Normal compiler output contains no rendezvous. The diagnostic VSIX command enables a ten-second rendezvous, and the attach-only smoke navigates and imports immediately—without an artificial pre-attach delay—to prove that the entry breakpoint is retained. The final seamless launch still has to enable this compiler option through the normal marked-project build.

The normal-build package slice is implemented. `Compiler/Packaging/Assets/FrontEndCSharp.targets` makes the configured FrontEndCSharp project a non-reference-producing project dependency, obtains its actual target path from MSBuild, and generates browser output after the web build rather than through an ad-hoc `PreBuild` shell command. Repository development may still name `FrontEndCSharpCompilerProject`, but product builds consume the `FrontEndCSharp.Build` NuGet package. Its `buildTransitive` props/targets are imported through the supported PackageReference mechanism, and it carries the compiler as `CompilerPayload.zip` plus a small bootstrapper. The bootstrapper and VSIX share the same extraction implementation: payload hash selects a short `C:\FrontEndCSharp\<hash>` cache (falling back to `%LOCALAPPDATA%\FEC`), archive traversal is rejected, and extended-length paths are used only during filesystem extraction. This avoids expanding the runtime's intentionally descriptive filenames beneath the already-long global-package path. The versioned package includes its consumer setup README. The VSIX contains the build package as `Build/FrontEndCSharp.Build.nupkg`; its verifier inspects the nested package and builds a clean PackageReference consumer from an isolated NuGet cache.

A dedicated Web Worker remains a possible runtime host because it isolates execution from page UI. CDP target discovery and attachment must explicitly cover a worker-hosted interpreter before that configuration is supported.

## Delivery sequence

### 1. Visual Studio adapter-host spike

- Build a minimal standalone DAP adapter and development VSIX from the official Visual Studio Debug Adapter Host sample shape.
- Launch it in a Visual Studio 2022 experimental instance against a fixed C# source file.
- Prove breakpoint binding and source navigation, one thread and stack frame, Locals, Output, and a managed exception stop using only Visual Studio's built-in UI.
- Record and validate the supported packaged-command and native-F5 paths for an ordinary SDK-style `.csproj`.

### 2. Portable PDB vertical slice

- Read and validate symbols for a small debug-built fixture.
- Prove exact sequence points, hidden points, document checksums, parameter names, nested local scopes, and state-machine mapping in compiler tests.
- Emit a debug manifest for included user methods without editing generated output as source.

### 3. CDP suspension vertical slice

- Launch Chrome and Brave with isolated temporary profiles and attach to the intended target through CDP.
- Pause from an `ExecuteMSIL`-shaped nested call using `debugger;`, inspect each live `opts` frame, resume, and prove execution continues exactly once from the following instruction.
- Prove the safe-boundary bounce for an arbitrary Pause request and clean debugger detach behavior.
- Benchmark the inline instruction-boundary hook against a conditional CDP breakpoint on the `executeInstruction(opts)` call site.

### 4. Debugger kernel vertical slice

- Bind a breakpoint in one synchronous C# program.
- Stop before its IL instruction, inspect the source frame, arguments, and locals, then continue to the same final result as a non-debug run.
- Add step in, over, and out plus thrown and unhandled exception stops.

### 5. Adapter-to-runtime integration

- Connect the Visual Studio adapter directly to the isolated browser CDP endpoint.
- Map Visual Studio source paths and breakpoints to validated PDB documents and runtime method/IL locations.
- Drive Visual Studio's built-in debugger windows entirely through DAP; do not add a custom debugger UI.
- Package the adapter and launch contribution in a VSIX.

### 6. Async and product hardening

- Map state-machine frames and hoisted locals to source concepts.
- Record and display logical async causality.
- Add source packaging policy, worker hosting, path mapping, external-code filtering, large-value paging, and diagnostics for missing or mismatched symbols.

### 7. Edit and Continue

- Establish a Roslyn baseline and produce one method-body/PDB delta for an inactive method.
- Translate that delta into an atomic runtime method/debug-manifest update and prove that existing application state survives.
- Rebind Visual Studio breakpoints to the new generation and continue stepping in the updated source.
- Reject active-method, layout-changing, stale, and partially applicable edits without changing the running generation.
- Integrate Apply Code Changes through a supported Visual Studio extension point or a dedicated VSIX command.

### 8. Seamless ASP.NET, JavaScript, and FrontEndCSharp debugging

- Complete Stages A through F in the mixed-engine integration plan and record their results before choosing the final launch hook.
- Move browser-output generation into the normal web build, preserve the native launch, and add an attach-only adapter path with strict target/build correlation.
- Implement foreign-versus-managed CDP pause arbitration and prove native JavaScript breakpoints and policies remain intact.
- Validate the complete acceptance matrix in a Visual Studio experimental hive before removing the replacement `Debug.Start` path.

### Managed variable editing

Both debugger paths advertise DAP `setVariable` for live managed values. The standalone adapter retains the physical CDP call-frame identity and compiler-derived storage target for each projected argument, local, and reachable instance field, then evaluates a checked assignment against the live `opts.methodArgs`, `opts.locals`, or object field. The composed adapter projects live values as JavaScript accessors whose setters update the corresponding runtime storage; its DAP transform restores the hidden managed property key before forwarding `setVariable` to js-debug. Native JavaScript variable references are never claimed or rewritten.

Assignments preserve managed `{t,v}` wrappers and validate booleans, characters, strings, finite floating-point values, fixed-width integer syntax and ranges, nullability, and supported reference nulls. Floating-point rendering and snapshots explicitly display negative zero as `-0` instead of allowing JSON or ordinary JavaScript string conversion to erase its sign. Object replacement, enum parsing, and values without managed type information fail explicitly. Variable references are valid for one suspension only. Continued or superseded frames report a stale-reference error, and logical async/exception snapshots remain immutable. The standalone browser smoke test edits an argument, observes the changed value, executes the next instruction, observes the changed result, edits the resulting local, and rejects invalid and stale writes. Composed-adapter coverage proves live accessor projection, managed name restoration (including auto-properties and array elements), immutable-snapshot rejection, and native-reference pass-through.

## Initial non-goals

- Debugging generated JavaScript or relying on browser F12 tools.
- Building a custom source editor, breakpoint gutter, call-stack window, locals window, or other debugger UI.
- Firefox, Safari, and other non-Chromium suspension backends.
- Simultaneously debugging multiple page or worker execution targets.
- Attaching to the user's ordinary browser profile or sharing a target with external F12 browser DevTools. Sharing Visual Studio's own native-JavaScript target is required and governed by the mixed-engine plan above.
- Windows PDB support.
- BCL source stepping.
- Conditional breakpoints, data breakpoints, logpoints, expression evaluation, or set-next-statement.
- Editing an active method, migrating closure/async/iterator/type layout, and restarting an individual managed frame. Edit and Continue currently supports conservative inactive method-body replacement only.
- Reverse debugging.
- Pretending JavaScript microtasks are CLR threads.

These remain outside the initial delivery. Later milestones may take them on only after the basic interpreter debugger is correct and the required semantics are explicit.

## Primary references

- [Portable PDB format specification](https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md)
- [PEReader API](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.pereader)
- [System.Reflection.Metadata guidance](https://github.com/dotnet/symreader)
- [Debug Adapter Protocol overview](https://microsoft.github.io/debug-adapter-protocol/overview)
- [Debug Adapter Protocol specification](https://microsoft.github.io/debug-adapter-protocol/specification)
- [Visual Studio Debug Adapter Host and samples](https://github.com/microsoft/VSDebugAdapterHost)
- [Visual Studio Debug Adapter Host compound-session discussion](https://github.com/microsoft/VSDebugAdapterHost/issues/13)
- [Visual Studio custom debug engine architecture](https://learn.microsoft.com/visualstudio/extensibility/debugger/debug-engine)
- [Visual Studio Session Debug Manager](https://learn.microsoft.com/visualstudio/extensibility/debugger/session-debug-manager)
- [Visual Studio `IVsDebugger4`](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.shell.interop.ivsdebugger4)
- [Visual Studio `VsDebugTargetInfo4`](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.shell.interop.vsdebugtargetinfo4)
- [Visual Studio JavaScript and TypeScript debugging](https://learn.microsoft.com/visualstudio/javascript/debug-nodejs)
- [NuGet MSBuild props and targets](https://learn.microsoft.com/nuget/concepts/msbuild-props-and-targets)
- [.NET Project System Fast Up-to-Date Check](https://github.com/dotnet/project-system/blob/main/docs/up-to-date-check.md)
- [Roslyn `Compilation.EmitDifference`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.compilation.emitdifference)
- [Visual Studio supported C# Edit and Continue changes](https://learn.microsoft.com/visualstudio/debugger/supported-code-changes-csharp)
- [Visual Studio Hot Reload workflow](https://learn.microsoft.com/visualstudio/debugger/hot-reload)
- [Chrome DevTools Protocol Debugger domain](https://chromedevtools.github.io/devtools-protocol/tot/Debugger/)
- [Chrome DevTools Protocol Runtime domain](https://chromedevtools.github.io/devtools-protocol/tot/Runtime/)
- [Chrome DevTools Protocol Target domain](https://chromedevtools.github.io/devtools-protocol/tot/Target/)
- [Chrome DevTools Protocol version guidance](https://chromedevtools.github.io/devtools-protocol/)
- [Chrome remote-debugging profile requirements](https://developer.chrome.com/blog/remote-debugging-port)
- [MDN: Using Web Workers](https://developer.mozilla.org/docs/Web/API/Web_Workers_API/Using_web_workers)
