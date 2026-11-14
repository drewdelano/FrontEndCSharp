# Goals and Anti-Goals

This document defines the boundaries used when choosing implementations. When a quick fix conflicts with these goals, the architectural goal wins.

## Goals

### Preserve .NET semantics

- Treat emitted MSIL and CLI metadata as the source of truth.
- Make compiled code behave like the corresponding desktop .NET code, including exceptions, generics, value types, numeric behavior, and control flow.
- Let the C# compiler validate source-level type compatibility whenever possible.
- Represent .NET `null` as JavaScript `null`; never use `undefined` as a substitute.

### Compile rather than reimplement

- Compile environment-neutral BCL methods from their existing MSIL and recursively include their dependencies.
- Use focused JavaScript runtime overrides when a BCL operation necessarily has a different browser implementation, such as terminal HTTP transport, browser console output, or asynchronous scheduling. Preserve the public .NET API and keep shared behavior on the managed MSIL path whenever possible.
- Provide invariant-culture `ToString(*)` formatting for the standard .NET numeric,
  enumeration, temporal, duration, and identifier types. Use the .NET 8 runtime
  sources and desktop behavior as the contract, keep each formatter in the runtime
  override companion for its defining framework assembly, and make interpolation
  dispatch through the same managed overloads rather than a second formatting
  policy.
- Browser networking is bounded enabling work for ordinary web applications: make
  the common buffered `HttpClient` workflow useful through familiar .NET APIs,
  then support asynchronous streaming for incremental responses such as LLM output.
  Fetch owns CORS, cookies, forbidden headers, redirects, caching, and other browser
  policy. Complete `System.Net.Http` or socket-handler parity is not a goal; see
  [`features/BROWSER_HTTP.md`](features/BROWSER_HTTP.md).
- JSON support is bounded enabling work for buffered browser HTTP: deserialize a
  response payload into useful managed objects through familiar .NET APIs. The
  selected serializer companions are a scoped exception documented in
  [`features/JSON.md`](features/JSON.md); they do not establish a goal of complete serializer-library
  parity or authorize package-specific compiler behavior.
- Fail closed for IL-less runtime/internal methods that do not have an explicitly supported implementation.

### Fail closed

- Reject unsupported metadata, unsafe pointers, native APIs, and signatures explicitly.
- Produce a clear compile-time `NotSupportedException` rather than placeholder names or invalid JavaScript.
- Do not silently permit casts, conversions, or calls whose validity has not been established.

### Maintain a strict runtime

- Keep runtime values wrapped with their .NET type information.
- Use explicit state and C#-like invariants instead of relying on JavaScript coercion or incidental behavior.
- Support synchronous and asynchronous execution without changing observable .NET semantics.
- Keep the generated JavaScript minimal through dependency analysis and tree shaking.

### Prove changes

- Follow red-green-refactor for behavioral changes.
- Cover compiler and runtime behavior through C# unit and integration test projects. A C# harness can launch Node.js or Chromium when the system under test is JavaScript; the implementation language of the product does not require the test harness to use that language.
- Keep all existing tests passing and add regression tests for every corrected defect.

## Implementation standards

- Research the applicable C# specification, ECMA-335 rule, CoreCLR behavior, and emitted IL before implementing language or runtime semantics.
- Implement the complete behavior, including null handling, inheritance, interfaces, conversions, exceptional paths, and generic forms that are part of the same feature.
- Prefer reflection and metadata abstractions over parsing or manufacturing names.
- Implement repository tests, browser/debugger drivers, generators, validators, and maintenance automation in C# by default. Do not introduce JavaScript test infrastructure or PowerShell generation/regeneration logic when an ordinary C# test, console tool, MSBuild task, or source generator can provide the same behavior.
- Use explicit whitelists for supported behavior. Unknown cases must be rejected rather than accepted optimistically.
- Comments should explain why an implementation follows a particular rule; comments must not substitute for required behavior.
- Do not accept a partial implementation solely because it makes the currently failing test pass.
- Support both synchronous and asynchronous execution paths where the underlying .NET feature permits both.

## In scope

- Reading assemblies, MSIL, signatures, and reflection metadata.
- Building and linking the call graph.
- Interpreting MSIL in JavaScript.
- Browser-compatible .NET execution and explicit JavaScript interoperability.
- Task and async adaptations required by JavaScript's event loop.
- Exception handling, type checking, generics, delegates, and managed memory semantics.

## Anti-goals

- Parsing C# source as the primary compilation model.
- Hand-writing JavaScript versions of BCL methods that already contain usable MSIL.
- Using JavaScript built-ins when their semantics differ from .NET merely because they are convenient.
- Adding stubs, permissive fallbacks, placeholder identifiers, or tests that assert less to obtain a green build.
- Implementing unmanaged pointers or function pointers without a coherent address-space and memory model.
- Building a general-purpose addressable-storage model for address-producing, indirect-access, or typed-reference MSIL instructions. Fail closed when an opcode requires such a model instead of approximating an address with a JavaScript value.
- Extracting, disassembling, or emulating arbitrary x86/x64 native implementations.
- Emulating unavailable Windows, file-system, peripheral, or desktop UI APIs in the browser without an explicitly approved browser abstraction.
- Supporting WinForms, WebForms, Razor, database connections, or non-web security concepts as part of the compiler core.
- Reproducing non-invariant culture data or locale-specific formatting. The
  standard-type formatting program targets invariant culture only.
- Editing generated files under output or demo destination directories as if they were source files.
- Attaching the managed browser debugger after managed entry-point execution has begun. Debug mode is selected and configured before managed execution and remains fixed for the page lifetime.

## Decision test

Before adding a runtime override, answer these questions in order:

1. Does the method have MSIL? Compile it, unless it is an explicitly approved
   low-level BCL runtime abstraction whose implementation is coupled to CLR
   intrinsics or private runtime layout. Such abstractions may use a
   package-neutral CoreLib wrapper with JavaScript-native storage; they must keep
   the managed public/interface contract and must not become package-specific
   emulations.
2. Does it require a fundamental browser or JavaScript event-loop adaptation? A minimal runtime override may be appropriate.
3. Is it an unavailable operating-system API? Fail with `NotSupportedException`.
4. Is it any other IL-less runtime, internal-call, intrinsic, or native method? Fail closed with a clear `NotSupportedException`.

Test success alone never justifies violating this decision order.

## Native-code policy

- Detect native and IL-less methods through method metadata rather than a hard-coded method-name list.
- General-purpose x86/x64 extraction, disassembly, and emulation are outside this project's current scope.
- Do not treat a PE method RVA as a universal source of executable code: runtime internal calls and JIT intrinsics may have no native implementation in the managed assembly.
- For P/Invoke into unavailable Windows or operating-system facilities, fail with `NotSupportedException`.
- Only introduce a browser-compatible alternative when the operation is an essential environment boundary, such as console output.

## Runtime invariants

- The global namespace for emitted types and methods is `globalThis.DotNet`.
- Wrapped values carry explicit .NET type identity.
- Overloaded calls carry their parameter type signatures.
- Dependency discovery recursively includes required MSIL rather than relying on unrelated preloaded code.
- Instruction-by-instruction execution must preserve the possibility of cooperative scheduling and future WebWorker-based execution.

