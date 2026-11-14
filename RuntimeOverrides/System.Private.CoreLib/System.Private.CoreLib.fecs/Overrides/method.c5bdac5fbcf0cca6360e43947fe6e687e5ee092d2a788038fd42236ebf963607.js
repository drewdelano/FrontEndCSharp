import { createContinuationTask, defaultTaskScheduler, noContinuationOptions } from '../runtime-assets/System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith(System.Action`1[T], System.Threading.CancellationToken)'] = function (ctx, $this, continuation, cancellationToken) {
    return createContinuationTask(ctx, $this, continuation, [$this], cancellationToken, noContinuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task', false, 'continuationAction');
};
