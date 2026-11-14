import { createContinuationTask, defaultTaskScheduler, noCancellationToken } from '../runtime-assets/System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith<TResult>(System.Func`2[T, TResult], System.Threading.Tasks.TaskContinuationOptions)'] = function (ctx, $this, continuation, continuationOptions) {
    return createContinuationTask(ctx, $this, continuation, [$this], noCancellationToken, continuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task`1[TResult]', true, 'continuationFunction');
};
