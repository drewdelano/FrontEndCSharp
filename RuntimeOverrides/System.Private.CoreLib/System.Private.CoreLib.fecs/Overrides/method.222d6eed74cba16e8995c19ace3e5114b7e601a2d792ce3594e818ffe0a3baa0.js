import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Sign(System.Single)"] = function (ctx, value) {
    const f = unwrap(value);
    return wrap(Math.sign(f), 'System.Int32');
};
