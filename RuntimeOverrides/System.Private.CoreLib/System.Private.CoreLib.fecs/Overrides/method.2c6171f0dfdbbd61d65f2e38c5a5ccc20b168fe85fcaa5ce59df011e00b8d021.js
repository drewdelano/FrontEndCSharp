import { createWhenAllTask } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';
import { requireTaskCollectionArgument } from '../runtime-assets/System.Private.CoreLib.fecs/task-argument-validation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.WhenAll<TResult>(System.Threading.Tasks.Task`1[TResult][])'] = function (ctx, tasks) {
    requireTaskCollectionArgument(ctx, tasks, true);
    return createWhenAllTask(
        tasks,
        'System.Threading.Tasks.Task`1[TResult]',
        'TResult[]');
};
