import { deRef, wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1[TResult].GetAwaiter()'] = function (ctx, $this) {
    return wrap(
        unwrap(deRef($this)),
        'System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter[TResult]');
};
