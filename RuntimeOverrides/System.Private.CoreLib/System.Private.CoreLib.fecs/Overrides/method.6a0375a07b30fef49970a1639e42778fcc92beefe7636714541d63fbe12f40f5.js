import { onManualResetValueTaskSourceCompleted } from '../runtime-assets/System.Private.CoreLib.fecs/async-iterator.js';
globalThis.DotNet.Methods['System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1[TResult].OnCompleted(System.Action`1[T], System.Object, System.Int16, System.Threading.Tasks.Sources.ValueTaskSourceOnCompletedFlags)'] = function (ctx, core, continuation, state, token) {
    onManualResetValueTaskSourceCompleted(ctx, core, continuation, state, token);
};
