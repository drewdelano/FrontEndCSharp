import { awaitAsyncValueTaskMethodBuilderUnsafe } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1[TResult].AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)'] = function (ctx, _builder, awaiter, stateMachine) {
    awaitAsyncValueTaskMethodBuilderUnsafe(ctx, awaiter, stateMachine);
};
