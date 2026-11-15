# Lessons Learnt

This file is durable engineering memory, not a work log or a second design
specification. Keep a lesson here when it records a surprising CLR, CLI, BCL,
browser, compiler, or tooling fact; a reasoning failure worth avoiding; a testing
method that changed the quality of the evidence; or collaboration context that
will help future work reach the right result.

Current behavior and detailed subsystem contracts belong in the relevant design
documents. Unfinished work belongs in `TODO.md`, unresolved choices in
`DISCUSSION.md`, and completed implementation history in Git.

## Reasoning and collaboration

- Start from the public .NET contract and the observable application outcome, then
  identify the concrete runtime mechanism. A broad declared type such as
  `Task<Stream>` does not answer which concrete stream .NET returns or where that
  implementation should live.
- When the user challenges a premise, return to the emitted IL, framework source,
  runtime type, or physical test. Do not defend an earlier abstraction after its
  factual basis has changed.
- Distinguish a language/API contract from one implementation's incidental
  machinery. Conversely, do not call an obscure but observable behavior an
  implementation detail merely because it is inconvenient to reproduce.
- Prefer package-neutral compiler mechanisms over package-specific exceptions.
  Concrete type flow for a JavaScript runtime override must come from its C#
  provider body, not a JavaScript annotation or an HTTP/JSON rule in the compiler.
- A runtime-override provider's terminal-type policy is local to that declaration.
  Treating an atomic type such as `System.String` as a global compiler exclusion
  silently defeats another provider that legitimately requests its constructors or
  members. Carry exact exclusions with the access policy through catalog resolution
  and recursive registration.
- Scope is part of correctness. Agree on the smallest end-to-end outcome, explicit
  non-goals, and a completion gate before turning a library or framework name into
  work. Do not silently expand an enabling dependency into a compatibility program.
- Record agreed unfinished work before changing focus. Do not promote a plausible
  idea from discussion into `TODO.md` until its direction is agreed.
- Preserve a useful distinction between facts, observations, and inferences. For
  example, an observed Visual Studio launch path can justify compatibility code;
  it does not justify a claim about Microsoft's intent.
- Favor a small number of strong, composable concepts over local special cases.
  The best corrections in this repository have usually made the compiler more
  faithful to CLR structure while making package code less visible to it.
- Give transported composite data a named class. Record classes are appropriate
  when the composite is a value-semantic key. Tuples obscure the
  contract at method, collection, cache-key, and subsystem boundaries, and an
  unnamed fallback can silently discard otherwise useful element names. Reserve
  deconstruction for framework types or genuinely local control flow.
- Repository null equality uses `== null` and `!= null`. Reserve `is` for type and
  property-pattern matching; do not rewrite null equality as `is null` or
  `is not null`.
- A copied-source component registry has no trustworthy package-install metric for
  individual components. npm downloads measure shared runtime packages, while
  public code search is biased by aliases, private repositories, forks, and indexed
  branches. Use the registry's own dependency in-degree and implementation
  complexity for initial prioritization, and label external search counts as dated
  secondary evidence rather than product telemetry.

## CLI, CLR, and C# lowering

### Identity and generics

- A metadata token identifies a row in one module, not in an assembly or process.
  Persisted identities must include the defining module MVID, and readers must open
  the defining module rather than assuming the manifest module.
- Namespace-qualified type names can collide across assemblies. The runtime does
  not currently carry assembly identity in every key, so compilation must reject
  cross-assembly collisions explicitly rather than invent aliases or precedence.
- Decode a generic method body against its generic definition. Keep `T` and `!!0`
  in shared instructions and carry the recursive closed type tree on calls and
  runtime values; decoding separately for each construction breaks shared method
  identity.
- Generic substitutions compose recursively. An inner `T -> T` must first resolve
  through the ambient map, and nested shapes such as
  `Dictionary<string, List<int?>>` must survive metadata, calls, fields, locals,
  reflection, and runtime wrappers without being flattened to strings.
- Generic definitions, constructed generic types, and nested types are distinct
  metadata states. `IsGeneric`, `GenericTypeArguments`, and a `+T` in a reflected
  name do not mean the same thing.
- Constructed generic types share runtime layout with their generic definition.
  Merge referenced fields at the definition, emit definition-shaped field
  metadata, and carry closed arguments separately. Never let whichever closed
  construction sorts first determine the shared layout.
- An array name is not a type tree. Preserve its element as a recursive type
  reference and carry SZARRAY versus general ARRAY, rank, sizes, and lower bounds
  separately across locals, operands, reflection metadata, runtime handles, and
  companion metadata. In particular, `T[]` and `T[*]` are distinct rank-one CLR
  types even though both have one element dimension.
- Runtime dispatch keys for members of an open generic declaring type retain that
  declaring type's parameters. Concrete substitutions belong in the call context,
  not in a newly manufactured open-type key.
- A by-ref type must be normalized from its element type before generic and
  namespace formatting. Reading `FullName` first can leak assembly-qualified
  generic fragments into an otherwise canonical signature.
- `CustomAttributeData` reports the encoded CLI primitive type for an argument
  even when the attribute constructor declares that parameter as `object`. Use
  the typed argument's `ArgumentType` when selecting signed versus unsigned wire
  encodings; the constructor parameter type is not precise enough.
- `MethodBase.ContainsGenericParameters` can be true for a static constructor on a
  generic declaring type even though `MethodBase.GetGenericArguments()` is not
  valid for that constructor. Require `MethodInfo` before reading method-generic
  arguments.

### Operands, values, and storage

- CLI variable operands are unsigned: `ShortInlineVar` is a byte and `InlineVar`
  is a ushort. Instance argument zero is the encoded `this` slot, which
  `ParameterInfo` cannot represent; subtracting it makes later arguments off by
  one. Validate variable bounds while decoding.
- Byte `0xFE` introduces two-byte opcodes. Reject only a standalone trailing
  `0xFE` as reserved `prefix1`; decode `0xFE xx` before deciding whether the
  resulting opcode is supported.
- Native integers in this runtime are fixed at 32 bits and are not addresses. Keep
  their signed and unsigned stack tags distinct from `Int32`/`UInt32`, and convert
  explicitly at `IntPtr`/`UIntPtr` boundaries.
- Checked unsigned conversions interpret the source's existing 32- or 64-bit
  pattern before range checking. Likewise, `conv.u8` of int32 `-1` yields
  `4294967295`, not `UInt64.MaxValue`; source width matters before JavaScript
  `BigInt` conversion.
- An address is a storage location, not the value found there. Locals, arguments,
  fields, and array elements can use bounded reference cells, but pointer,
  block-memory, typed-reference, and layout operations must fail closed until their
  storage semantics exist.
- `constrained.` is part of the following `callvirt`, not an independent no-op. Keep
  the receiver as a managed reference through dispatch so a value-type implementation
  observes and mutates the original storage, and reject control flow that enters the
  call after bypassing the prefix.
