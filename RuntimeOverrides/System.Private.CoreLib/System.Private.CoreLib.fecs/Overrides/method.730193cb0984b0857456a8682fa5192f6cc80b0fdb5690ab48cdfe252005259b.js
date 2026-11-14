import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Asin(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.asin(d), 'System.Double');
};
