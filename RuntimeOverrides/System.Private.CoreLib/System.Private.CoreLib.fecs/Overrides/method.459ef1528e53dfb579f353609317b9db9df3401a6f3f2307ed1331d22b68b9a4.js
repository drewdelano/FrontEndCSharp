import { setManualResetValueTaskSourceException } from '../runtime-assets/System.Private.CoreLib.fecs/async-iterator.js';
globalThis.DotNet.Methods['System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1[TResult].SetException(System.Exception)'] = function (_ctx, core, exception) {
    setManualResetValueTaskSourceException(core, exception);
};
