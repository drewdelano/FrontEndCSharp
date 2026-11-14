# JSON Libraries

This document defines the approved boundary and verified compatibility surface for
`System.Text.Json`, Newtonsoft.Json, and `System.Net.Http.Json` in the browser
runtime. The primary outcome is deliberately narrow: buffered JSON received through
`HttpClient` can be deserialized into ordinary managed objects that are convenient
for C# application code to navigate. Complete serializer-library parity is not a
project goal. The selected .NET package remains the public contract; JavaScript JSON
APIs are private implementation tools.

`System.Text.Json` through `System.Net.Http.Json` is the primary path. Newtonsoft.Json
compatibility is secondary and exists for applications that explicitly choose that
package; it is not required to complete the initial HTTP response workflow.

## Relationship to the project goals

The JSON work is governed by [`GOALS.md`](../GOALS.md):

- Enable the buffered browser HTTP path; do not treat JSON as an independent
  library-completeness program.
- Preserve the observable behavior of the selected desktop .NET package. The two
  serializers intentionally keep different naming, conversion, ordering, option,
  and exception rules.
- Fail closed when a public overload, option, converter, metadata shape, or managed
  type contract has not been implemented and verified.
- Keep emitted output minimal. Retain only the application metadata and executable
  members requested through package-neutral reachability declarations.
- Prove supported behavior with C# tests against desktop .NET and the browser
  runtime, including real Chromium where browser execution matters.

The JavaScript-first serializer companions are a narrow exception to the usual
"compile rather than reimplement" rule. The full managed serializer paths currently
reach unsupported address-producing operations, span-heavy internals, pools, and
private runtime layout before useful public behavior executes. The exception is
limited to the public entry points and behavior recorded here. It must not move JSON
rules into the compiler or justify unrelated managed-library reimplementations.
Re-evaluate the exception if the managed serializer paths become compatible with the
supported runtime model. Focused coverage beyond the core response-deserialization
path remains useful, but it does not create a commitment to implement adjacent
serializer features or make every covered edge case part of the routine browser gate.

## Architecture boundary

- `System.Text.Json.fecs` and `Newtonsoft.Json.fecs` own separate private token,
  contract, conversion, and writer implementations. They do not share a serializer
  substrate, because superficially similar behavior differs observably between the
  packages.
- Managed application constructors, accessors, comparers, and other invoked methods
  stay managed. Companion providers declare the package-neutral type metadata and
  method access they require; the compiler does not know serializer attributes or
  rules.
- A provider return declares both its managed contract type and, when known, the
  concrete implementation type. Reachability uses both so an interface contract can
  retain only the selected construction and mutation path.
- Shared CoreLib overrides are allowed only for general CLR behavior such as runtime
  type identity, collection contracts, equality, and date/time backing operations.
  They must not contain serializer-specific policy.
- `System.Net.Http.Json` is the integration target. Its companion contains only the
  asynchronous bridge from buffered `HttpContent` to the verified generic
  `System.Text.Json` string entry point; it is not a separate serializer or Fetch
  implementation.

Equivalent low-level code may be duplicated between the serializer companions. That
duplication is preferable to coupling their contracts or normalizing their behavior.

## Native JSON primitives

The supported Node and Chromium runtimes provide the `JSON.parse` reviver `context`
argument. Its `source` text permits exact integral conversion before a JavaScript
`Number` loses precision. Native parsing is used only where feature detection and
the requested contract prove it lossless.

Native parsing alone is insufficient because it collapses duplicate properties,
can reorder integer-shaped names, rounds numeric values, and does not produce .NET
DOM or serializer objects. Each companion therefore owns any source index or ordered
token representation needed for exact paths, positions, duplicates, token kinds,
and original numeric text. Object storage must not permit names such as `__proto__`
to mutate JavaScript prototypes.

`JSON.stringify` is not a serializer implementation. Its `toJSON`, omission,
non-finite-number, `BigInt`, property-order, and escaping behavior does not define
either managed package's contract.

## Verified compatibility surface

The tests are the authority for exact edge behavior. This section records the
supported public surface at a useful planning level rather than duplicating every
assertion.

### Shared typed surface

Both companions currently verify:

- generic string serialization and deserialization through their selected stable
  public entry points;
- null, string, Boolean, `Char`, all integral widths, `Single`, and `Double`, with
  exact 64-bit integer tokens and package-specific coercion and non-finite rules;
- typed POCO classes, positional records, and directly requested application
  structs; constructor binding, managed getter/setter execution, inheritance,
  opted-in non-public members, read-only members, required members, ordering,
  effective-name collision rejection, and unknown members;
- one-dimensional arrays over the supported primitive contracts and each package's
  distinct `byte[]` representation;
- numeric enums for all eight backing types, flags, aliases, undefined values, and
  the built-in string-enum converters with package-specific naming and casing;
- recursive `List<T>`, `IList<T>`, and `IReadOnlyList<T>` plus string-keyed
  `Dictionary`, `IDictionary`, and `IReadOnlyDictionary` contracts over supported
  primitive, enum, and POCO values;
