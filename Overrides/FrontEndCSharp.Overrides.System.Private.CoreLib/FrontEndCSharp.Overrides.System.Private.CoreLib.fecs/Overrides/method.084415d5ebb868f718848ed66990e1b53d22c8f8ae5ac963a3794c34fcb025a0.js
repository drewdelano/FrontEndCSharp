import { createContinuationTask, defaultTaskScheduler, noContinuationOptions } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task`1[TResult].ContinueWith(System.Action`1[T], System.Threading.CancellationToken)'] = function (ctx, $this, continuation, cancellationToken) {
    return createContinuationTask(ctx, $this, continuation, [$this], cancellationToken, noContinuationOptions, defaultTaskScheduler, 'System.Threading.Tasks.Task', false, 'continuationAction');
};
