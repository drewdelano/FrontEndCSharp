import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Remove(System.Int32, System.Int32)"] = function (ctx, thisArg, startIndex, count) {
    const s = unwrap(thisArg);
    const idx = unwrap(startIndex);
    const cnt = unwrap(count);
    
    return {
        t: 'System.String',
        v: s.substring(0, idx) + s.substring(idx + cnt)
    };
};
