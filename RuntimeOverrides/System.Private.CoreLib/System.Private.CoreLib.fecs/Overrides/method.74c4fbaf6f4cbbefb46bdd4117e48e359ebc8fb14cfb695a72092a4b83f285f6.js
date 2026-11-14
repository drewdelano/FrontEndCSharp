import { initializeTimer } from '../runtime-assets/System.Private.CoreLib.fecs/managed-timer.js';

globalThis.DotNet.Methods['System.Threading.Timer..ctor(System.Threading.TimerCallback, System.Object, System.Int32, System.Int32)'] = function (ctx, $this, callback, state, dueTime, period) {
    initializeTimer(ctx, $this, callback, state, dueTime, period);
};
