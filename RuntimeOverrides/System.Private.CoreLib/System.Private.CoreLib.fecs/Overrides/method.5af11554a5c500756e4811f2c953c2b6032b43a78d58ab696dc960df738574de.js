import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Equals(System.String, System.String)"] = function (ctx, a, b) {
    return {
        t: 'System.Boolean',
        v: unwrap(a) === unwrap(b)
    };
};
