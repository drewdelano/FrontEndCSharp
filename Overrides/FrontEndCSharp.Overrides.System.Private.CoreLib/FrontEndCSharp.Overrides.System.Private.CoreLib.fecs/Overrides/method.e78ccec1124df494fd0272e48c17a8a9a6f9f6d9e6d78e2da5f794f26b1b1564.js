import { createContinuationTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task`1[TResult].ContinueWith<TNewResult>(System.Func`2[T, TResult], System.Threading.CancellationToken, System.Threading.Tasks.TaskContinuationOptions, System.Threading.Tasks.TaskScheduler)'] = function (ctx, $this, continuation, cancellationToken, continuationOptions, scheduler) {
    return createContinuationTask(ctx, $this, continuation, [$this], cancellationToken, continuationOptions, scheduler, 'System.Threading.Tasks.Task`1[TResult]', true, 'continuationFunction');
};
