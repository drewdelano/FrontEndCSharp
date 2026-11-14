import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Text.StringBuilder.Append(System.String)'] = function (_ctx, builder, value) {
    unwrap(builder).$text += unwrap(value) ?? '';
    return builder;
};
