import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Trim(System.Char[])"] = function (ctx, thisArg, trimCharsArg) {
    const s = unwrap(thisArg);
    const trimCharsWrapped = unwrap(trimCharsArg);
    
    let charsToTrim = new Set();
    if (trimCharsWrapped && trimCharsWrapped.length > 0) {
        for (let i = 0; i < trimCharsWrapped.length; i++) {
            charsToTrim.add(unwrap(trimCharsWrapped[i]));
        }
    } else {
         return {
            t: 'System.String',
            v: s.trim()
        };
    }

    let start = 0;
    let end = s.length - 1;

    while (start <= end && charsToTrim.has(s[start])) {
        start++;
    }

    while (end >= start && charsToTrim.has(s[end])) {
        end--;
    }

    return {
        t: 'System.String',
        v: s.substring(start, end + 1)
    };
};
