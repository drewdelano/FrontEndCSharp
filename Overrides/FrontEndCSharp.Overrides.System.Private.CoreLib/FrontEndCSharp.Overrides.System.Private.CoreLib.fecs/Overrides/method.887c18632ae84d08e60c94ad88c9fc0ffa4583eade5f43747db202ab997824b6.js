import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Cos(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.cos(d), 'System.Double');
};
