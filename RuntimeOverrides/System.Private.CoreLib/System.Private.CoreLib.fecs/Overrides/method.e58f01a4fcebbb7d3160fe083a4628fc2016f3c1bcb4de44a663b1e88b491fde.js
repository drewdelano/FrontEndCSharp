import { wrap, unwrap } from '../msil-type-helpers.js';
import { throwManagedException } from '../managed-exceptions.js';
import "./type.System.ArgumentOutOfRangeException.js";
globalThis.DotNet.Methods['System.Threading.Tasks.Task`1[TResult].ConfigureAwait(System.Threading.Tasks.ConfigureAwaitOptions)'] = function (ctx, $this, options) {
    const flags = unwrap(options);
    if (!Number.isInteger(flags) || (flags & ~5) !== 0) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            'Task<TResult>.ConfigureAwait does not support the requested ConfigureAwaitOptions flags.');
    }

    return wrap({
        task: $this,
        continueOnCapturedContext: (flags & 1) !== 0,
        suppressThrowing: false,
        forceYielding: (flags & 4) !== 0
    }, 'System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1[TResult]');
};
