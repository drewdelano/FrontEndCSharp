import { wrap, unwrap } from '../msil-type-helpers.js';
import { throwManagedException } from '../managed-exceptions.js';
import "./type.System.ArgumentOutOfRangeException.js";
globalThis.DotNet.Methods['System.Threading.Tasks.Task.ConfigureAwait(System.Threading.Tasks.ConfigureAwaitOptions)'] = function (ctx, $this, options) {
    const flags = unwrap(options);
    if (!Number.isInteger(flags) || (flags & ~7) !== 0) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            'ConfigureAwaitOptions contains an unsupported flag.');
    }

    return wrap({
        task: $this,
        continueOnCapturedContext: (flags & 1) !== 0,
        suppressThrowing: (flags & 2) !== 0,
        forceYielding: (flags & 4) !== 0
    }, 'System.Runtime.CompilerServices.ConfiguredTaskAwaitable');
};
