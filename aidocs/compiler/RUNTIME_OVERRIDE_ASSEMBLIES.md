# Runtime Override Assemblies

FrontEndCSharp runtime overrides are distributed as ordinary managed companion dependencies. A companion for `System.Console.dll` is named `FrontEndCSharp.Overrides.System.Console.fecs.dll`. It does not replace, patch, or load into the target application; the compiler reads its metadata and embedded resources from the compiled application's resolved dependency graph.

## Authoring model

The lightweight `FrontEndCSharp.RuntimeOverrides` contract assembly defines the metadata attributes. A companion declares one target assembly and groups provider methods by target type:

```csharp
[assembly: RuntimeOverrideAssembly("System.Console")]

[RuntimeOverrideType(typeof(Console))]
internal static class ConsoleOverrides
{
    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void WriteLine(string value);
}
```

The pack's embedded `Overrides/manifest.json` maps that inferred signature to a deterministic resource name:

```json
{
  "version": 1,
  "overrides": [
    {
      "signature": "System.Console.WriteLine(System.String)",
      "file": "method.f5d661fa7ac9fcfd6d2284d5985c7a035e7210556ab7bfce22c1d2bf99054b17.js"
    }
  ]
}
```

The named resource contains the JavaScript implementation and must register the same canonical signature:

```javascript
globalThis.DotNet.Methods['System.Console.WriteLine(System.String)'] = function (ctx, value) {
    console.log(value);
};
```

The signature and resource name are not repeated in an attribute. C# overload resolution and the provider method's parameter and return types describe the signature; the manifest locates the resource. The compiler validates that the target member exists and that the manifest digest, filename, and embedded JavaScript registration all match that inferred signature.

Instance providers are static methods whose first parameter is the target receiver and use `MemberKind = RuntimeOverrideMemberKind.InstanceMethod`. Constructor providers use `MemberKind = RuntimeOverrideMemberKind.Constructor`, accept the constructor parameters, and return the constructed target type. `TargetMemberName` exists only for the uncommon case where the legal provider method name cannot match the target metadata name.

Non-generic provider declarations without managed dependencies use `extern` and `MethodImplOptions.InternalCall`, making it explicit that the companion is metadata rather than executable replacement code. The CLR rejects `InternalCall` on generic methods and methods of generic types, so those providers use an inert throwing body instead.

## Dependency declarations

The provider body is never executed. Managed member references in a non-extern provider body are strongly typed dependency declarations. An override with dependencies references them in its declaration body; the compiler decodes those references from IL and adds them to the managed call graph. The binary package installs those managed registrations before execution, so embedded JavaScript contains implementation imports only and carries no managed-method dependency declarations.

This avoids canonical-signature strings in dependency attributes and lets the C# compiler reject missing overloads or incompatible generic calls. Virtual and interface references use the runtime override's upstream possible argument types to select concrete receivers. Return types from those selected targets then feed dependent virtual references, so an ordinary provider body can declare a complete chain such as `GetEnumerator`, `Current`, `MoveNext`, and `Dispose` without a second enumeration-specific attribute.

Writes through provider `ref` and `out` parameters are declarations too. The
compiler summarizes concrete and null values that reach normal provider returns,
including writes composed through ordinary companion helper methods, then applies
the summary to the caller's referenced local, argument, field, or array element.
`ref` preserves the incoming possibilities while `out` replaces them, and every
concrete result is checked against the target parameter's element type. Exceptional
completion does not apply the normal-return summary.

Generic dependency signatures may use `object` as an erased generic slot. The
package-neutral dependency matcher closes that slot from the requested managed
contract while preserving every non-erased generic argument exactly. For example,
a companion can relate `List<object>` construction and `ICollection<object>.Add`
to an `IList<int>` contract without the compiler knowing that either type is a
collection or imposing an interface-to-concrete mapping. The constructor and
interface references remain independent policy in that companion's provider MSIL.

