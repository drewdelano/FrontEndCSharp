import { getValueTaskAwaiter } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Threading.Tasks.ValueTask.GetAwaiter()'] = function (ctx, valueTask) {
    return getValueTaskAwaiter(ctx, valueTask, false);
};
