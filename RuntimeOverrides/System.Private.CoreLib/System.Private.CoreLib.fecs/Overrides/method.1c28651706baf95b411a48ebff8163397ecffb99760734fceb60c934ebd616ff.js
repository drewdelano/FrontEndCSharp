import { throwManagedException } from '../managed-exceptions.js';
import { wrap, unwrap } from '../msil-type-helpers.js';
import { formatIntegral } from '../runtime-assets/System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.UInt128.ToString(System.String)'] = function (ctx, value, format) {
    const result = formatIntegral(value, unwrap(format), 'System.UInt128');
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
