import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Substring(System.Int32)"] = function (ctx, thisArg, startIndex) {
    const s = unwrap(thisArg);
    const start = unwrap(startIndex);
    return {
        t: 'System.String',
        v: s.substring(start)
    };
};
