import { throwManagedException } from '../managed-exceptions.js';
import { wrap, unwrap } from '../msil-type-helpers.js';
import { formatReal } from '../runtime-assets/System.Private.CoreLib.fecs/managed-real-formatting.js';

globalThis.DotNet.Methods['System.Double.ToString(System.String)'] = function (ctx, value, format) {
    const result = formatReal(value, unwrap(format), 'System.Double');
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
