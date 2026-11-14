import { wrap } from '../msil-type-helpers.js';
import { formatGuid } from '../runtime-assets/System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.Guid.ToString()'] = function (_ctx, value) {
    return wrap(formatGuid(value, null), 'System.String');
};
