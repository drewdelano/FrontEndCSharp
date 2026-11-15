import { getValueTaskAwaiter } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Threading.Tasks.ValueTask`1[TResult].GetAwaiter()'] = function (ctx, valueTask) {
    return getValueTaskAwaiter(ctx, valueTask, true);
};
