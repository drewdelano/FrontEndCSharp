import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Trim(System.Char)"] = function (ctx, thisArg, trimCharArg) {
    const s = unwrap(thisArg);
    const c = unwrap(trimCharArg);
    
    let start = 0;
    let end = s.length - 1;

    while (start <= end && s[start] === c) {
        start++;
    }

    while (end >= start && s[end] === c) {
        end--;
    }

    return {
        t: 'System.String',
        v: s.substring(start, end + 1)
    };
};
