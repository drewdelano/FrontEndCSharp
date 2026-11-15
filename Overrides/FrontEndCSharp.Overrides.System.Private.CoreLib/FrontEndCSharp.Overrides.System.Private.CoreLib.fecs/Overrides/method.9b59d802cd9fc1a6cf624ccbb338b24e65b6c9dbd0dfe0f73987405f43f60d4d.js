import { createContinuationTask, defaultTaskScheduler, noContinuationOptions } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith<TResult>(System.Func`2[T, TResult], System.Threading.CancellationToken)'] = function (ctx, $this, continuation, cancellationToken) {
    return createContinuationTask(ctx, $this, continuation, [$this], cancellationToken, noContinuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task`1[TResult]', true, 'continuationFunction');
};
