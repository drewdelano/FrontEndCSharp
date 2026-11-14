import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Concat(System.String, System.String)"] = function (ctx, s1, s2) {
    const s1unwrapped = unwrap(s1);
    const s2unwrapped = unwrap(s2);

    return {
        t: 'System.String',
        v: s1unwrapped + s2unwrapped
    };
};