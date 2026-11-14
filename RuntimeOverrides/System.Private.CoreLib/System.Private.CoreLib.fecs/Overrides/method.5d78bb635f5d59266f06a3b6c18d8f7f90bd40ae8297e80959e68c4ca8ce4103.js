import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Replace(System.String, System.String)"] = function (ctx, thisArg, oldValue, newValue) {
    const s = unwrap(thisArg);
    const oldVal = unwrap(oldValue);
    const newVal = unwrap(newValue);
    return {
        t: 'System.String',
        v: s.split(oldVal).join(newVal)
    };
};
