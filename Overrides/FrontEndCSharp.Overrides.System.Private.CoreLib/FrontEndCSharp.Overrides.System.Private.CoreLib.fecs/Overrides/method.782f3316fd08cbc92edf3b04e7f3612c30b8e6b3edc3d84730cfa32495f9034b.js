import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Exp(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.exp(d), 'System.Double');
};
