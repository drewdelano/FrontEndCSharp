import { awaitAsyncValueTaskMethodBuilderSafe } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.AwaitOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)'] = function (ctx, _builder, awaiter, stateMachine) {
    awaitAsyncValueTaskMethodBuilderSafe(ctx, awaiter, stateMachine);
};
