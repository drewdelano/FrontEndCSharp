import { createContinuationTask, defaultTaskScheduler, noContinuationOptions } from '../runtime-assets/System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith(System.Action`2[T1, T2], System.Object, System.Threading.CancellationToken)'] = function (ctx, $this, continuation, state, cancellationToken) {
    return createContinuationTask(ctx, $this, continuation, [$this, state], cancellationToken, noContinuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task', false, 'continuationAction');
};
