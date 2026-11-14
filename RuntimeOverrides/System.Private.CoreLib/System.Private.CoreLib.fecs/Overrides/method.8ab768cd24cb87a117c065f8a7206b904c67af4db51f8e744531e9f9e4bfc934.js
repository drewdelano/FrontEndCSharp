import { createContinuationTask, noCancellationToken, noContinuationOptions } from '../runtime-assets/System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith<TResult>(System.Func`2[T, TResult], System.Threading.Tasks.TaskScheduler)'] = function (ctx, $this, continuation, scheduler) {
    return createContinuationTask(ctx, $this, continuation, [$this], noCancellationToken, noContinuationOptions, scheduler, 'System.Threading.Tasks.Task`1[TResult]', true, 'continuationFunction');
};
