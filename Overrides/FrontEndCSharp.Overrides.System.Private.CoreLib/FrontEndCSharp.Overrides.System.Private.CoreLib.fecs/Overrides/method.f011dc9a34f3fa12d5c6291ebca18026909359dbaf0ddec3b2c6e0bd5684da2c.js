import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Sqrt(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.sqrt(d), 'System.Double');
};
