import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.LastIndexOf(System.String)"] = function (ctx, thisArg, value) {
    const s = unwrap(thisArg);
    const v = unwrap(value);
    return {
        t: 'System.Int32',
        v: s.lastIndexOf(v)
    };
};
