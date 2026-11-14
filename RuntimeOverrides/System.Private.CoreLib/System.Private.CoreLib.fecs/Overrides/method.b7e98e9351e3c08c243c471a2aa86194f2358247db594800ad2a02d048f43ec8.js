import { createTimeSpanDelay } from '../runtime-assets/System.Private.CoreLib.fecs/task-delay.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.Delay(System.TimeSpan, System.Threading.CancellationToken)'] = function (ctx, delay, cancellationToken) {
    return createTimeSpanDelay(ctx, delay, cancellationToken);
};
