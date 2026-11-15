import { wrap } from '../msil-type-helpers.js';
import { formatTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.DateTimeOffset.ToString(System.IFormatProvider)'] = function (_ctx, value, _provider) {
    return wrap(formatTemporal('System.DateTimeOffset', value, null), 'System.String');
};
