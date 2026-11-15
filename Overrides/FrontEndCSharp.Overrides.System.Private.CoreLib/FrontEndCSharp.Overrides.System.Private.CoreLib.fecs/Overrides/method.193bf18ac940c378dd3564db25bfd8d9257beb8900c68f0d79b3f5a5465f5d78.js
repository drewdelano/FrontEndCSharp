import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Concat(System.String[])"] = function (ctx, values) {
    const arr = unwrap(values);
    const unwrappedArr = arr.map(x => unwrap(x));
    return {
        t: 'System.String',
        v: unwrappedArr.join('')
    };
};
