import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Pow(System.Double, System.Double)"] = function (ctx, x, y) {
    const xVal = unwrap(x);
    const yVal = unwrap(y);
    return wrap(Math.pow(xVal, yVal), 'System.Double');
};
