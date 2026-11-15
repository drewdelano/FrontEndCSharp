import { createContinuationTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith(System.Action`1[T], System.Threading.CancellationToken, System.Threading.Tasks.TaskContinuationOptions, System.Threading.Tasks.TaskScheduler)'] = function (ctx, $this, continuation, cancellationToken, continuationOptions, scheduler) {
    return createContinuationTask(ctx, $this, continuation, [$this], cancellationToken, continuationOptions, scheduler, 'System.Threading.Tasks.Task', false, 'continuationAction');
};
