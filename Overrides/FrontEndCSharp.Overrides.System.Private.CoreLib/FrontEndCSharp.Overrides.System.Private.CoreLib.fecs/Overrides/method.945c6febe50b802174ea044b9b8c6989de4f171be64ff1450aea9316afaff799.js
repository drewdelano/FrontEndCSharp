import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.StartsWith(System.String)"] = function (ctx, thisArg, value) {
    const s = unwrap(thisArg);
    const val = unwrap(value);
    return {
        t: 'System.Boolean',
        v: s.startsWith(val)
    };
};
