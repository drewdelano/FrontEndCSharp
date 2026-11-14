import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Log10(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.log10(d), 'System.Double');
};
