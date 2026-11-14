import { getValueTaskAwaiterResult } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.ValueTaskAwaiter.GetResult()'] = function (ctx, awaiter) {
    getValueTaskAwaiterResult(ctx, awaiter, false);
};
