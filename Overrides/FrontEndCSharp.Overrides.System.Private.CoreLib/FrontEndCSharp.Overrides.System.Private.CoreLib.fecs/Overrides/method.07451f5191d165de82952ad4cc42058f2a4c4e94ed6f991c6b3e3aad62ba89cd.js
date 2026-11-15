import { deRef, throwDotNetException, unwrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.TaskAwaiter.GetResult()'] = function (ctx, $this) {
    const taskAwaiter = unwrap(deRef($this));
    const source = getTaskCompletionSource(unwrap(taskAwaiter.task));
    if (!source.isCompleted) {
        throw new Error("TaskAwaiter.GetResult() cannot run before its Task is complete.");
    }

    if (source.isFaulted || source.isCanceled) {
        throwDotNetException(ctx, source.exception);
    }
};
