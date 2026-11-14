import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Insert(System.Int32, System.String)"] = function (ctx, thisArg, startIndex, value) {
    const s = unwrap(thisArg);
    const idx = unwrap(startIndex);
    const val = unwrap(value);
    
    return {
        t: 'System.String',
        v: s.slice(0, idx) + val + s.slice(idx)
    };
};
