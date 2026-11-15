import { createWhenAllTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';
import { requireTaskCollectionArgument } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-argument-validation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.WhenAll(System.Threading.Tasks.Task[])'] = function (ctx, tasks) {
    requireTaskCollectionArgument(ctx, tasks, true);
    return createWhenAllTask(tasks, 'System.Threading.Tasks.Task');
};
