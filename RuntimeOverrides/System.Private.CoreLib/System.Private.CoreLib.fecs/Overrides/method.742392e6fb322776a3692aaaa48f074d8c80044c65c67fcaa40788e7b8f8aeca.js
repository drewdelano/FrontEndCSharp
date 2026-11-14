import { awaitAsyncIteratorSafe } from '../runtime-assets/System.Private.CoreLib.fecs/async-iterator.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.AwaitOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)'] = function (ctx, _builder, awaiter, stateMachine) {
    awaitAsyncIteratorSafe(ctx, awaiter, stateMachine);
};
