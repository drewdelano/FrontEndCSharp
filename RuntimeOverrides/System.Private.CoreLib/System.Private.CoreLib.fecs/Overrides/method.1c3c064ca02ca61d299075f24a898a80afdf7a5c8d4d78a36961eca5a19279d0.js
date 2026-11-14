import { throwManagedException } from '../managed-exceptions.js';
import { wrap } from '../msil-type-helpers.js';
import { formatIntegral } from '../runtime-assets/System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.Int128.ToString()'] = function (ctx, value) {
    const result = formatIntegral(value, null, 'System.Int128');
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
