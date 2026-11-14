import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.get_Length()"] = function (ctx, thisArg) {
    const s = unwrap(thisArg);

    return {
        t: 'System.Int32',
        v: s.length
    };
};