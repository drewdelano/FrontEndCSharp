import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task`1[TResult].ConfigureAwait(System.Boolean)'] = function (ctx, $this, continueOnCapturedContext) {
    return wrap({
        task: $this,
        continueOnCapturedContext: unwrap(continueOnCapturedContext),
        forceYielding: false,
        suppressThrowing: false
    }, 'System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1[TResult]');
};
