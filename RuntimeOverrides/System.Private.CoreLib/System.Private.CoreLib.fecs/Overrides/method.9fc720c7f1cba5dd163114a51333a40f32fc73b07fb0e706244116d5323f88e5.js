import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.PadRight(System.Int32, System.Char)"] = function (ctx, thisArg, totalWidth, paddingChar) {
    const s = unwrap(thisArg);
    const width = unwrap(totalWidth);
    const char = unwrap(paddingChar);
    
    return {
        t: 'System.String',
        v: s.padEnd(width, char)
    };
};
