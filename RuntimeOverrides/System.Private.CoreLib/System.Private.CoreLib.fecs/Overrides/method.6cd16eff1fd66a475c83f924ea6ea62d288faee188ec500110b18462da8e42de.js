import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Min(System.Double, System.Double)"] = function (ctx, val1, val2) {
    const v1 = unwrap(val1);
    const v2 = unwrap(val2);
    return wrap(Math.min(v1, v2), 'System.Double');
};
