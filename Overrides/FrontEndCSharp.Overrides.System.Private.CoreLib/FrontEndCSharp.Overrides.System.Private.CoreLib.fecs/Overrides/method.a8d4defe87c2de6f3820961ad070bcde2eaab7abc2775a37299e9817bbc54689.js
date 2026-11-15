import { moveNextAsyncIterator } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/async-iterator.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.MoveNext<TStateMachine>(TStateMachine&)'] = function (ctx, _builder, stateMachine) {
    moveNextAsyncIterator(ctx, stateMachine);
};
