import { getValueTaskAwaiterResult } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.ValueTaskAwaiter`1[TResult].GetResult()'] = function (ctx, awaiter) {
    return getValueTaskAwaiterResult(ctx, awaiter, true);
};