- Before rejecting an address-producing opcode wholesale, inspect supported C#
  lowering. Roslyn uses `ldflda` in ordinary async state machines and for by-ref
  field arguments; a bounded field cell is narrower than a general address space.
- JavaScript property names are not CLR field identities. Base and derived fields,
  including generated backing fields, may share a name. Key storage by declaring
  type and metadata token, retaining the source name only for presentation.
- JavaScript state libraries must observe compiler-owned storage fields without
  replacing the non-configurable readable aliases that preserve CLR field identity.
  Build an explicit annotation map for configurable storage descriptors instead of
  handing the complete managed object to an automatic JavaScript annotator.
- MobX React context distributes an observable store but does not subscribe a
  component or change mutation enforcement. `observer` owns render tracking, while
  `enforceActions` independently controls direct writes. When an application opts
  into `Never`, keep ordinary application updates as direct assignments; retain an
  action API only for consumers with an explicit transactional requirement.
- MobX React's `Observer` render region is a stable React component, not a new
  component type manufactured during each parent render. A managed `Observe`
  helper should create that stable element and pass the render callback as a prop;
  calling `observer` on a newly created component inside render would instead
  change component identity and force remounts.
- Value-type overrides may receive either an ordinary wrapped struct or a managed
  reference to its storage. Dereference before reading or mutating fields, or
  address-form construction updates the reference descriptor instead of the value.
- .NET `null` is JavaScript `null`, never `undefined`. Validation must inspect the
  unwrapped managed payload because `ldnull` can itself be carried in a wrapper.

### Exceptions, async, and initialization

- Exception filters execute during search, before finally blocks unwind. A filter
  that throws is treated as false and search continues.
- Pending `leave`/unwind transfers form a stack. A nested `try/finally` inside an
  executing finally creates another transfer; a single slot loses the outer one.
  A new exception replaces obsolete pending control transfer.
- CLI `fault` uses `endfinally` but runs only for exceptional unwind. Normal
  `leave` must skip it. C# `return`, `break`, and `continue` across protected
  regions all lower to the same region-aware leave mechanism.
- JavaScript exceptions crossing managed code must still participate in managed
  finally unwinding. Interpreter-created managed exceptions should use one
  `DotNetError` carrier whose managed metadata, not JavaScript subclassing,
  determines catch inheritance.
- `Task.Delay(0)` is complete when returned. Scheduling even a zero-delay timer
  changes `IsCompleted`. `ConfigureAwaitOptions.ForceYielding` similarly requires
  a completed awaiter to report incomplete so continuation crosses an asynchronous
  boundary.
- Roslyn emits `IAsyncStateMachine.SetStateMachine`, but the JavaScript task builder
  retains the original managed state-machine reference directly. Verify optimized
  multi-await behavior before adding an override solely because a CLR builder uses
  that hook while boxing.
- Static initialization is once per closed generic type. Mark initialization in
  progress before invoking `.cctor()`, cache success, and cache a single managed
  `TypeInitializationException` on failure rather than rerunning or rethrowing the
  raw inner exception.

## Closed-world compilation and runtime overrides

- When a whole-graph analysis both validates invariants and returns the selected
  set, call it once and retain its result. A validation wrapper that discards the
  result before an immediate result-producing call can double seconds and gigabytes
  without adding correctness. Use phase allocation evidence plus exact graph and
  emitted-artifact hashes before removing such a pass.
- Treat compiler payload contents as an immutable process-level inventory. Repeatedly
  formatting reflection-heavy method identities and probing for the same absent file
  can dominate compilation even though every individual `File.Exists` looks cheap;
  check an empty inventory before constructing the identity.
- Keep companion runtime assets behind an owner-qualified generic transport contract.
  The compiler hub must not name spoke files, maintain a per-file cleanup inventory,
  or flatten companion-owned modules into its own runtime namespace.
- Companion selection is not feature reachability. Start from used override modules
  and transport only their transitive static-import closure; otherwise optional
  helpers such as reflection silently become a fixed cost of selecting a broad pack.
- Cache runtime-override resolution at the immutable collection boundary rather than
  caching reflection primitives independently. Retaining the resolved entry, matched
  canonical signature, and negative result for an exact method removes repeated name
  normalization and parameter reflection together. Keep the cache collection-scoped,
  and bypass it for a process-wide empty singleton so collectible load contexts are
  not rooted by arbitrary method keys.
- Missing BCL behavior is not permission to write JavaScript. Inspect the method's
  IL first. Compile usable MSIL; use an approved environment/runtime abstraction
  only where necessary; otherwise fail closed.
- A public property-bag escape hatch does not require selecting the complete BCL
  `Dictionary<TKey, TValue>` implementation for browser output. When the product
  contract needs only indexed lookup, enumeration, removal, and clearing, a focused
  managed collection avoids accidentally turning comparer, hashing, cryptography,
  and unrelated CoreLib intrinsics into part of the compatibility scope.
- JavaScript adapters must recognize both native `null` and a wrapped managed null
  before invoking retained managed instance methods. React commonly supplies null
  host props, and treating the wrapper itself as a live receiver converts an
  ordinary absent-props case into a managed `NullReferenceException`.
- An abstract or interface method intentionally has no body; that alone does not
  make it a missing mandatory intrinsic. Its contract participates in dispatch,
  while the selected concrete implementation supplies executable behavior.
- Runtime-override companions are declarative, untrusted inputs. Inspect provider
  metadata without executing attributes, read embedded resources directly, and
  reject mismatches among typed providers, canonical signatures, hashes,
  manifests, resources, and JavaScript registrations.
- A companion has no silent precedence. Select companions through the target
  application's dependency graph and reject duplicate signatures across selected
  packs.
- Provider IL is the managed reachability declaration. JavaScript imports,
  comments, and dynamic calls cannot tell the compiler which managed types,
  constructors, accessors, enumerators, or virtual members to retain.
- Reflection-like package companions need executable custom-attribute identity as
  well as serialized values. Record the exact attribute constructor and argument
  types, and root only application attribute constructors, named setters, and
  declared callbacks requested by a package-neutral provider contract.
- An explicit runtime override replaces the target method's execution semantics.
  Decode its provider body for metadata and dependencies without rejecting
  unsupported runtime behavior on a path that cannot execute, but continue to
  reject malformed IL and reserved encodings.
- Resolve provider dependencies in the selected application's assembly load
  context and preserve defining assembly/module identity. Same-named compiler-host
  assemblies can be a different package revision even when CLR assembly versions
  match.
- A companion-owned concrete implementation type must load in the application's
  load context so it shares the application's framework types. Merely finding the
  companion in the compiler host can create an incompatible type universe.
- An asynchronous override can return concrete values through a broad `Task<T>`
  contract. Summarize only values that flow into the returned task in the C#
  provider body, preserve nested constructed type references recursively, carry
  them through task/awaiter flow, and register even zero-field result types in
  emitted metadata.
- Runtime overrides still have ordinary successful and exceptional control-flow
  successors. Modeling their return value must not bypass the merge into later
  application instructions.
