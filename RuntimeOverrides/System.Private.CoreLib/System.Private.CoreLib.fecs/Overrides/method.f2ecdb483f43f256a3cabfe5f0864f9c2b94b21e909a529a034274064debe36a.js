import { createContinuationTask, defaultTaskScheduler, noCancellationToken, noContinuationOptions } from '../runtime-assets/System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith(System.Action`2[T1, T2], System.Object)'] = function (ctx, $this, continuation, state) {
    return createContinuationTask(ctx, $this, continuation, [$this, state], noCancellationToken, noContinuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task', false, 'continuationAction');
};
