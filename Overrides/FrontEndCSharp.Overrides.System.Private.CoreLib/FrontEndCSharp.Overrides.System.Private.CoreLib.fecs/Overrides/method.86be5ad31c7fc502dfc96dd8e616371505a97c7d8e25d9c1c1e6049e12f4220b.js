import { wrap } from '../msil-type-helpers.js';
import { formatInt32 } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.Int32.ToString()'] = function(ctx, thisArg) {
    return wrap(formatInt32(thisArg, null), 'System.String');
};