- Provider `ref`/`out` effects are storage effects, not return types. Summarize only
  writes reaching normal returns, compose summaries through ordinary managed
  helpers, and apply them to the caller's actual local, argument, field, or array
  location. `out` is a strong replacement; `ref` retains its incoming value set.
- A static generic runtime override cannot obtain metadata for a result type from
  a value parameter when the method receives only text. Put the package-neutral
  type-access request on the provider's generic parameter and preserve it through
  generated provider configuration instead of teaching the compiler about a
  particular API such as enum parsing.
- An override that owns a companion-native value representation must construct
  that same representation on every path. In particular, unsuccessful `TryParse`
  must write the concrete managed default to its `out` location rather than rely
  on a JavaScript default that happens to be falsey or zero-like.
- CLR arrays implement generic enumeration through runtime-supplied methods that
  reflection does not expose with `GetInterfaceMap`. Model that dispatch explicitly
  at the array/runtime boundary and retain the interface contracts instead of
  pulling the internal CoreLib array-enumerator implementation into the browser
  call graph.
- Possible-type analysis must distinguish unreachable, reachable null-only, and
  reachable unknown receiver sites. Only the last is an unresolved dispatch error.
- Allocation-sensitive flow must carry site identity through arguments, returns,
  fields, arrays, and merges. Keep uncertainty attached to the type that introduced
  it; one unknown sealed value must not taint unrelated proven allocations.
- Dependency discovery and emission are symmetric. If emitted code imports a type
  or member, the call graph must retain it even when a branch returns or throws
  before runtime use.
- Exact enumerable dependencies include the concrete `GetEnumerator`, `MoveNext`,
  `Current`, and `Dispose` implementations. Referencing only `IEnumerable<T>` or
  globally rooting non-generic enumeration can respectively prune a struct
  enumerator or explode the closed-world graph.
- Explicit-interface member identity includes the declaring interface and full
  metadata name. Collapsing it to the short method name creates false collisions
  during provider generation and can dispatch an unrelated interface contract at
  runtime.
- JavaScript materializers still need typed provider dependencies for every managed
  operation they perform indirectly. Declare construction, enumeration, and
  mutation contracts in provider IL instead of relying on a native array or object
  to make the corresponding managed members reachable.
- Managed collection state is shared across the selected public surface. If an
  override constructs or mutates a collection, every supported observation path
  such as enumeration and `ToArray` must bridge the same state.
- A third-party package vertical slice is a useful compiler/runtime probe. Keep the
  package's usable MSIL intact, fix general CLR and framework primitives at their
  owning boundary, and reserve the package companion for a narrow environment
  adaptation rather than rewriting package policy.
- Framework adaptations belong to the companion for the defining assembly. A
  browser-safe `System.Uri` implementation is not an HTTP rule and never a compiler
  special case.
- A low-level BCL abstraction may merit a package-neutral implementation even when
  public methods contain IL if that IL depends on CLR intrinsics, private layout, or
  RVA data. Such exceptions require an explicit scope decision and one coherent
  managed contract; they must not hide inside a serializer-specific companion.
- Fixed-length hash filenames avoid Windows path failures for long generic
  signatures. Keep the readable signature in the manifest and validate the hash;
  readability is metadata, not a reason to put a complete signature in a path.

## Runtime and transport boundaries

- JavaScript numbers, strings, arrays, regular expressions, truthiness, and
  coercions are not substitutes for their .NET counterparts. Use emitted managed
  behavior or implement the exact selected contract explicitly.
- A current-time getter must derive its UTC ticks and local offset from one host
  clock sample. Sampling them independently can straddle a daylight-saving or date
  boundary and produce a `DateTime` or `DateTimeOffset` that never represented a
  real instant.
- Formatted interpolation lowers through
  `DefaultInterpolatedStringHandler.AppendFormatted<T>(T, string)` and
  `IFormattable.ToString(string, IFormatProvider)`, while an unformatted hole uses
  `ToString()`. A handler override must declare and dispatch those exact managed
  dependencies; JavaScript `String(value)` both loses managed struct semantics and
  lets an interpolation-only program prune the formatter it needs.
- Generic interpolation must select formatting from the method's concrete `T`.
  Small integral values can be widened on the evaluation stack, but their `X` and
  `B` formats still use the original type width. Process-based parity tests must
  also force UTF-8 pipe decoding so invariant symbols such as `¤` and `‰` are
  compared as managed text rather than the host console code page.
- Interpolation fallback is virtual dispatch, not an exact method-table lookup.
  Walk inherited `ToString()` overrides and use `Object.ToString()` for a class with
  no override; when a format is supplied, call the formatted overload only for an
  `IFormattable` value and otherwise ignore the format as .NET does. Keep exact
  intrinsic lookup for widened value-type stack representations because trimmed
  framework metadata may not retain enough interface information to rediscover the
  original formatter.
- Temporal formatting is a family of related but distinct contracts. Reuse token
  rendering only after applying each type's standard-format map and validation:
  `DateOnly` rejects time tokens, `TimeOnly` rejects date tokens,
  `DateTimeOffset` owns an explicit offset and different universal conversions,
  and `TimeSpan` has a separate duration grammar. Preserve ticks as integers until
  the final seven-digit fraction is rendered; JavaScript millisecond dates cannot
  recover that precision.
- Floating-point formatting has two precision contracts: shortest round-trip text
  uses 5/9/17 digits for `Half`/`Single`/`Double`, while custom formats intentionally
  retain 5/7/15 digits for compatibility. IEEE midpoint output uses ties-to-even,
  unlike decimal-buffer midpoint formatting, so one shared numeric grammar still
  needs representation-specific digit generation and rounding policy.
- Enum formatting is metadata-driven even though its CLI evaluation-stack value is
  integral. Preserve the concrete type supplied by `constrained.` and `box`; otherwise
  dispatch selects an integral formatter or a direct `Enum.ToString(format)` call sees
  only `System.Object`. Receiver-side runtime-override metadata access uses target
  parameter `-1`, while generic interpolation must independently request enum metadata
  from its value parameter because a provider dependency body has no runtime call-site
  receiver to analyze.
- .NET enum names are sorted by their unsigned backing representation. General exact
  lookup uses the first matching alias, while flags exact lookup walks from largest to
  smallest and therefore selects the last matching alias; multi-name flags output then
  reverses the greedy matches into ascending order.
- Call-scoped interpreter state must be installed immediately before dispatch and
  restored in `finally`, including when an override throws. Allocating a fresh
  context per instruction can conceal state leaks without defining the right
  lifetime.
- Keep binary transport separate from interpreter semantics. Normalize operands
  once, use versioned numeric opcodes and typed values, and make every reader
  reconstruct the established instruction-block model.
- Managed strings are sequences of UTF-16 code units, including isolated
  surrogates. Canonical WTF-8 preserves that domain; ordinary UTF-8 encoders can
  replace or reject it. Decode only after reconstructing a complete front-coded
  entry and reject malformed or noncanonical encodings in every reader.
