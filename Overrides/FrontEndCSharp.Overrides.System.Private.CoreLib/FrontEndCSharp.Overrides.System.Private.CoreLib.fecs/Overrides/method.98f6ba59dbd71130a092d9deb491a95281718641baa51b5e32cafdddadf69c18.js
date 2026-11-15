import { createContinuationTask, defaultTaskScheduler, noCancellationToken, noContinuationOptions } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith<TResult>(System.Func`3[T1, T2, TResult], System.Object)'] = function (ctx, $this, continuation, state) {
    return createContinuationTask(ctx, $this, continuation, [$this, state], noCancellationToken, noContinuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task`1[TResult]', true, 'continuationFunction');
};
