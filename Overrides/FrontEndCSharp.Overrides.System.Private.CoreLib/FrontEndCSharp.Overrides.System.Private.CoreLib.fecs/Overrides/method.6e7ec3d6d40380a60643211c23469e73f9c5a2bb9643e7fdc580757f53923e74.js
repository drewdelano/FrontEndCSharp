import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Text.StringBuilder.Append(System.Char)'] = function (_ctx, builder, value) {
    unwrap(builder).$text += String.fromCharCode(Number(unwrap(value)));
    return builder;
};
