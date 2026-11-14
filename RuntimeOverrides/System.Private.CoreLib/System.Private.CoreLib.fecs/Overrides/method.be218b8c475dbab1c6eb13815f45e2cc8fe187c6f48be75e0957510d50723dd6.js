import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Tanh(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.tanh(d), 'System.Double');
};
