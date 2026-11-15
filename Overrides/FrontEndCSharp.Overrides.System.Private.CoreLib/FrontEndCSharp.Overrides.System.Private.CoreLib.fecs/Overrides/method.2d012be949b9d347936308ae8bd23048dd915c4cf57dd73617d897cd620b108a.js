import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Log2(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.log2(d), 'System.Double');
};
