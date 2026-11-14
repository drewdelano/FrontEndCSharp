import { setManualResetValueTaskSourceResult } from '../runtime-assets/System.Private.CoreLib.fecs/async-iterator.js';
globalThis.DotNet.Methods['System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1[TResult].SetResult(TResult)'] = function (_ctx, core, result) {
    setManualResetValueTaskSourceResult(core, result);
};
