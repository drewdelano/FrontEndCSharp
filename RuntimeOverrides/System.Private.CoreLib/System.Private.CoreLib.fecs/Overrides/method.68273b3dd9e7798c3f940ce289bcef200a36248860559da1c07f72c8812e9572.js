import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Clamp(System.Int32, System.Int32, System.Int32)"] = function (ctx, value, min, max) {
    const v = unwrap(value);
    const minVal = unwrap(min);
    const maxVal = unwrap(max);
    return wrap(Math.min(Math.max(v, minVal), maxVal), 'System.Int32');
};
