import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Atan2(System.Double, System.Double)"] = function (ctx, y, x) {
    const yVal = unwrap(y);
    const xVal = unwrap(x);
    return wrap(Math.atan2(yVal, xVal), 'System.Double');
};
