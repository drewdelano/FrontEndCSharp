import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Abs(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.abs(d), 'System.Double');
};
