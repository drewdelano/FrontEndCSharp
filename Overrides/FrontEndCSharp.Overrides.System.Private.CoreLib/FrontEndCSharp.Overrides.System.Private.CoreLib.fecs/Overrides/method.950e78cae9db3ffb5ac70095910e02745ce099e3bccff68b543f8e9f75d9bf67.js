import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Substring(System.Int32, System.Int32)"] = function (ctx, thisArg, startIndex, length) {
    const s = unwrap(thisArg);
    const start = unwrap(startIndex);
    const len = unwrap(length);
    return {
        t: 'System.String',
        v: s.substring(start, start + len)
    };
};
