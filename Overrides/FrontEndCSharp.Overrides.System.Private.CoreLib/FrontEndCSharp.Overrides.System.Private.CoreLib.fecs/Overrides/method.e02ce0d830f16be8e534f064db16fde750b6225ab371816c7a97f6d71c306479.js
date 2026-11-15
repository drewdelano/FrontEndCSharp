import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Max(System.Int64, System.Int64)"] = function (ctx, val1, val2) {
    const v1 = unwrap(val1);
    const v2 = unwrap(val2);
    return wrap(v1 > v2 ? v1 : v2, 'System.Int64');
};
