import { isValueTaskAwaiterCompleted } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.ValueTaskAwaiter`1[TResult].get_IsCompleted()'] = function (ctx, awaiter) {
    return isValueTaskAwaiterCompleted(ctx, awaiter);
};
