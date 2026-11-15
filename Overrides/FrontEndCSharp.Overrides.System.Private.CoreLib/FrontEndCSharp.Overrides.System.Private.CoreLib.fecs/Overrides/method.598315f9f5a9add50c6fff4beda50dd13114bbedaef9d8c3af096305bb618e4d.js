import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Atanh(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.atanh(d), 'System.Double');
};
