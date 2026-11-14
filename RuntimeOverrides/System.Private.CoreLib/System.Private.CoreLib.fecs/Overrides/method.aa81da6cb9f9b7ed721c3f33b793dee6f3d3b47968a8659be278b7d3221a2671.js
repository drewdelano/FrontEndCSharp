import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.IndexOf(System.Char)"] = function (ctx, thisArg, value) {
    const s = unwrap(thisArg);
    const c = unwrap(value);
    return {
        t: 'System.Int32',
        v: s.indexOf(c)
    };
};
