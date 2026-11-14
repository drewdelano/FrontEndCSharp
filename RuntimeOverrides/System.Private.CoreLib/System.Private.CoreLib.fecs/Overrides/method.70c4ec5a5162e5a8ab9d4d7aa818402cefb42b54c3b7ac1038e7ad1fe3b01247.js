import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.ToLower()"] = function (ctx, thisArg) {
    const s = unwrap(thisArg);
    return {
        t: 'System.String',
        v: s.toLowerCase()
    };
};
