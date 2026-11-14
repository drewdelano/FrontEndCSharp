import { createContinuationTask, defaultTaskScheduler, noCancellationToken, noContinuationOptions } from '../runtime-assets/System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task`1[TResult].ContinueWith<TNewResult>(System.Func`2[T, TResult])'] = function (ctx, $this, continuation) {
    return createContinuationTask(ctx, $this, continuation, [$this], noCancellationToken, noContinuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task`1[TResult]', true, 'continuationFunction');
};
