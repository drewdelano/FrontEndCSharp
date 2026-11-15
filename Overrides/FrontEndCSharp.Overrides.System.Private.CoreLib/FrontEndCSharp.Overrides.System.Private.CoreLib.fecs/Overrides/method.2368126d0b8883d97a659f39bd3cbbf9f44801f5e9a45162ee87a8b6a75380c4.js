import { createContinuationTask, defaultTaskScheduler, noCancellationToken } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task`1[TResult].ContinueWith<TNewResult>(System.Func`3[T1, T2, TResult], System.Object, System.Threading.Tasks.TaskContinuationOptions)'] = function (ctx, $this, continuation, state, continuationOptions) {
    return createContinuationTask(ctx, $this, continuation, [$this, state], noCancellationToken, continuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task`1[TResult]', true, 'continuationFunction');
};
