import { deRef } from '../msil-type-helpers.js';
import { registerSafeAwaiterContinuation } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';
import { createAsyncContinuationContext } from '../managed-debugger-hooks.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)'] = function (ctx, $this, awaiter, sm) {
    const awaiterValue = deRef(awaiter);
    const stateMachineReference = deRef(sm);
    const stateMachine = deRef(stateMachineReference);
    const continuationContext = createAsyncContinuationContext(ctx);

    registerSafeAwaiterContinuation(ctx, awaiterValue, () => {
        globalThis.DotNet.Methods[`${stateMachine.t}.MoveNext()`](continuationContext, stateMachineReference);
    });
};
