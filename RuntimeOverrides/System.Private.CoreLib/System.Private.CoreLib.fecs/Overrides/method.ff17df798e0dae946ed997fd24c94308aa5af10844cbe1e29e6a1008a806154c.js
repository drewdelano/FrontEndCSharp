import { throwManagedException } from '../managed-exceptions.js';
import { wrap } from '../msil-type-helpers.js';
import { formatReal } from '../runtime-assets/System.Private.CoreLib.fecs/managed-real-formatting.js';

globalThis.DotNet.Methods['System.Single.ToString()'] = function (ctx, value) {
    const result = formatReal(value, null, 'System.Single');
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
