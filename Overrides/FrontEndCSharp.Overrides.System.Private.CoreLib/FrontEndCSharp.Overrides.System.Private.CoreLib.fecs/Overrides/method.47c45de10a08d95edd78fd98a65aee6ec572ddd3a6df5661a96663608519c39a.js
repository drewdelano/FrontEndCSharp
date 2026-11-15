import { createContinuationTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith<TResult>(System.Func`3[T1, T2, TResult], System.Object, System.Threading.CancellationToken, System.Threading.Tasks.TaskContinuationOptions, System.Threading.Tasks.TaskScheduler)'] = function (ctx, $this, continuation, state, cancellationToken, continuationOptions, scheduler) {
    return createContinuationTask(ctx, $this, continuation, [$this, state], cancellationToken, continuationOptions, scheduler, 'System.Threading.Tasks.Task`1[TResult]', true, 'continuationFunction');
};