`ManagedTypeAccessAttribute` declares reflection-like access that provider JavaScript
performs against a generic argument or a proven runtime parameter type. Use
`InstanceConstructors` when the provider may bind any instance constructor, as JSON
deserializers do for parameterized constructors and positional records; use
`PublicParameterlessConstructor` only when the provider contract is limited to a
public default constructor. A provider may list exact atomic or terminal contracts
in `ExcludedTypes`. The compiler still retains each excluded contract type, but it
does not apply that declaration's constructor, member, or enum-metadata policy to
the type. Exclusions belong to one access declaration and are never global: another
provider can independently request access to the same type. Both JSON companions
exclude `System.String`, which they process atomically, so a direct string contract
does not retain unused pointer, span, and other `System.String` constructors while
ordinary POCO constructor binding remains unchanged.

Requested property and field access carries the same policy recursively into direct
application-owned reference types, allowing nested POCO contracts to retain their
constructors and selected members. Framework member types remain behind explicit
provider dependencies so a POCO policy cannot accidentally retain their desktop
implementation details; generic application arguments of mapped framework
containers still receive the recursive policy.

CoreLib formatting that depends on private runtime layout or unsupported stack
allocation remains a focused runtime adaptation. The invariant integral slice owns
all public `ToString(*)` overloads on `SByte`, `Byte`, `Int16`, `UInt16`, `Int32`,
`UInt32`, `Int64`, `UInt64`, `IntPtr`, `UIntPtr`, `Int128`, and `UInt128`. A shared
arbitrary-precision formatter implements the applicable invariant .NET 8 standard
and custom numeric formats, including decimal, hexadecimal, binary, currency,
fixed-point, general, number, percent, exponential, sections, digit placeholders,
grouping, scaling, literals, escaping, and managed `FormatException` behavior.
Native integers retain the runtime's 32-bit native-width contract.

The invariant non-integral slice owns all public `ToString(*)` overloads on `Half`,
`Single`, `Double`, and `Decimal`. Its formatter preserves the .NET 8 shortest
round-trip limits (5, 9, and 17 digits), the legacy custom-format limits (5, 7,
and 15 digits), signed zero, `NaN`, infinities, standard numeric formats, and the
shared custom-format grammar. `Decimal` is decoded from its exact 96-bit coefficient,
scale, and sign; its public scaled-value constructor has a companion override because
the framework implementation reaches unsupported indirect floating-point/pointer-era
runtime machinery before formatted decimal values can otherwise be exercised.

The invariant enumeration slice owns all public `System.Enum.ToString(*)`
overloads. It follows the .NET 8 `G`, `F`, `D`, and `X` contracts for every C#
integral backing type: unsigned-value name ordering, duplicate aliases, exact names,
greedy flags decomposition from largest to smallest followed by ascending-name
output, signed decimal fallback, and uppercase zero-padded hexadecimal output.
Enum metadata remains reachability-driven. The generated provider marks its instance
receiver for `EnumMetadata`, while interpolation marks its generic value parameter;
unrelated non-enum values do not expand into reflected type hierarchies.

