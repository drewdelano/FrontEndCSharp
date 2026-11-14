import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { formatInt32 } from '../runtime-assets/System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.Int32.ToString(System.String, System.IFormatProvider)'] = function (ctx, value, format, _provider) {
    const result = formatInt32(value, unwrap(format));
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
