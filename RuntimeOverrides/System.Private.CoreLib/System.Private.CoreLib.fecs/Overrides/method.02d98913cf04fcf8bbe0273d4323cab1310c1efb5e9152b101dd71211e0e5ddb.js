import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Acos(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.acos(d), 'System.Double');
};
