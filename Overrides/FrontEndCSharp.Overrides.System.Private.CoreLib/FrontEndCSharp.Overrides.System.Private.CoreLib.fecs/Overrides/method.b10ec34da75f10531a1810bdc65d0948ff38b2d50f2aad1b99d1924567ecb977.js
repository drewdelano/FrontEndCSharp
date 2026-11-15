import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Cbrt(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.cbrt(d), 'System.Double');
};
