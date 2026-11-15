import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Compare(System.String, System.String, System.Boolean)"] = function (ctx, strA, strB, ignoreCase) {
    let a = unwrap(strA);
    let b = unwrap(strB);
    const ignore = unwrap(ignoreCase);
    
    if (a === b) return { t: 'System.Int32', v: 0 };
    if (a === null) return { t: 'System.Int32', v: -1 };
    if (b === null) return { t: 'System.Int32', v: 1 };
    
    if (ignore) {
        a = a.toLowerCase();
        b = b.toLowerCase();
    }
    
    return {
        t: 'System.Int32',
        v: a.localeCompare(b)
    };
};
