import { getManualResetValueTaskSourceStatus } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/async-iterator.js';
globalThis.DotNet.Methods['System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1[TResult].GetStatus(System.Int16)'] = function (ctx, core, token) {
    return getManualResetValueTaskSourceStatus(ctx, core, token);
};
