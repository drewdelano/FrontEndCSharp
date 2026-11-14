import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.ToUpper()"] = function (ctx, thisArg) {
    const s = unwrap(thisArg);
    return {
        t: 'System.String',
        v: s.toUpperCase()
    };
};
