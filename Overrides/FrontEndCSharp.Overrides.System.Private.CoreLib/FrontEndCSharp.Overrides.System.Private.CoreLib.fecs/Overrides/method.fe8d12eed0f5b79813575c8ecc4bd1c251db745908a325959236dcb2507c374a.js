import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Remove(System.Int32)"] = function (ctx, thisArg, startIndex) {
    const s = unwrap(thisArg);
    const idx = unwrap(startIndex);
    
    return {
        t: 'System.String',
        v: s.substring(0, idx)
    };
};
