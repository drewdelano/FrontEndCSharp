import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Compare(System.String, System.String)"] = function (ctx, strA, strB) {
    const a = unwrap(strA);
    const b = unwrap(strB);
    
    let result = 0;
    if (a === b) result = 0;
    else if (a === null) result = -1;
    else if (b === null) result = 1;
    else result = a.localeCompare(b);
    
    return {
        t: 'System.Int32',
        v: result
    };
};
