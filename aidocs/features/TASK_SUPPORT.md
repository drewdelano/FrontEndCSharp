# Task Support

This document records the Task, ValueTask, and compiler-lowered asynchronous behavior
supported by the JavaScript runtime and the remaining architectural surface. Concrete
unfinished work belongs in [`../TODO.md`](../TODO.md).

## Supported compiler-lowering contract

The runtime supports the paths emitted by the C# compiler for ordinary `async Task` and
`async Task<TResult>` methods:

- Builder creation, state-machine start, Task retrieval, safe and unsafe await continuation,
  successful completion, and exceptional completion.
- `TaskAwaiter` and `TaskAwaiter<TResult>` creation, completion checks, safe and unsafe
  continuation registration, and result retrieval.
- Custom `INotifyCompletion` awaiters through `AwaitOnCompleted` and `OnCompleted`.
- Boolean and `ConfigureAwaitOptions` configuration for Task and Task<TResult>, including
  configured awaiter completion, continuation, fault, result, forced yielding, suppressed
  non-generic faults, and flag validation.
- Synchronous completion before an await observes the Task.
- Asynchronous continuation through `Task.Delay` and `Task.Yield`.
- The `Int32` and `TimeSpan` `Task.Delay` overloads, with and without
  `CancellationToken`: immediate zero completion, permanently pending `-1`,
  event-loop-backed positive delays, .NET timeout bounds, pre-cancellation, and
  cancellation during a pending delay.
- Browser event-loop `System.Threading.Timer` support for the callback/state `Int32`
  constructor, one-shot and periodic delivery, `Change(Int32, Int32)`, and `Dispose`.
  Timer callbacks are serialized by the JavaScript event loop rather than CLR thread-pool
  workers, and stale schedules cannot revive a changed or disposed timer.
- Preservation of the original wrapped .NET exception through Task awaiters and the
  JavaScript Promise returned by `waitForTask`.
- A single terminal transition with asynchronous, exactly-once continuation dispatch.
- `CompletedTask`, `FromResult`, and generic and non-generic `FromException` factories.
- `Status`, `IsCompleted`, `IsCompletedSuccessfully`, `IsFaulted`, and `IsCanceled`, plus
  successful result access, managed aggregate inspection through `Task.Exception`, and exact
  faulted and canceled `Task<TResult>.Result` aggregation.
- Array, array-backed enumerable, and two-task `WhenAll` and `WhenAny` composition, including
  ordered generic results, retention of every fault, and managed empty/null-element validation.
- JavaScript-scheduled `Task.Run` overloads and every public `ContinueWith` overload on
  `Task` and `Task<TResult>` for managed method-group delegates, including state,
  pre-cancellation, status filters, `ExecuteSynchronously`, and the default scheduler.
- Debug class and optimized struct async state machines. The JavaScript builder retains and
  resumes the same managed state-machine reference, so the executed `Start`, `MoveNext`, and
  await-continuation paths do not require the CLR builder's `SetStateMachine` boxing handoff.
- Compiler-generated async iterators and `await foreach`, including
  `AsyncIteratorMethodBuilder`, generic and non-generic value-task-source paths,
  source-backed `ValueTask` awaiters, ordered asynchronous yields, early disposal,
  exception propagation, and the direct `[EnumeratorCancellation]` token flow.
  Roslyn's statically present linked-token fallback fails closed with
  `PlatformNotSupportedException`; linked tokens, public token registration, and
  `CancelAfter` are not implemented by this slice.
- Managed `ArgumentNullException` validation for null arguments to the supported Task
  factories, combinators, and continuation methods.

General cancellation behavior beyond `Task.Delay` and the explicitly listed direct
async-enumeration flow remains intentionally separate from this inventory.

## Task extension policy

The public .NET 8 reference surface targeted by this repository does not contain span-based
`WhenAll` or `WhenAny` overloads; revisit those only with a target-framework upgrade that
exposes them.

Methods with usable MSIL should continue through the MSIL pipeline. A runtime override is
appropriate only where the JavaScript event loop or the shared JavaScript Task
representation creates a genuine environment boundary.

The compiler runtime owns only that universal Task identity and entry-point bridge:
the task-to-completion-source registry, `waitForTask`, and the JavaScript exception
used to cross into the event loop. `System.Private.CoreLib.fecs` owns Task builders,
awaiters, combinators, cancellation, continuations, enumeration, validation, ValueTask,
and async-iterator helpers. Those companion assets are emitted only when a used override
statically imports them.
