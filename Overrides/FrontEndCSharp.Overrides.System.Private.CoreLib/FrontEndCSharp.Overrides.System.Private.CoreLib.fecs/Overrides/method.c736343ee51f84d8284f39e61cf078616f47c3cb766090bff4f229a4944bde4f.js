import { awaitAsyncIteratorUnsafe } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/async-iterator.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)'] = function (ctx, _builder, awaiter, stateMachine) {
    awaitAsyncIteratorUnsafe(ctx, awaiter, stateMachine);
};
