import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Truncate(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.trunc(d), 'System.Double');
};
