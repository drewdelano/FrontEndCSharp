import { createIntegerDelay } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-delay.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.Delay(System.Int32, System.Threading.CancellationToken)'] = function (ctx, delay, cancellationToken) {
    return createIntegerDelay(ctx, delay, cancellationToken);
};
