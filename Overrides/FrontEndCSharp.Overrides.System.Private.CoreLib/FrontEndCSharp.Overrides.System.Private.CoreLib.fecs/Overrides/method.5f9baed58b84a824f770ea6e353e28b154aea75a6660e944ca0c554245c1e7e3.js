import { initializeValueTaskFromResult } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Threading.Tasks.ValueTask`1[TResult]..ctor(TResult)'] = function (ctx, valueTask, result) {
    initializeValueTaskFromResult(ctx, valueTask, result);
};
