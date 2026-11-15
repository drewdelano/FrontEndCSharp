import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Concat(System.Object)"] = function (ctx, arg0) {
    const val0 = unwrap(arg0);
    return {
        t: 'System.String',
        v: String(val0)
    };
};
