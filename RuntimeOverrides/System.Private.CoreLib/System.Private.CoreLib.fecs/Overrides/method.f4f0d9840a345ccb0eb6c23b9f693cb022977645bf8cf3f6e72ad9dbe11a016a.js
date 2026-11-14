import { createTimeSpanDelay } from '../runtime-assets/System.Private.CoreLib.fecs/task-delay.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.Delay(System.TimeSpan)'] = function (ctx, delay) {
    return createTimeSpanDelay(ctx, delay);
};
