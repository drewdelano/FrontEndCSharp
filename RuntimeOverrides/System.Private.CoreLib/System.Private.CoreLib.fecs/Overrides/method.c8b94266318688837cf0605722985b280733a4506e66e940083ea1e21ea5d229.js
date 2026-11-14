import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Concat(System.Object, System.Object)"] = function (ctx, arg0, arg1) {
    const val0 = unwrap(arg0);
    const val1 = unwrap(arg1);
    return {
        t: 'System.String',
        v: String(val0) + String(val1)
    };
};
