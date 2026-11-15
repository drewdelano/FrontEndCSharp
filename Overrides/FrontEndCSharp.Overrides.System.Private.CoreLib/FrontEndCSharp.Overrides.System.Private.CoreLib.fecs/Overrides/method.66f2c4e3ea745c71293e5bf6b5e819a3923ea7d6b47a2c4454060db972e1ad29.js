import { createContinuationTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith(System.Action`2[T1, T2], System.Object, System.Threading.CancellationToken, System.Threading.Tasks.TaskContinuationOptions, System.Threading.Tasks.TaskScheduler)'] = function (ctx, $this, continuation, state, cancellationToken, continuationOptions, scheduler) {
    return createContinuationTask(ctx, $this, continuation, [$this, state], cancellationToken, continuationOptions, scheduler, 'System.Threading.Tasks.Task', false, 'continuationAction');
};
