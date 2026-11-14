import { deRef, wrap, unwrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter[TResult].get_IsCompleted()'] = function (ctx, $this) {
    const awaiter = unwrap(deRef($this));
    const source = getTaskCompletionSource(unwrap(awaiter.task));
    return wrap(source.isCompleted && !awaiter.forceYielding, 'System.Boolean');
};
