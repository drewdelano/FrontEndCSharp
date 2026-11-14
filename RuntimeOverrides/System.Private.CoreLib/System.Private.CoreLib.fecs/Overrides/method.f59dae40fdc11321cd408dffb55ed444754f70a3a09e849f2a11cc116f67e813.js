import { getManualResetValueTaskSourceResult } from '../runtime-assets/System.Private.CoreLib.fecs/async-iterator.js';
globalThis.DotNet.Methods['System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1[TResult].GetResult(System.Int16)'] = function (ctx, core, token) {
    return getManualResetValueTaskSourceResult(ctx, core, token);
};
