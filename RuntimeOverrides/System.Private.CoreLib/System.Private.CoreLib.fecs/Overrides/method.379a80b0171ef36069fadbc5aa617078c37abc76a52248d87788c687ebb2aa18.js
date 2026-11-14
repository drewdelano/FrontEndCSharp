import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.PadRight(System.Int32)"] = function (ctx, thisArg, totalWidth) {
    const s = unwrap(thisArg);
    const width = unwrap(totalWidth);
    
    return {
        t: 'System.String',
        v: s.padEnd(width)
    };
};
