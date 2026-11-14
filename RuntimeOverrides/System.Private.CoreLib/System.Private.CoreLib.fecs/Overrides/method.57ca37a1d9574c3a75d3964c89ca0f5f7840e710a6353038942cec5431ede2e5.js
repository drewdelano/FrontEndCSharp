import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Sign(System.Int64)"] = function (ctx, value) {
    const i = unwrap(value);
    if (i > 0n) return wrap(1, 'System.Int32');
    if (i < 0n) return wrap(-1, 'System.Int32');
    return wrap(0, 'System.Int32');
};
