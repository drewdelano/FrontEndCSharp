import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Sin(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.sin(d), 'System.Double');
};
