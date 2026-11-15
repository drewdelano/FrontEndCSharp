import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Log(System.Double, System.Double)"] = function (ctx, a, newBase) {
    const aVal = unwrap(a);
    const baseVal = unwrap(newBase);
    return wrap(Math.log(aVal) / Math.log(baseVal), 'System.Double');
};
