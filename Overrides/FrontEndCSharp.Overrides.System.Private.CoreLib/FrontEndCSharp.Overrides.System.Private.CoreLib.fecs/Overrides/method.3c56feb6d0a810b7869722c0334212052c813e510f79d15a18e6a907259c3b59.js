import { wrap } from '../msil-type-helpers.js';
import { isCancellationRequested } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-cancellation.js';

globalThis.DotNet.Methods['System.Threading.CancellationToken.get_IsCancellationRequested()'] = function (ctx, $this) {
    return wrap(isCancellationRequested($this), 'System.Boolean');
};