- Browser request policy remains browser policy. Fetch owns CORS, forbidden
  headers, cookies, redirects, cache behavior, and normalized response headers.
  Typed adapter options may expose selected policy, but managed collection state is
  not proof of what Chromium actually sent.
- `ResponseHeadersRead` is a streaming contract. Preserve the browser reader and
  unread suffixes when a managed read consumes less than one browser chunk. A
  timing-only test cannot distinguish streaming from fast whole-body buffering.
- Streaming and buffered HTTP paths may share the public `Stream` contract while
  returning different concrete types. Virtual dispatch and metadata retention must
  follow those concrete values rather than attaching browser state to an abstract
  `Stream` object.

## Serialization lessons

Detailed supported behavior belongs in [`features/JSON.md`](features/JSON.md). The durable lessons are about
where serializer behavior lives and which distinctions are easy to erase.

- Serializer internals are not prerequisites for browser JSON. Desktop metadata
  caches, spans, pools, synchronization, and address-taking code can be much larger
  than the selected public contract. Keep library behavior in selected companions
  and generalize the compiler only for independently justified CLR capabilities.
- Newtonsoft.Json and System.Text.Json are separate contracts. Do not create a
  shared JavaScript coercion policy for numbers, enums, required members, nulls,
  constructors, naming, diagnostics, or escaping simply because both consume JSON.
- Preserve lexical information until typed conversion is complete. Exact integral
  text, floating-point token spelling, source offsets, duplicate/unknown member
  positions, and UTF-16 code units cannot be reconstructed reliably from a native
  JavaScript value after parsing.
- Absence, explicit zero/default, and explicit null are often three distinct
  metadata states. Do not collapse them with truthiness or null-coalescing when
  processing serializer settings or attributes.
- Build and validate the effective member contract before invoking constructors,
  getters, or setters. Name collisions, required/unknown-member policy, ordering,
  and inclusion rules can be observable even for empty input.
- Constructor and member reachability is package-neutral metadata demand. The
  compiler retains the requested constructors/accessors and their reflection data;
  each companion owns library-specific selection, binding, and conversion.
- Reflection access requested by a provider must recurse through selected
  application-owned reference-type properties and fields. Keep framework member
  types behind their explicit provider dependencies, while still following generic
  application arguments; applying POCO policy directly to framework types can pull
  unsupported CoreLib implementation machinery into the graph.
- Native `JSON.parse`, `JSON.stringify`, `Number`, `Boolean`, and `String` are
  useful primitives only behind parity tests. Their numeric precision, coercion,
  escaping, property order, and error behavior are not the .NET serializer contract.
- Source normalization for comments, trailing separators, or nonstandard numeric
  tokens must preserve length and line breaks when diagnostics depend on offsets.
  Never use global replacement that can modify quoted property names or strings.
- Cycles require ancestry, not a global visited set. Sibling references can be
  legal while an active ancestor must be rejected or substituted according to the
  selected library policy.
- Reflection demand for enums, fields, properties, and constructors is different.
  Avoid routing enum metadata through general member discovery or recursively
  treating framework/primitive array elements as POCOs.

## Debugger and editor integration

Detailed debugger architecture and accepted behavior belong in
[`debugger/ARCHITECTURE.md`](debugger/ARCHITECTURE.md) and
[`debugger/DEBUGGING_THE_DEBUGGER.md`](debugger/DEBUGGING_THE_DEBUGGER.md); FECSX behavior belongs in
[`features/FECSX.md`](features/FECSX.md).

- A JavaScript stack after an `await` cannot recover its managed callers. Snapshot
  immutable managed frame identities and values when registering the continuation;
  keep logical async parents out of physical step-depth accounting except where
  logical depth is explicitly required.
- For live synchronous execution, the interpreter context owns the managed stack.
  Do not reconstruct it from paged JavaScript-adapter frames. Use adapter frames
  only for the surrounding native JavaScript stack.
- Debugger projections are views, not storage. Every writable projected local,
  argument, field, property alias, or array element must retain a live runtime
  target. Scope object references to one suspension and clear them on resume.
- DAP display text is not an object transport. `evaluate.result` can be truncated
  and cannot make a value expandable. Preserve live `variablesReference` values and
  let the adapter own paging, formatting, and remote-object lifetime.
- Managed and native stops can coexist in one JavaScript VM. Require explicit
  managed stop identity before relabeling, resuming, stepping, or clearing state;
  unrelated native stops must pass through unchanged.
- A managed breakpoint, exception, or pause supersedes an outstanding managed step.
  Clear the old plan or it can fire later at an unrelated sequence point.
- Source maps map static generated locations; they do not synthesize managed stack,
  locals, exceptions, or evaluation for a shared interpreter instruction. A
  checksum-valid `.cs` source map also does not make Visual Studio route an existing
  C# breakpoint to the JavaScript engine.
- Bundled `sourcesContent` is recursive debugger input. Vendor source-map comments
  embedded there can make js-debug probe hundreds of maps that the bundle neither
  publishes nor needs. Mask only vendor directives, preserve every character
  position and line ending, and retain application directives so the outer bundle
  map stays exact without silently changing application debugging policy.
- A managed interpreter debugger needs one stable mapped execution anchor, not a
  map of the complete runtime and vendor graph. Select mapped modules before webpack
  builds VLQ mappings, retain authored runtime assets and build identity, and keep a
  full-map mode explicit for compiler/runtime development.
- Debugger ownership requires exact source, build, and runtime identity. Never pick
  the newest output, first tab, first process, matching URL alone, or filename alone
  when multiple bundles, browsers, or path aliases can exist.
- Portable PDB document tables include generated documents. Select executable
  sequence points deliberately, keep generated plumbing hidden, and verify the PDB
  document rather than trusting generated `#line` source text. Windows `#line`
  paths must not double their native separators.
- Metadata for async locals lives in state-machine fields and Portable PDB hoisted
  scopes, not only the `MoveNext` local array. Hide compiler awaiter/spill fields
  while projecting source locals.
- Visual Studio services and workspaces are often unavailable during early package
  or editor construction. Resolve external services lazily, retry after workspace
  changes, and give immediate lexical behavior a cancellation lifetime independent
  of slower semantic classification.
- Visual Studio async completion filters the source set captured at the session's
  first character; it does not necessarily ask the provider for a new catalog on
  every subsequent character, and erasing the token does not necessarily end the
  session. Seed a one-letter Tailwind session with the complete grouped surface—
  exact standalone utilities, functional family roots, and variants—so changing
  `p-` to `blo` can find `block`; omitted exact continuations cannot reappear. The
  grouped surface avoids carrying every numeric and color value before its family
  is selected. Keep descriptions lazy because formatting a complete family tooltip
  for every candidate would make the correct session behavior unusably slow.
