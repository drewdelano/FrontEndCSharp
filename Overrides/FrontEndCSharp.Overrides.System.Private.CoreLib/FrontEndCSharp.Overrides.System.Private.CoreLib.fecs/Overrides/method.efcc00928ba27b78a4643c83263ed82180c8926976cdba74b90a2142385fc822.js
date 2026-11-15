import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Join(System.String, System.String[])"] = function (ctx, separator, value) {
    const sep = unwrap(separator);
    const arr = unwrap(value); // This is an array of wrapped strings
    
    // Unwrap each element in the array
    const unwrappedArray = arr.map(item => unwrap(item));
    
    return {
        t: 'System.String',
        v: unwrappedArray.join(sep)
    };
};
