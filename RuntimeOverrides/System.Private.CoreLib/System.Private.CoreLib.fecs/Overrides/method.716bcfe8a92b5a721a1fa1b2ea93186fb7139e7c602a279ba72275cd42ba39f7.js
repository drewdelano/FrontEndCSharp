import { deRef, throwDotNetException, unwrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter[TResult].GetResult()'] = function (ctx, $this) {
    const awaiter = unwrap(deRef($this));
    const source = getTaskCompletionSource(unwrap(awaiter.task));
    if (!source.isCompleted) {
        throw new Error('ConfiguredTaskAwaiter.GetResult() cannot run before its Task is complete.');
    }
    if (source.isFaulted || source.isCanceled) {
        throwDotNetException(ctx, source.exception);
    }
    return source.result;
};
