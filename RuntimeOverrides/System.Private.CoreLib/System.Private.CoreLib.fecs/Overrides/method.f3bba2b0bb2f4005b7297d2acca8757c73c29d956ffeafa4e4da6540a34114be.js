import { wrap } from '../msil-type-helpers.js';
import { formatTemporal } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.DateTimeOffset.ToString()'] = function (_ctx, value) {
    return wrap(formatTemporal('System.DateTimeOffset', value, null), 'System.String');
};
