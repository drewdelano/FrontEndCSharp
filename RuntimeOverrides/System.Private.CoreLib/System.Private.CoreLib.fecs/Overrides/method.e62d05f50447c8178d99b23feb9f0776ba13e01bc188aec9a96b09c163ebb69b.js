import { throwManagedException } from '../managed-exceptions.js';
import { wrap } from '../msil-type-helpers.js';
import { formatIntegral } from '../runtime-assets/System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.Int64.ToString(System.IFormatProvider)'] = function (ctx, value, _provider) {
    const result = formatIntegral(value, null, 'System.Int64');
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
