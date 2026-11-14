import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.PadLeft(System.Int32)"] = function (ctx, thisArg, totalWidth) {
    const s = unwrap(thisArg);
    const width = unwrap(totalWidth);
    
    return {
        t: 'System.String',
        v: s.padStart(width)
    };
};
