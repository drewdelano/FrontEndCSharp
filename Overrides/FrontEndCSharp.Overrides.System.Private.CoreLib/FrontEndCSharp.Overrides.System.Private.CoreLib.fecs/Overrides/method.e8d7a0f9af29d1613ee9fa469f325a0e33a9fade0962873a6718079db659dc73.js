import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Floor(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.floor(d), 'System.Double');
};
