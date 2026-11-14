import { createIntegerDelay } from '../runtime-assets/System.Private.CoreLib.fecs/task-delay.js';
globalThis.DotNet.Methods['System.Threading.Tasks.Task.Delay(System.Int32)'] = function (ctx, delayMs) {
    return createIntegerDelay(ctx, delayMs);
};
