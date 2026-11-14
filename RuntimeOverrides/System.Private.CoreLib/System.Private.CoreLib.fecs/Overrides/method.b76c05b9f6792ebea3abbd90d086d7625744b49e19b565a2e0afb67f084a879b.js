import { createContinuationTask, defaultTaskScheduler, noCancellationToken, noContinuationOptions } from '../runtime-assets/System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.ContinueWith(System.Action`1[T])'] = function (ctx, $this, continuation) {
    return createContinuationTask(ctx, $this, continuation, [$this], noCancellationToken, noContinuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task', false, 'continuationAction');
};
