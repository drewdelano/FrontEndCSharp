import { deRef } from '../msil-type-helpers.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start<TStateMachine>(TStateMachine&)'] = function (ctx, $this, sm) {
    const stateMachine = deRef(sm);
    globalThis.DotNet.Methods[`${stateMachine.t}.MoveNext()`](ctx, sm);
};
