import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Tan(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.tan(d), 'System.Double');
};
