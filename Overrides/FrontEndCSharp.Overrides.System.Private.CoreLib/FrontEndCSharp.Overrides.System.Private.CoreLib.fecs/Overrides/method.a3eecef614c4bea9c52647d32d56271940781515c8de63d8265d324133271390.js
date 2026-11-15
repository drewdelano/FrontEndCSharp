import { createWhenAnyTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';
import { requireTaskCollectionArgument } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-argument-validation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.WhenAny(System.Threading.Tasks.Task[])'] = function (ctx, tasks) {
    requireTaskCollectionArgument(ctx, tasks, false);
    return createWhenAnyTask(tasks, 'System.Threading.Tasks.Task`1[TResult]');
};
