import { isValueTaskAwaiterCompleted } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.ValueTaskAwaiter.get_IsCompleted()'] = function (ctx, awaiter) {
    return isValueTaskAwaiterCompleted(ctx, awaiter);
};
