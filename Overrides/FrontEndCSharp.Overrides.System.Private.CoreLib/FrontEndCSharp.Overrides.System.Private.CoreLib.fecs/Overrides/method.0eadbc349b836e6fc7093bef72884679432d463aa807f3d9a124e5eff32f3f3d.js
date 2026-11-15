import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.IndexOf(System.String)"] = function (ctx, thisArg, value) {
    const s = unwrap(thisArg);
    const val = unwrap(value);
    return {
        t: 'System.Int32',
        v: s.indexOf(val)
    };
};
