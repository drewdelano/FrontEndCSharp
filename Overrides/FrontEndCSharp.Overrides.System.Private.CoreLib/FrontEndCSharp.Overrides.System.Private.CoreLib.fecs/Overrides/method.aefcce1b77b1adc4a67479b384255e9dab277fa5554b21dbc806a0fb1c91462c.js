import { wrap } from '../msil-type-helpers.js';
import { createWhenAnyTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';
import { requireNonNullTaskArgument } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-argument-validation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.WhenAny<TResult>(System.Threading.Tasks.Task`1[TResult], System.Threading.Tasks.Task`1[TResult])'] = function (ctx, task1, task2) {
    requireNonNullTaskArgument(ctx, task1, 'task1');
    requireNonNullTaskArgument(ctx, task2, 'task2');
    return createWhenAnyTask(
        wrap([task1, task2], 'System.Threading.Tasks.Task`1[TResult][]'),
        'System.Threading.Tasks.Task`1[TResult]');
};
