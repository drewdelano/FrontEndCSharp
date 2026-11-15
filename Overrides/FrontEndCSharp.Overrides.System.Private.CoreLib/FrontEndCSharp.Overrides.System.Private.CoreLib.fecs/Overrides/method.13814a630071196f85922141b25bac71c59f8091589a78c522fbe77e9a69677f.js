import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.CompareTo(System.String)"] = function (ctx, thisArg, strB) {
    const a = unwrap(thisArg);
    const b = unwrap(strB);
    
    if (a === b) return { t: 'System.Int32', v: 0 };
    if (b === null) return { t: 'System.Int32', v: 1 }; // CompareTo(null) returns > 0
    
    return {
        t: 'System.Int32',
        v: a.localeCompare(b)
    };
};
