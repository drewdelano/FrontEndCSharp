import { changeTimer } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-timer.js';

globalThis.DotNet.Methods['System.Threading.Timer.Change(System.Int32, System.Int32)'] = function (ctx, $this, dueTime, period) {
    return changeTimer(ctx, $this, dueTime, period);
};