- A complete initial catalog still cannot model syntax whose candidates depend on
  punctuation typed later in the session. For FECSX Tailwind slash modifiers and
  arbitrary `[]`/`()` values, dismiss and retrigger the active async-completion
  session at grammar transitions so the source sees the current token. Do not
  replace Visual Studio's one-per-view completion item manager merely to obtain
  dynamic items; doing so would also take ownership of Roslyn filtering and
  selection for every FECSX completion context. An auto-closed arbitrary template
  remains syntactically closed while the user types inside it, so “open delimiter”
  detection is insufficient: refresh on edits anywhere within the arbitrary token.
  If parameter help depends on position, retain the caret's token-relative offset
  and refresh an already-open session on caret movement as well.
- A custom image moniker can be present in a correctly packaged image manifest yet
  render as a blank completion icon when the image service cannot locate the
  assembly containing its XAML resource. Register that assembly's runtime codebase
  in the VSIX pkgdef and declare the intended source size; suffix text is not proof
  that the completion icon resolved. Image-service XAML must also be valid WPF:
  SVG attributes that look familiar are not necessarily WPF shape properties
  (`FillRule` on `Path` caused the Tailwind resource to fail loading entirely).
- An empty arbitrary-value preview such as `drop-shadow()` explains no useful
  syntax. Show a concrete bracketed example, keep the committed template editable,
  and state the authored-to-CSS mapping explicitly (for Tailwind arbitrary values,
  underscores become spaces). Parameter help must follow the actual CSS function
  grammar: `drop-shadow()` accepts horizontal offset, vertical offset, optional
  non-negative Gaussian-blur standard deviation, and color; spread distance and
  `inset` are box-shadow concepts and must not be advertised for filter drop
  shadows.
- Visual Studio's project security warning may default “Do this for every project
  in the current solution” to checked. Physical harnesses that intentionally accept
  a trusted project must clear that option before Open; applying the decision to a
  large solution can turn a focused editor check into a project-by-project load.
- A component has one identity: its declared C# member. The filename is a
  convention, not a second export name. JSX structure, closing tags, classification,
  braces, and completion should share the recovered FECSX tree rather than grow
  independent scanners.
- Do not compile the build-time FECSX generator and an editor projection that
  reproduces the same declaration in one temporary compilation. Remove only that
  generator, retain other analyzers, and keep mapped authored segments for rename,
  navigation, and diagnostics.
- Visual Studio can expose a project analyzer with an opaque path, display name,
  and ID even though Roslyn can load its generators. Generator removal must inspect
  the loaded generator identity as a final fallback; incremental generators arrive
  behind Roslyn's wrapper rather than as their declared implementation type.
- FECSX component tags occupy markup spans that are intentionally absent from the
  ordinary C# projection. Resolve Go To Definition through a minimal import-and-
  namespace probe against the generator-enabled project, then map the resolved
  facade/nested-facade symbol back to the authored component declaration.
- Roslyn completion can contain multiple edits, and rename can touch both authored
  and projection-only text. Map the complete edit set, apply edits wholly inside
  authored segments, ignore wholly synthetic edits, and reject edits crossing a
  mapping boundary.
- Optional generated value properties should track assignment explicitly when
  omission differs from `false` or zero. `Nullable<T>` is not automatically the
  best authoring/runtime representation when it materially expands generic reachability.
- JSX text has separate authored and semantic forms. Preserve raw text and exact
  spans in the parser for editor operations, then apply JSX line trimming and
  entity decoding during lowering. Single-line whitespace is significant while
  multiline indentation-only text is omitted; quoted attributes decode entities
  without applying child-text whitespace cleanup.
- Hybrid FECSX declaration discovery must respect top-level C# brace depth. A
  broader method-body recognizer can otherwise mistake an ordinary method inside
  a preamble type for the first component. Preserve the selected body form in the
  syntax tree so generated Roslyn projections remain parseable for both arrow and
  block-bodied components.
- An embedded TextMate grammar can share a delimiter with its containing rule,
  but `applyEndPatternLast` is not a substitute for a child grammar that actually
  emits nested scopes. Visual Studio's `source.cs` include contributes no child
  lexical states inside FECSX, so the FECSX seam must protect C# strings, comments,
  characters, and recursively balanced braces itself. Prove the result by reading
  installed `ITMScopeTag` spans for known boundary fixtures. Screenshots and JSON
  property checks are not classification evidence.
- Do not put a string scope on the complete begin/end range of an interpolated
  string. Scope only its delimiters and literal segments; a balanced interpolation
  hole must not inherit `string.*`, or that lexical parent can override the Roslyn
  classifications of parameters and member accesses inside the hole. Compare the
  final aggregated classification set with an ordinary-expression baseline rather
  than inferring the result from the absence of one bad scope.
- Visual Studio integration is proven in the installed experimental hive, not by a
  valid VSIX archive or registry file alone. Verify effective registration, loaded
  assemblies, rendered editor behavior, and the actual F5 lifetime.
- For project-aware editor verification, launch Visual Studio with the solution,
  wait for DTE and solution readiness, and only then open the authored document
  through `ItemOperations`. A startup `/Command File.OpenFile` can race project
  loading and leave the document on a standalone fallback path.
- Keep claims about unsupported Visual Studio composition paths bounded to the
  installed version and evidence. Once a public integration experiment reaches a
  stable negative result, preserve the conclusion and remove speculative harnesses
  rather than turning evidence tooling into product architecture.

## Testing and evidence

- Test the observable boundary, not a nearby proxy. For HTTP, assert the controlled
  server's received method, headers, and exact bytes. For streaming, withhold a
  later chunk until managed code reports the first. For editors, inspect the real
  rendered surface. For debugger composition, exercise every claimed domain in one
  lifetime.
- Pair focused semantic tests with one bounded real vertical slice. Pure tests make
  edge cases cheap; Chromium, Visual Studio, packaging, and separate-process tests
  catch environment and composition failures that mocks conceal.
- Reproduce the exact control-flow shape that exposed a bug. A simple finally test
  cannot prove nested pending transfers, and a successful Fetch cannot prove the
  server observed the requested bytes.
- A broad call-graph assertion can pass because the fixture's own verification code
  directly calls the member it claims metadata policy retained. Keep reachability
  fixtures focused and do not consume the asserted member on an ordinary managed
  path; otherwise the test is tautological.
- Use negative controls. Reproduce suspected upstream behavior in an unrelated
  project, prove wrong build identity yields no debugger match, and prove an
  unrelated allocation does not enter a provider result summary.
- Do not weaken, skip, or replace an assertion merely to get green. If a broad test
  is too expensive, record that explicitly and replace it only with focused coverage
  that proves the same contract.
- A disabled smoke must remain visibly excluded by the test/workflow layer. Never
  put an unconditional successful exit inside the smoke itself: an explicit manual
  invocation must either exercise the contract or fail, not masquerade as evidence.
- Test duration is part of the regression contract. Separate test-body time from
  host/build/browser/process startup, share immutable expensive setup when safe, and
  keep routine browser fixtures to representative vertical slices.
- Repository-wide source scans must exclude ignored vendor and generated directories
  such as `node_modules`, `bin`, and `obj` before reading file contents. Otherwise
  installed packages and accumulated build output become hidden test inputs and can
  turn a small ownership assertion into a timeout.
