import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Math.Abs(System.Int64)"] = function (ctx, value) {
    const i = unwrap(value);
    if (i === -9223372036854775808n) {
        throw new Error('OverflowException: Negating the minimum value of a twos complement number is invalid.');
    }
    return wrap(i < 0n ? -i : i, 'System.Int64');
};
