import { createContinuationTask, noCancellationToken, noContinuationOptions } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith<TResult>(System.Func`3[T1, T2, TResult], System.Object, System.Threading.Tasks.TaskScheduler)'] = function (ctx, $this, continuation, state, scheduler) {
    return createContinuationTask(ctx, $this, continuation, [$this, state], noCancellationToken, noContinuationOptions, scheduler, 'System.Threading.Tasks.Task`1[TResult]', true, 'continuationFunction');
};
