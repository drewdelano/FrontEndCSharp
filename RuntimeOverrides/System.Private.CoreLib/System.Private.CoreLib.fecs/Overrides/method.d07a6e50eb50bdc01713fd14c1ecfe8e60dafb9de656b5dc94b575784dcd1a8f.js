import { disposeTimer } from '../runtime-assets/System.Private.CoreLib.fecs/managed-timer.js';

globalThis.DotNet.Methods['System.Threading.Timer.Dispose()'] = function (ctx, $this) {
    disposeTimer(ctx, $this);
};
