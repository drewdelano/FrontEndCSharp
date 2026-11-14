import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Atan(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.atan(d), 'System.Double');
};