The invariant temporal and duration slice owns every public `ToString(*)` overload
on `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, and `TimeSpan`. It implements
the applicable .NET 8 standard and custom formats, including seven-digit tick
precision, `DateTimeKind` and offset behavior, UTC conversions, optional fractions,
quoted literals, escaping, percent tokens, type-specific token restrictions, and
managed `FormatException` behavior. Boundary and representative values are covered
by generated desktop/runtime parity matrices. The identifier slice also owns every
standard `Guid` shape.

The secure-random slice keeps one Web Crypto entropy source in the CoreLib-owned
`secure-random.js` asset. `Guid.NewGuid()` consumes sixteen random bytes and sets the
RFC 4122 version-4 and variant bits. `FrontEndCSharp.Overrides.System.Security.Cryptography.fecs` reuses that
asset for the bounded static `RandomNumberGenerator.GetBytes(int)`, `GetInt32(int)`,
and `GetInt32(int, int)` surface. Byte requests are chunked beneath Web Crypto's
per-call quota, and integer ranges use rejection sampling rather than biased modulo
reduction. Instance generators, nonzero-byte APIs, spans, and cryptographic
algorithms remain outside this slice.

The single-threaded `Lazy<T>` slice owns factory construction, `Value`, and
`IsValueCreated`. It evaluates a factory at most once, caches the original managed
exception on failure, and rejects recursive `Value` access with managed
`InvalidOperationException`. Boolean and `LazyThreadSafetyMode` constructors,
publication races, default construction, and CLR spin-wait or synchronization
emulation remain outside this browser-runtime slice.

The common `System.Random` slice owns parameterless and explicit-seed construction,
`Random.Shared`, all three `Next` bounds, `NextDouble`, and `NextBytes(byte[])`.
Explicit seeds drive a stable 32-bit generator so equal seeds and call sequences
repeat exactly; parameterless and shared instances use Web Crypto only to obtain
their initial seed. Integer bounds use rejection sampling to avoid modulo bias and
preserve the full signed-int interval. Span, 64-bit, single-precision, shuffle,
string, and collection-oriented APIs remain outside this slice.

The common `System.Threading.Timer` browser slice owns the callback/state constructor
with `Int32` due time and period, `Change(Int32, Int32)`, and `Dispose`. A recursive
event-loop timeout implements both one-shot and periodic delivery; a generation token
prevents an obsolete callback from rescheduling after `Change` or `Dispose`, including
when either operation occurs inside the callback. `PeriodicTimer`, `TimeProvider`,
wait-handle disposal, CLR thread-pool concurrency, and broader threading APIs remain
outside this slice.

The common `Task.Delay` slice owns the `Int32` and `TimeSpan` overloads with and
without `CancellationToken`. Zero completes synchronously, `-1` remains pending,
positive delays use the browser event loop, and long valid TimeSpan delays are
chunked beneath the host timeout limit. Cancellation before or during a delay
produces a canceled Task and removes its registered callback on terminal completion.
Linked sources, `CancelAfter`, public token registration, and the public
`TimeProvider` overload remain outside this slice.

The ordinary byte-array-backed `MemoryStream` slice owns parameterless and
`byte[]` construction, capability and length/position/capacity properties,
synchronous byte-array and single-byte reads and writes, seeking, length changes,
array copies, permitted buffer exposure, disposal, and byte-array `ReadAsync` and
`WriteAsync`. Parameterless streams are expandable and publicly expose their
buffer; `byte[]` streams remain writable but fixed-capacity and non-exposable.
Async byte-array operations complete synchronously or return a pre-canceled Task.
Span/memory overloads, unmanaged storage, filesystem behavior, and a general
addressable-memory model remain outside this slice.

The temporal slice also owns `DateTime.Now`, `DateTime.Today`, `DateTime.UtcNow`,
`DateTimeOffset.Now`, and `DateTimeOffset.UtcNow`. Each getter samples the host clock
once, converts Unix milliseconds to managed ticks, and derives any local offset from
that same instant so local `DateTimeKind`, daylight-saving offsets, UTC zero-offset
values, and local-midnight truncation remain internally consistent.

`DefaultInterpolatedStringHandler` dispatches formatted values through the exact
intrinsic formatter selected by concrete generic `T`, or through virtual
`IFormattable.ToString(string, IFormatProvider)` for user types. Non-formattable
values ignore the format and use virtual `Object.ToString()`, so inherited overrides
and the default managed runtime-type name are preserved. Null values produce empty
text. Provider IL declares the formatting dependencies so interpolation-only
programs retain the concrete formatters; a widened evaluation-stack representation
does not change the selected intrinsic formatter. Non-invariant cultures remain
outside this program.

The matching invariant parsing slice owns `Parse(string)` and
`TryParse(string?, out T)` for the same integral, floating-point, decimal, enum,
temporal, duration, and identifier types. It follows desktop .NET for accepted
invariant syntax, bounds, special floating-point values, null input, parse versus
overflow exceptions, and the zero/default result written by unsuccessful
`TryParse`. Tests include boundary and formatter-to-parser round trips through the
real compiler/runtime. Style/provider overloads, span and UTF-8 inputs, generic
parsing interfaces, and non-invariant cultures are intentionally outside this
bounded slice.

## Runtime return types

The target method's declared return type remains the managed contract at every call
site. When JavaScript constructs a more specific managed type, the provider declares
that runtime possibility with `RuntimeOverrideReturnTypeAttribute`. A concrete type
may be named directly, an open generic definition such as `List<>` may be closed
against the target contract, or an integer argument may select a method generic
parameter:

```csharp
[JavaScriptOverride]
[RuntimeOverrideReturnType(typeof(List<>))]
internal static IList<T> CreateList<T>() => throw null!;

