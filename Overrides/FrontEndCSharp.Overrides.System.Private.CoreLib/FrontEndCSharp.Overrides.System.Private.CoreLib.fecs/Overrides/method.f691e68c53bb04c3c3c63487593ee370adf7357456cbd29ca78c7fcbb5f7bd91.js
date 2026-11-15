import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.LastIndexOf(System.Char)"] = function (ctx, thisArg, value) {
    const s = unwrap(thisArg);
    const c = unwrap(value);
    return {
        t: 'System.Int32',
        v: s.lastIndexOf(c)
    };
};
