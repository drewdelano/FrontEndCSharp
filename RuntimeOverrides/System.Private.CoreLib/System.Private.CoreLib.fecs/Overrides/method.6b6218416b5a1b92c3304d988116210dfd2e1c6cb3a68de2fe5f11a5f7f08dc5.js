import { wrap } from '../msil-type-helpers.js';
import { formatTemporal } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.DateTime.ToString()'] = function (_ctx, value) {
    const formatted = formatTemporal('System.DateTime', value, null);
    if (formatted === null) throw new Error('The managed DateTime value is outside its valid range.');
    return wrap(formatted, 'System.String');
};
