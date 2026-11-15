import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Acosh(System.Double)"] = function (ctx, value) {
    const d = unwrap(value);
    return wrap(Math.acosh(d), 'System.Double');
};