- default `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, and `TimeSpan`
  contracts with managed tick precision; and
- package-specific exception families, JSON paths, and source positions for covered
  syntax and conversion failures.

### System.Text.Json

The verified entry points are the generic string overloads of
`JsonSerializer.Serialize` and `JsonSerializer.Deserialize` with default or explicit
`JsonSerializerOptions`.

Supported options include the `General` and `Web` presets, case-insensitive property
matching, all five built-in property and dictionary-key naming policies, number and
unmapped-member handling, field inclusion, read-only filtering, default/null
omission, the obsolete `IgnoreNullValues`, trailing commas, skipped comments,
maximum depth, indented output, and `ReferenceHandler.IgnoreCycles`. Covered
attributes include `JsonPropertyName`, `JsonIgnore`, `JsonInclude`,
`JsonPropertyOrder`, `JsonRequired`, C# `required`, `JsonNumberHandling`, and
`JsonUnmappedMemberHandling`.

Built-in `JsonStringEnumConverter` and `JsonStringEnumConverter<TEnum>` support
naming policies, flags, target scoping, and integer fallback. Custom naming policies,
custom converters or reference handlers, and `ReferenceHandler.Preserve` fail
explicitly.

`HttpContent.ReadFromJsonAsync<T>(CancellationToken)` is verified over buffered
browser HTTP content with the Web options preset. Its serializer dependency remains
closed over the caller's `T`, so the compiler retains the same POCO and collection
contract that a direct `JsonSerializer.Deserialize<T>` call would require.

### Newtonsoft.Json

The verified entry points are the one-argument and settings overloads of
`JsonConvert.SerializeObject(object)` and `JsonConvert.DeserializeObject<T>(string)`.

Covered contracts include Newtonsoft's case-insensitive exact-name preference,
public fields and properties, `JsonProperty`, `JsonIgnore`, `JsonRequired`,
`JsonConstructor`, `JsonObject(MemberSerialization.OptIn)`, item required/null
handling, per-member required/null/default handling, and typed primitive
`DefaultValueAttribute` constants. The built-in default, camel, snake, and kebab
naming strategies and `StringEnumConverter` are supported for the verified forms.

Covered settings include formatting and string escaping; global null, default, and
missing-member handling; constructor handling; ignored reference loops; maximum
depth; and additional-content checking. Active reference preservation, type-name
metadata, custom naming strategies, custom converters, and unverified settings fail
explicitly.

## Deliberately deferred surface

This section is a boundary, not a roadmap. The following areas are outside the
currently verified surface and are not implied by nearby support. Revisit one only
when an agreed application or HTTP-response scenario requires it:

- source-generated contexts, DOM and node APIs, `JToken`/LINQ-to-JSON and JSONPath;
- serializer-native UTF-8 readers and writers, streams, asynchronous APIs, and
  cancellation beyond the buffered `HttpContent` bridge;
- custom converters, serialization callbacks, contract resolvers, polymorphism,
  extension data, and reference preservation;
- arrays of POCOs, non-string dictionary keys, and broader mutable, immutable, set,
  queue, stack, or user-defined collection contracts;
- unannotated non-public fields selected by Newtonsoft fields-mode contracts; and
- custom date formats and the remaining date-related settings.

Reachability and contract choices that require agreement remain in
[`DISCUSSION.md`](../DISCUSSION.md). A deferred feature must not be approximated by
JavaScript defaults or added as a serializer-specific compiler root.

## Minimal-output rules

- Managed type access is declarative and package-neutral. It may retain requested
  constructors, properties, fields, attributes, enum literals, collection
  operations, and managed callback methods, but it does not select serializer
  behavior.
- Runtime-override providers are the extension point for reflection-driven
  reachability. Their C# contracts request the exact package-neutral type metadata
  and executable member categories a library will discover at runtime; the
  compiler must not infer library policy or broadly retain application assemblies.
  Genuinely open-ended application reflection requires explicit application-owned
  roots. Add a new provider capability only for an agreed, bounded consumer and
  keep unsupported discovery modes failing closed until that contract exists.
- Contract and concrete return types are both significant. Interface or base-type
  metadata alone is insufficient when the provider constructs a specific managed
  implementation.
- Enum metadata contains only underlying type, flags status, and declared literal
  names and values. Naming and conversion remain companion-owned.
- Collection companions use public managed contracts and declared dependencies;
  they do not inspect desktop collection private fields.
- Unsupported shapes and active options throw deterministic managed exceptions
  rather than silently using JavaScript behavior.

## Verification

Behavioral changes require desktop-versus-browser evidence at the same public API.
Prioritize the end-to-end buffered-response path through `GetFromJsonAsync<T>` or
`HttpContent.ReadFromJsonAsync<T>` into a representative POCO and collection graph.
The primary suites are:

- [`RuntimeOverrides.Tests/CompanionTests.cs`](../RuntimeOverrides/RuntimeOverrides.Tests/CompanionTests.cs)
- [`Compiler.Tests/JsonLibraryParityTests.cs`](../Compiler/Compiler.Tests/JsonLibraryParityTests.cs)
- [`SystemTextJsonBrowserSmoke`](../RuntimeOverrides/System.Text.Json/Tests/SystemTextJsonBrowserSmoke/Program.cs)
- [`NewtonsoftJsonBrowserSmoke`](../RuntimeOverrides/Newtonsoft.Json/Tests/NewtonsoftJsonBrowserSmoke/Program.cs)
- [`HttpJsonBrowserSmoke`](../RuntimeOverrides/System.Net.Http.Json/Tests/HttpJsonBrowserSmoke/Program.cs)

The two direct serializer smokes are intentionally independent and bounded to one
representative POCO each. `HttpJsonBrowserSmoke` is the primary composed buffered
browser gate and covers HTTP, the Web defaults, a POCO, a list, and a string-keyed
dictionary through one JSON POST echo. Response-streaming belongs to the separate System.Net.Http browser gate
so neither closed-world fixture reaches the repository's code-owned 10-second
maximum; cases at or above the five-second slow bar require review. Keep option
combinations and edge behavior in focused parity tests.

Use C# harnesses for Node and Chromium execution. Add focused compiler/runtime tests
when a change affects package-neutral reachability, metadata, or shared CLR behavior.
Keep unsupported cases as explicit regression expectations.
