import { unwrap } from '../msil-type-helpers.js';
import { formatInterpolatedValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted<T>(T, System.String)'] = function (ctx, handler, value, format) {
    unwrap(handler).$parts.push(formatInterpolatedValue(ctx, value, format));
};
