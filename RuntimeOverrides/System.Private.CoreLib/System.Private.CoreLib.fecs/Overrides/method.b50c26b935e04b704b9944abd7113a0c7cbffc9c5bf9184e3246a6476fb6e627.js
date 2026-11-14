import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Text.StringBuilder.ToString()'] = function (_ctx, builder) {
    return wrap(unwrap(builder).$text, 'System.String');
};
