import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.ToCharArray()"] = function (ctx, thisArg) {
    const s = unwrap(thisArg);
    
    // Split string into characters and wrap each one
    const chars = s.split('').map(c => wrap(c, 'System.Char'));
    
    return {
        t: 'System.Char[]',
        v: chars
    };
};
