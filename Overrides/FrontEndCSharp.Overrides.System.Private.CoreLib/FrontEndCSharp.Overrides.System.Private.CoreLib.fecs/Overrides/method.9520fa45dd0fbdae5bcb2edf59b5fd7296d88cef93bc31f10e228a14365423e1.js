import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Concat(System.Object[])"] = function (ctx, args) {
    const arr = unwrap(args);
    const unwrappedArr = arr.map(x => String(unwrap(x)));
    return {
        t: 'System.String',
        v: unwrappedArr.join('')
    };
};
