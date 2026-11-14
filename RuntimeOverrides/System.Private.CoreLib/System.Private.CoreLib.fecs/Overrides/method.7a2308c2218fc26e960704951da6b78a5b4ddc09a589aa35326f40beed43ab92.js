import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted(System.String)'] = function (_ctx, handler, value) {
    unwrap(handler).$parts.push(unwrap(value) ?? '');
};
