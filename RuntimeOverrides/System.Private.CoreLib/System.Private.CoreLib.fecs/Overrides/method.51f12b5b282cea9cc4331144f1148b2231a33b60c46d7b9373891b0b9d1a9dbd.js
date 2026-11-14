import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { formatEnum } from '../runtime-assets/System.Private.CoreLib.fecs/managed-enum-formatting.js';

globalThis.DotNet.Methods['System.Enum.ToString(System.String)'] = function (ctx, value, format) {
    const result = formatEnum(value, unwrap(format));
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
