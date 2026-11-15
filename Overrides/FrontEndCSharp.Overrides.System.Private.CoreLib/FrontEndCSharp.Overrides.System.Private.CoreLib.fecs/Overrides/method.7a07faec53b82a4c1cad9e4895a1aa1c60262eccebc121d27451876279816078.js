import { createContinuationTask, defaultTaskScheduler, noCancellationToken } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith(System.Action`2[T1, T2], System.Object, System.Threading.Tasks.TaskContinuationOptions)'] = function (ctx, $this, continuation, state, continuationOptions) {
    return createContinuationTask(ctx, $this, continuation, [$this, state], noCancellationToken, continuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task', false, 'continuationAction');
};
