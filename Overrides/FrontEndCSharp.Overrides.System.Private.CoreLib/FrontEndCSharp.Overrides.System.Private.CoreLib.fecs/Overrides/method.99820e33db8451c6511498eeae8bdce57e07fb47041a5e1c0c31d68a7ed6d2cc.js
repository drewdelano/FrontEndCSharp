import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Sign(System.Int32)"] = function (ctx, value) {
    const i = unwrap(value);
    return wrap(Math.sign(i), 'System.Int32');
};
