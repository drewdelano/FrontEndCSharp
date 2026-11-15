import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.String.Concat(System.String, System.String, System.String, System.String)'] = function (ctx, value0, value1, value2, value3) {
    return wrap(
        String(unwrap(value0) ?? '') + String(unwrap(value1) ?? '')
            + String(unwrap(value2) ?? '') + String(unwrap(value3) ?? ''),
        'System.String');
};
