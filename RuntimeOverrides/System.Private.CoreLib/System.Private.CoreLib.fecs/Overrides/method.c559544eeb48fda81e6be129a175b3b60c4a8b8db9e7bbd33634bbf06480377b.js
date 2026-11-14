import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Asinh(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.asinh(d), 'System.Double');
};
