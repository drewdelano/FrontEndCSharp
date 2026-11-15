import { wrap } from '../msil-type-helpers.js';
import { formatTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.TimeSpan.ToString()'] = function (_ctx, value) {
    return wrap(formatTemporal('System.TimeSpan', value, null), 'System.String');
};