[JavaScriptOverride]
[RuntimeOverrideReturnType(0)]
internal static T Deserialize<T>(string json) => throw null!;
```

For `Task<T>` overrides, the provider body can instead construct one or more
concrete values and pass their common contract to `Task.FromResult`. The catalog
uses the same control-flow analysis as managed code to summarize only values that
actually reach the returned task; unrelated allocations are excluded. Constructed
generic results are represented recursively, so multiple alternatives and nested
generic arguments retain their exact identities. The compiler carries that summary
through ordinary task wrappers, `GetAwaiter`, and `GetResult`, then uses the concrete
result for virtual dispatch while preserving the public `Task<T>` contract.

Array result and by-ref references use the same recursive element descriptor as the
binary package, including array kind, rank, and shape; companion metadata never
uses an assembly-qualified reflection array name as identity.

The catalog validates each declaration against the target return contract. The
compiler retains the declared contract as referenced metadata and uses only the
selected concrete runtime possibilities for virtual dispatch and allocation-
sensitive flow, avoiding unrelated implementations. Provider-body dependencies
continue to declare managed members required by an implementation; they are not a
substitute for explicit return semantics.

A companion may also define a runtime-only concrete implementation that does not
exist in the desktop target assembly, such as a browser-conditioned internal type.
Mark that sealed companion type with `RuntimeOverrideImplementationTypeAttribute`;
providers may then target it even though the companion still declares the public
framework assembly as its package target. During compilation the companion is
loaded into the selected application's load context so the implementation shares
the application's contract types and cannot acquire an unrelated assembly identity.

## Auxiliary runtime assets

A companion can own JavaScript modules used by several of its override resources
by embedding `RuntimeAssets/*.js` with logical names beneath the exact namespace
`<companion assembly>.RuntimeAssets`. The compiler accepts only non-empty, strict
UTF-8 resources with simple lowercase `.js` filenames. The compiler begins with the
override modules actually present in the managed call graph and writes only their
transitive static-import closure. Assets therefore require both a selected companion
and a used override path; merely selecting an owner does not emit every optional
capability it contains. Unbundled output places the closure at
`runtime-assets/<companion assembly>/<file>`. The owner-qualified directory makes
equal local filenames in different companions collision-safe. The writer replaces
the complete `runtime-assets` directory for each unbundled compilation; no mutable
inventory file is part of public output. Bundled output consumes the same selected
closure in staging and publishes no loose runtime modules.

Method overrides import their owner's auxiliary modules through
`../runtime-assets/<companion assembly>/<file>`. An auxiliary module reaches the
compiler runtime two levels above its owner directory. Single-owner modules belong
to the companion defining their managed contract: expression trees, ValueTask and async iterators,
character enumeration, culture, hash sets, concurrent dictionaries, regular
expressions, and stacks follow that rule. The compiler discovers and transports
these resources generically; it must not contain companion names or asset filename
lists. Only execution-engine and universal runtime primitives belong to the compiler.
Optional BCL or framework behavior belongs to a companion even when several other
companions consume it. Such consumers import the owner-qualified module and declare
a direct `ProjectReference` or package dependency on the owner companion so ordinary
dependency discovery selects it. A missing selected owner is a compilation error;
selecting no used consumer omits that helper graph from output. Moving a shared
module must never create an undeclared cross-companion dependency.

## Discovery and safety

- A companion filename must end in `.fecs.dll`. Its target assembly identity comes
  from the required `RuntimeOverrideAssemblyAttribute`, not from its filename.
- A provider target must belong to the declared target assembly or be an explicitly
  marked companion implementation type.
- Duplicate managed signatures, manifest filenames, and reused JavaScript resources are errors.
- A signature may occur in exactly one companion; there is no implicit precedence between packs.
- JavaScript is stored as an embedded UTF-8 resource and validated before use.
- Every resource uses `method.<sha256>.js`, derived from its exact canonical signature and recorded in the embedded manifest.
- Companion attributes and signatures are inspected with `MetadataLoadContext`. Provider IL is decoded through a collectible isolated load context; custom attribute constructors and provider methods are never executed, and provider code is never loaded into the default application context.
- Resource bytes are read directly from the PE resource section.

The compiler reads the compiled application's `.deps.json` and loads only `*.fecs.dll` runtime assets selected by that dependency graph. Merely placing a companion beside the application or compiler does not select it. The companion is validated without loading provider code into the default application context, and its embedded JavaScript is written through the same signature-hashed module path used by emitted methods. `FrontEndCSharp.Overrides.System.Console.fecs.dll` owns the Console overrides and `FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.dll` owns the CoreLib overrides. Package companions follow the same rule: for example, `FrontEndCSharp.Overrides.FluentValidation.fecs.dll` targets the pinned FluentValidation assembly and owns only its browser environment adaptation. Supporting framework contracts remain in their defining companions, including `System.Collections`, `System.Collections.Concurrent`, `System.Linq`, `System.Linq.Expressions`, `System.Security.Cryptography`, and `System.Text.RegularExpressions`.

Build and test all companion projects with `dotnet test Overrides/Overrides.sln`.
Each family and project is prefixed with `FrontEndCSharp.Overrides`, including
`FrontEndCSharp.Overrides.System.Console/FrontEndCSharp.Overrides.System.Console.fecs`
and `FrontEndCSharp.Overrides.System.Private.CoreLib/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs`.
`Overrides/FrontEndCSharp.Overrides.Interop.JS.React/Tests/FrontEndCSharp.Overrides.Interop.JS.React.Tests`
validates every companion's target attribute, manifest, signature hashes, resources,
and expected override count in one React-owned test host using shared infrastructure
from `Compiler/RuntimeOverrides/Tests`. The public override-authoring attributes live
in the separate `FrontEndCSharp.RuntimeOverrides` assembly under
`Compiler/RuntimeOverrides/Abstractions`. A browser-target project selects a pack
with an ordinary `ProjectReference` or `PackageReference`; the normal .NET build
must copy the companion beside the target assembly and record it in the target's
`.deps.json`. The compiler's `--generate-runtime-override-providers` command
regenerates the typed CoreLib declarations, and
`--generate-runtime-override-manifest` regenerates each pack's embedded manifest
after resource changes. Both are C# tooling commands and take a resource directory
followed by an output path. JavaScript-package companions import the compiler-owned
packaging contract from
`Compiler/RuntimeOverrides/Packaging/JavaScriptPackageCompanion.targets`.

Build `Compiler/Core/Compiler.csproj`, then invoke either command through
`dotnet <Compiler.dll> <command> <resource-directory> <output-path>`. This keeps
member discovery on the compiler's .NET 8 CoreLib and makes generation independent
of the caller's shell. Provider authoring logic lives in
`Compiler/Core/Helpers/RuntimeOverrideProviderGenerator.cs`; do not edit regenerated
declarations as source. Manifest generation lives in
`Compiler/Core/Helpers/RuntimeOverrideManifestGenerator.cs`. The compiler test suite
regenerates CoreLib provider source into disposable output, requires it to match the
checked-in generated source compiled by the ordinary project graph, and validates
that compiled companion through the normal override catalog.

When a generated CoreLib provider needs managed dependencies beyond the generator's
ordinary signature-derived body,
`Overrides/FrontEndCSharp.Overrides.System.Private.CoreLib/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/Overrides/provider-bodies.json`
owns the exact C# statements and managed parameter-access declarations for those
signatures. Parameter keys are zero-based target parameters; `this` denotes an
instance receiver and is cataloged as target parameter `-1`. The compiler generator
only validates and emits that companion-owned data; it must not contain tables of
CoreLib collection methods, comparer behavior, or primitive hashing exceptions.

## JavaScript library adapters

A JavaScript library adapter is a pair of assemblies under `Overrides/`:

- A normal managed assembly such as `FrontEndCSharp.Overrides.Interop.JS.React.dll` defines the public C# API and carries no JavaScript implementation while retaining the `Interop.JS.React` C# namespace.
- Its `FrontEndCSharp.Overrides.Interop.JS.React.fecs.dll` companion targets that managed assembly through `RuntimeOverrideAssemblyAttribute`, supplies the JavaScript method implementations, and declares its npm requirements with `JavaScriptPackageDependencyAttribute`.

For an npm-backed adapter, both projects in the pair use the exact version of the
primary upstream JavaScript package as their project/package `<Version>`. Peer and
private transitive dependencies do not determine that version. Package-free
adapters such as `FrontEndCSharp.Overrides.Interop.JS.Browser` retain the FrontEndCSharp package version.

The adapters are organized as `FrontEndCSharp.Overrides.Interop.JS.Browser/`,
`FrontEndCSharp.Overrides.Interop.JS.React/`, `FrontEndCSharp.Overrides.Interop.JS.MobX/`,
`FrontEndCSharp.Overrides.Interop.JS.Motion/`, `FrontEndCSharp.Overrides.Interop.JS.LucideIcons/`,
`FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI/`, `FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn/`,
and `FrontEndCSharp.Overrides.Interop.JS.Shadcn.Recharts/`. The `Shadcn` segment
marks the bounded FECSN/shadcn-selected API ownership while each adapter continues
to own and version its distinct upstream npm package. Browser DOM/window behavior
has no npm dependency.
The library adapters build an embedded `FrontEndCSharp.JavaScriptPackages.zip`
from the checked-in package lock owned by each companion. A consuming
FrontEndCSharp build therefore does not run npm or depend on a global
`node_modules` tree. Base UI, Lucide Icons, and Recharts declare the selected React
version as a peer supplied by `Interop.JS.React`; Recharts owns `react-is` privately
rather than publishing a separate adapter for that implementation dependency. The
Lucide pair is versioned with `lucide-react@1.72.0` and exposes only an explicitly
reviewed catalog through one static override per icon; it does not expose dynamic
name lookup or bind the complete upstream catalog.

The compiler validates that every embedded direct dependency has the declared exact package name and version. It combines only the selected companions' requirements, rejects conflicting versions, extracts their locked package graphs into the disposable bundling directory, and lets webpack include only modules reachable from used overrides. The temporary `node_modules` tree is never published with browser output. A peer declaration can be satisfied by a selected companion that embeds the same exact package version; an embedded dependency wins over an otherwise identical peer declaration.

Package archives can contain identical files contributed by more than one selected
companion. Collision validation compares their decompressed content. Zip entry
streams are not assumed to be seekable, expose `Length`, or return equal chunk sizes
from one `Read`; comparison must fill each buffer through repeated reads before it
decides that equal compressed-package entries differ.

Do not put React, MobX, Motion, or another adapter's npm packages into the compiler's own bundler dependency set. That would make unselected libraries part of the compiler payload and would prevent companion-level version validation and distribution.