- A child process can exit while a reusable MSBuild node still owns its redirected
  output handles. Bound stream completion as part of the process deadline and pass
  `-nodeReuse:false` to nested `dotnet build` or `dotnet pack` verification commands;
  otherwise a nominally bounded integration gate can wait forever after exit.
- Source ownership and execution hosting are separate decisions. Keep a
  package-specific fixture and browser gate beside its runtime-override package,
  while identical companion-contract assertions can share one NUnit host to avoid
  repeated process startup.
- Keep the slow-test threshold distinct from the failure ceiling. The code-owned
  NUnit policy reports every case at or above five seconds as slow and fails every
  case that reaches the 10-second maximum.
- Bounded fixture parallelism can shorten feedback without weakening tests. Keep
  methods within each fixture serialized, use one fixture instance per case, and
  put the few compiler-heavy cases that approach the ceiling under contention on
  NUnit's exclusive queue. Parallel timings include resource contention, so compare
  the complete-suite wall interval as well as individual-case durations.
- Timeouts in normally small compiler tests often indicate an infinite loop or
  uncontrolled dependency expansion. Inspect the graph and emitted program before
  increasing the timeout.
- A browser smoke can complete successfully while Chromium is still closing
  speculative or duplicate static-file connections. Test-owned servers must await
  request tasks during disposal and treat cancellation-time socket aborts as normal
  cleanup, without suppressing assertion or compilation failures from the test body.
- Public browser override adapters must terminate before desktop orchestration
  helpers. Overriding only an internal transport method can leave public overloads
  pulling a large, irrelevant desktop dependency graph into the emitted program.
- Keep routine browser compilation fixtures to one representative vertical slice.
  A broad asynchronous matrix multiplies possible-type analysis and can duplicate
  faster focused coverage while hiding a dependency-expansion regression behind a
  long compiler phase.
- Chromium virtual time is unsuitable for request-timeout tests because
  `AbortSignal.timeout` measures active time and network waits can pause virtual
  time. Use a real-time controlled browser harness and an application-owned
  completion marker.
- The implementation language does not dictate the harness language. C# can own
  Node.js, Chromium, Visual Studio, or browser-app lifecycle and assertions; keep
  JavaScript for product/runtime behavior and use PowerShell only where a Windows
  host genuinely requires it.
- Exercise tooling under the actual host. Windows PowerShell uses .NET Framework,
  Visual Studio has real package/service timing, and an in-process compiler test can
  accidentally succeed because the test host already loaded a dependency.
- Treat caches and generated artifacts as suspects. Refresh copied companion DLLs,
  force restore stale assets when needed, and verify the output actually served by
  the browser before interpreting a failure as product semantics.
- Preserve operator state. Snapshot and restore IDE selections and breakpoints,
  never discard unsaved buffers, touch only owned processes and temporary profiles,
  and fail closed when cleanup targets are ambiguous.
- Visual Studio UI Automation can retain offscreen completion elements, omit an
  automation ID, and append positional text such as `. 1 of 1` to the accessible
  name. Match only live onscreen items, treat ID and normalized name as independent
  evidence, and reopen the scenario-specific popup while waiting for project-system
  readiness. Use DTE text edits for deterministic verifier setup; reserve physical
  UI input for the editor behavior being proven.
- A missing `VsRegEdit` value is unavailable registration evidence, not proof that
  VSIX installation failed. Bound the known read failure, then verify the installed
  package definition, image manifest, and MEF catalog before exercising the real
  editor surface.
- Use the current local checkout path for repository tooling and prefer
  repository-relative user-facing commands. Code may normalize a mapped drive's
  backing UNC alias returned by Windows for identity checks, but direct UNC
  invocation is not a supported workflow.

## Build, packaging, and publication

- Source links are not an assembly boundary. When production hosts or reusable
  test infrastructure compile the same C# file, put the code in a shared project
  and use ordinary references; this preserves one type identity and one validation
  policy. Retain a `<Compile Link>` only when compiling the exact source into a
  separate host is itself the test, document it beside the item, and enforce the
  exception with a repository test.
- Do not propagate transitive compiler payloads with post-build `rmdir`/`XCOPY`
  steps. Declare them as MSBuild content, generate derived payloads beneath the
  producing project's `IntermediateOutputPath` before target-path assignment, and
  let MSBuild copy them to each consumer without shared-directory deletion races.
- An incremental MSBuild target's body does not run when `Inputs` and `Outputs` are
  up to date, so items declared only inside that body disappear from the evaluated
  copy/package graph. Keep generation incremental, but register its existing output
  from a separate always-running target before target-path assignment. Use a
  dedicated intermediate item with qualified metadata when translating generated
  files into `Content`; unqualified metadata can batch against unrelated existing
  content and assign generated bytes to the wrong target paths.
- Source and generated output can look alike. Confirm the source path before
  editing; generated demo/test output is disposable and will be overwritten.
- SDK default globs cross nested project directories. Explicitly exclude a nested
  independent project tree from its parent; solution membership is not a compile
  boundary.
- When adding a direct `ProjectReference`, add the referenced project and its tests
  to the solution, including references with `ReferenceOutputAssembly="false"`.
  That metadata suppresses a compiler reference; it does not remove the project
  from Visual Studio's restore/build graph. Command-line graph restore can succeed
  while Visual Studio solution restore fails for an absent or unloaded project.
- `MSBuildWorkspace` project references can be `CompilationReference` objects.
  Staging runnable output must copy the built artifact from `OutputFilePath` so
  MSBuild-provided embedded resources are preserved.
- Two lexical paths can name one checkout. Since NuGet writes absolute project paths
  into `obj`, drive/UNC aliases sharing an intermediate directory can corrupt each
  other's restore graph. Derive intermediate roots from a stable hash of the
  lexical repository root rather than special-casing drive letters. Put each
  project's intermediates under a short repository-level project hash as well;
  appending the root hash beneath a deeply nested project's own `obj` directory can
  push ordinary MSBuild outputs past the legacy Windows `MAX_PATH` boundary.
- After changing an intermediate root, Visual Studio's fast up-to-date check can
  skip a template project because its `bin` output exists even though its generated
  VSSDK template archive does not exist at the new path. A template-bearing VSIX
  must materialize those archives before `AssignProjectConfiguration` resolves and
  extracts `TemplateProjectOutputGroup`; relying only on the solution dependency is
  not sufficient.
- ASP.NET development static-web-asset manifests also embed absolute source paths
  in final web-host output. If one checkout is built through multiple lexical roots,
  delete the host's generated runtime and endpoint manifests before `PrepareForBuild`
  so the current root's isolated intermediate manifest is copied into `bin`. Otherwise
  Visual Studio can load a valid but unreachable manifest from another drive or share
  alias.
- Include companion archives with repository-relative MSBuild items. A rooted
  `EmbeddedResource Remove` under `obj` makes assembly contents machine-dependent.
- Build browser output in a disposable staging directory and publish only after a
  successful bundle. Preserve unrelated destination files and remap source-map
  names to the stable published namespace before deleting staging.
