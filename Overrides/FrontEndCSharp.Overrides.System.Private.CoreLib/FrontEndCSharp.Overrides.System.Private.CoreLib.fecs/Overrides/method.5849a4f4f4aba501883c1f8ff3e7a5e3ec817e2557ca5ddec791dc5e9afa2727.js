import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Abs(System.Single)"] = function (ctx, value) {
    const f = unwrap(value);
    return wrap(Math.fround(Math.abs(f)), 'System.Single');
};
