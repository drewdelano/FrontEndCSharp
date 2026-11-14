import { wrap } from '../msil-type-helpers.js';
import { formatTemporal } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.TimeOnly.ToString(System.IFormatProvider)'] = function (_ctx, value, _provider) {
    return wrap(formatTemporal('System.TimeOnly', value, null), 'System.String');
};