- Cache-busting only an ES-module entry does not invalidate its static imports.
  Bundle the initial graph behind one cache-busted entry and keep the binary package
  version coupled to that graph.
- Incremental generation must track runtime payload changes, not only
  `Compiler.dll`. Browser assets created during build must exist before static web
  asset discovery or a first build can omit newly generated files.
- A runtime module's source ownership and its output location are separate
  contracts. Put a single-owner module in the companion that defines the managed
  behavior, validate and publish it only when that companion is selected, and keep
  cross-companion helpers compiler-owned until dependencies between companion
  assets are explicit. Browser fixtures must use project references to companions;
  raw DLL hint paths can copy an older embedded asset while the producing project
  is still being rebuilt.
- MSBuild `Inputs`/`Outputs` timestamps are not a complete browser-compilation
  cache key: they miss property-only configuration changes, deleted public outputs,
  and content changes whose timestamps are preserved. Fingerprint the deployed
  assembly/PDB/manifest set and complete tool payload by content, include the
  effective compiler arguments, require every published output, and commit the
  candidate state only after staged publication succeeds.
- Long paths and randomized VSIX directories are architectural constraints. Use
  short content-addressed extraction roots, fixed-length override filenames, and a
  `buildTransitive` package for project integration rather than importing from an
  installed extension directory.
- Application reflection loading must follow the dependency manifest. Use a
  dedicated `AssemblyLoadContext` with `AssemblyDependencyResolver` and a bounded
  same-directory fallback; `Assembly.LoadFile` alone may fail only when a method
  body later resolves a copied dependency.
- A zero process exit code is not always completion evidence for Visual Studio
  maintenance. Use the console host where required, wait for installer children,
  and verify the effective CLSID/assembly or editor behavior rather than only files
  on disk.
- A raw project-template ZIP can be present in an installed VSIX yet remain absent
  from the modern New Project dialog. Package templates through
  `TemplateProjectOutputGroup` under the `ProjectTemplates` VSIX subpath so full
  Visual Studio MSBuild generates the `.vstman`; `dotnet build` cannot run that
  manifest generator. Prove the result through the physical dialog, not only by
  inspecting or expanding the archive.
- A VSSDK project whose `Microsoft.VsSDK.targets` import depends on a
  PackageReference must be evaluable before that package has been restored.
  Condition the import on the target file's existence, but do not assume Visual
  Studio's Build Solution command will restore a newly added project first. Resolve
  the exact package version from generated NuGet metadata or the configured/default
  global package cache so an already downloaded build-tools package works on first
  evaluation. If the package is genuinely absent, fail from an ordinary build
  target with an actionable restore diagnostic. A template-only utility project
  that compiles no product code should use the classic VSSDK project shape and stay
  NuGet-free; an SDK-style project still requires `project.assets.json` before
  Build even when the required package is already present in the global cache.
  Also initialize the package's VSSDK tool environment before
  `GenerateTemplatesManifest`: the package's default initialization before
  `CreateVsixContainer` is too late for that dependency, and a project without an
  earlier VSCT compilation can otherwise resolve `VsixUtil.exe` from a stale
  machine-wide SDK path.
- Multi-project templates have three independent folder owners: the New Project
  wizard, the root `ProjectGroup`, and each linked project. Do not let the root
  create another folder, create linked projects in place, and do not repeat a
  nested source directory in `TargetFileName`; otherwise projects or content land
  in duplicated directories. Use short acceptance names because SDK intermediate
  paths can still hit the Windows 260-character limit.
- Compiler-generated async iterators implement both generic and non-generic
  `IValueTaskSource` contracts with identical simple method signatures. Explicit-interface
  runtime naming must distinguish the interface's generic arity; matching only the
  namespace-qualified base name can collapse those methods into one identity.
- A runtime-override constructor that defers work to an interface source must declare
  those managed calls in its provider body. JavaScript can invoke the source later, but
  without that managed dependency the call graph correctly trims the generated explicit
  interface methods and the runtime has nothing to dispatch.
- When a JavaScript runtime adaptation caches a managed factory failure, cache the
  wrapped .NET exception rather than the transient JavaScript bridge error. Re-throwing
  the wrapper preserves managed exception identity and catch behavior on every later
  access while still allowing non-managed runtime defects to remain uncached.
- A seeded browser PRNG needs an explicitly unsigned fixed-width state transition;
  ordinary JavaScript numbers alone do not preserve 32-bit overflow semantics.
  Use `Math.imul` plus unsigned normalization, and use rejection sampling for
  bounded results so deterministic repeatability does not introduce modulo bias.
- Implement periodic browser timers with recursive `setTimeout` scheduling and a
  generation token rather than `setInterval`. A callback can call `Change` or
  `Dispose`; checking the captured generation after invocation prevents the old
  schedule from creating a second cadence or reviving a disposed timer.
- Cancellation-backed event-loop operations need one terminal transition and an
  explicit callback-unregistration path. Check pre-cancellation before scheduling,
  clear the host timeout when cancellation wins, and unregister the cancellation
  callback when normal completion wins so a completed Task is not retained by its
  source.
- Framework calls to an overridden virtual member can be encoded against its base
  slot even when the source receiver is a concrete derived type. When adapting a
  public browser boundary such as byte-array `Stream.WriteAsync`, inspect the
  emitted call graph and provide a base-slot dispatcher alongside the concrete
  implementation; a derived registration alone may leave the base MSIL path live.
- Project-level builds and focused test suites do not replace a full solution build.
  In particular, a type declared in one FECSX file can be consumed by generated
  sources for sibling components, so renaming or removing that shared declaration
  must be validated through the owning application and the complete Visual Studio
  solution.
- `buildTransitive` props can be imported before properties in the consuming project
  body exist. Defaults that depend on `FrontEndCSharpProject` or
  `IntermediateOutputPath` must remember whether the user supplied an override and
  compute the effective value inside the execution target. Otherwise paired-project
  inputs can be rejected as outside the root and cache files can land in the project
  directory.
- A generated static-web asset can have an absolute item identity on its first build
  and a default-glob relative identity on the next build. Remove both forms and add
  one project-relative `Content` item after generation; otherwise an unchanged
  second build can fail with two assets targeting the same path.
- Browser acceptance is required for adapted component sources even when FECSX and
  ordinary .NET compilation pass. The first fecscn browser fixture exposed an enum
  switch-expression path whose runtime value was not a native-size integer; explicit
  enum comparisons preserved the intended component variants without expanding the
  shadcn work into a compiler-compatibility program.
- Byte-exact registry source digests require an explicit repository line-ending
  policy. Pin digest-bearing authored sources to LF in `.gitattributes`; otherwise
  a Windows checkout can rewrite the bytes and make a valid registry fail its own
  integrity check.
- Imported React components and managed React components need distinct element
  creation paths. A managed component requires a private props marker so its render
  delegate can recover the managed snapshot; forwarding that marker to an imported
  library component can leak an implementation detail into its props or the DOM.
  Use an opaque imported-component handle with the ordinary `ReactProps` bag.
