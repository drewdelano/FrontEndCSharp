import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Abs(System.Int32)"] = function (ctx, value) {
    const i = unwrap(value);
    if (i === -2147483648) {
        throw new Error('OverflowException: Negating the minimum value of a twos complement number is invalid.');
    }
    return wrap(Math.abs(i), 'System.Int32');
};
