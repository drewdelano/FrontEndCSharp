import { createWhenAllTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';
import { requireNonNullTaskArgument, requireTaskCollectionArgument } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-argument-validation.js';
import { materializeTaskEnumerable } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-enumeration.js';


globalThis.DotNet.Methods['System.Threading.Tasks.Task.WhenAll<TResult>(System.Collections.Generic.IEnumerable`1[T])'] = function (ctx, tasks) {
    requireNonNullTaskArgument(ctx, tasks, 'tasks');
    const taskList = materializeTaskEnumerable(ctx, tasks);
    requireTaskCollectionArgument(ctx, taskList, true);
    return createWhenAllTask(
        taskList,
        'System.Threading.Tasks.Task`1[TResult]',
        'TResult[]');
};
