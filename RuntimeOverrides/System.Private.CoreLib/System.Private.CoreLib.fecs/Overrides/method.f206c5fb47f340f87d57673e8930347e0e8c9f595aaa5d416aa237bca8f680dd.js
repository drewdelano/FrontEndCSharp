import { deRef, wrap, unwrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.TaskAwaiter.get_IsCompleted()'] = function (ctx, $this) {
    const taskAwaiter = unwrap(deRef($this));
    const source = getTaskCompletionSource(unwrap(taskAwaiter.task));
    return wrap(source.isCompleted, "System.Boolean");
};
