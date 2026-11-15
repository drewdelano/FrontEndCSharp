import { wrap } from '../msil-type-helpers.js';
import { formatEnum } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-enum-formatting.js';

globalThis.DotNet.Methods['System.Enum.ToString(System.IFormatProvider)'] = function (_ctx, value, _provider) {
    return wrap(formatEnum(value), 'System.String');
};