- FECSX block components cannot perform setup statements before their markup return.
  Keep component-specific imported-prop construction in the application-owned C#
  source and make the FECSX body one `return <markup>;` expression.
- `ZipArchiveEntry.Open()` commonly returns a non-seekable decompression stream,
  and `Stream.Read` may return any positive partial count. Package-archive collision
  checks must not read `Length` unless both streams are seekable or assume matching
  single-read boundaries; fill equal-sized buffers repeatedly and compare the bytes
  actually obtained.
- Recharts consumes structured JavaScript data and declares `react-is` as a peer.
  Keep typed managed chart points in source-owned APIs, convert them through a
  focused import override rather than weakening the general `ReactProps` contract,
  and pin required runtime peers explicitly when companion archives use
  `npm ci --omit=peer`. Independently owned companion archives can also require
  incompatible transitive versions at the same flattened npm path. Preserve the
  upstream constraints by checking in a valid nested package-lock layout for the
  private version; do not weaken archive-collision validation or falsely promote
  the transitive package into a shared public adapter.
- Copied component-source ownership and JavaScript-package interop ownership are
  separate boundaries. Keep shadcn-shaped FECSX/C# scaffolding in its registry and
  keep each upstream runtime dependency in a separate adapter, package graph, and
  version boundary. A shared `Interop.JS.Shadcn.*` naming prefix can make the
  selected-profile ownership explicit without combining Base UI, `cn`, and
  Recharts into one assembly or weakening tree shaking. Keep raw signature-bearing
  import types internal and expose a bounded public façade for each selected
  package surface.
- Package ownership alone does not establish a usable component API. When an
  adapter is intended for FECSX composition, expose concrete library-shaped
  component façades and assert that application/registry sources contain no
  `ImportedReactElement` or opaque-handle access. A manifest/package test can pass
  while the consumer-facing boundary remains unfinished.
- A shared JavaScript host adapter must not import an optional package selected by
  only some copied registry sources. Compose concrete React elements through the
  owning managed facade and pass them to the host as reserved child templates;
  the host can clone those elements without acquiring an undeclared cross-adapter
  dependency, and ordinary managed reachability can still remove unused imports.
- Lucide's top-level React exports contain multiple historical names for the same
  icon, so export-name enumeration is not a canonical catalog. Treat ESM modules
  that directly call `createLucideIcon` as the icon identities, discard alias-only
  re-export modules, then remove components marked `@deprecated` in the pinned
  declaration file. Generate one managed facade and one static import per remaining
  icon-data name so catalog completeness does not sacrifice tree shaking.
- Tailwind's v4 IntelliSense class list is the authoritative finite source for
  default utility names and most modifiers, while `candidatesToCss` supplies an
  exact hover preview without duplicating the framework's utility semantics.
  Some documented accepted modifiers are intentionally not advertised on their
  base entry: Tailwind 4.3.5 accepts opacity on drop-shadow size utilities even
  though those `getClassList()` entries have an empty modifier list. Preserve
  that documented completion seam explicitly and verify the generated catalog
  byte-for-byte against the pinned engine.
- Tailwind responsive breakpoints and conditions such as `hover:`, `focus:`, and
  `dark:` are variants, not utilities, so they are absent from `getClassList()`.
  Generate the selected default prefixes and their exact conditions from
  `getVariants()` into the same offline editor catalog. Keep the built-in `dark:`
  contract as Tailwind's default `prefers-color-scheme: dark` media condition;
  adding `@custom-variant dark` to the default fixture would replace that contract
  with selector-driven behavior. Because
  Visual Studio freezes an async-completion source set, typing or committing the
  variant's trailing colon must also restart the session before prefixed utility
  items can replace the stale fuzzy matches from the breakpoint token.
- Tailwind v4 theme variables are part of the utility/variant API, not merely CSS
  values. Generate a per-project editor catalog from the same effective CSS input
  as the build so imported `@theme` namespaces such as `--drop-shadow-*` and
  `--breakpoint-*` flow into completion without reimplementing Tailwind semantics.
  Publish only the resulting catalog path through compiler-visible MSBuild state;
  keep file watching and caching in the VSIX because the shared editor assembly is
  analyzer-safe and must not perform disk I/O.
- Fingerprint Tailwind CSS output and the editor design system separately. A
  `.fecsx` source edit can require tree-shaken CSS regeneration without changing
  the finite completion catalog, while a theme/import change must invalidate both.
  Treat a missing catalog as stale independently so it can self-heal without
  rewriting otherwise-current CSS.
- Tailwind utility-family boundaries cannot be recovered reliably by splitting a
  rendered class name at hyphens. One class can parse as both a static and a
  functional candidate, and another can parse against several functional roots.
  Preserve the most-specific functional root reported by Tailwind's design system
  in the generated editor catalog, and derive grouped completion and arbitrary
  examples from that metadata rather than inventing a numeric or naming sequence.
- A parsed Tailwind functional root is necessary but not sufficient for a useful
  completion family: roots such as `bg` emit several unrelated CSS properties.
  Persist the primary emitted property, reserve the broad prefix for the matching
  arbitrary-value grammar, and subdivide repeated value namespaces such as color
  scales (`bg-amber-`). Keep property-mismatched leaves exact, and sort value-less
  defaults beside their parameterized family rather than by display-text length.
- The final public declaration in a Tailwind utility can be identical across an
  entire family because it references a varying internal custom property. A tooltip
  that always chooses the first public declaration will make distinct ring widths,
  filters, transforms, shadows, mask stops, and scrollbar colors look identical.
  Compare declarations across the complete family, choose the smallest varying
  declaration set that distinguishes different generated CSS, and format internal
  variables as user-facing values such as `ring width`, `blur`, or `rotate x`.
- A NuGet package-ID rename must also rename convention-based files under
  `build`, `buildMultiTargeting`, and `buildTransitive`. NuGet emits `NU5129` when
  those props or targets retain the old package ID, and consumers may not receive
  the automatic imports. Treat package IDs, staged `.nupkg` names, nested VSIX
  paths, templates, and convention-based MSBuild asset names as one migration.
- `PackageReadmeFile` names an entry inside the finished `.nupkg`, not merely a
  source item or its MSBuild `Link`. When several projects share an external
  readme, give the packed item an explicit `PackagePath` matching the declared
  filename and inspect the archive after packing. Project-local readmes should
  remain owned by their projects so central packaging does not add duplicates.
- A registry source digest is part of the same change as the byte-exact authored
  source. After changing a digest-bearing component, update its authoring manifest
  and run the registry builder across the complete registry so a later mismatch is
  not hidden behind the first failure.
- NuGet's `--packages` option applies to the complete project-reference restore
  graph. A disposable fixture that points it at a temporary cache can rewrite
  repository `project.assets.json` files and then leave them broken when the cache
  is deleted. Keep that restore graph disposable by consuming freshly packed local
  packages instead of referencing repository projects.
