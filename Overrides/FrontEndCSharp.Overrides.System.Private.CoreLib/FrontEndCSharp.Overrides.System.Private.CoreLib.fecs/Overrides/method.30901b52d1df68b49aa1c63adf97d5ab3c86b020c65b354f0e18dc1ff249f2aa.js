import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear()'] = function (_ctx, handler) {
    const state = unwrap(handler);
    const result = state.$parts.join('');
    state.$parts.length = 0;
    return wrap(result, 'System.String');
};
