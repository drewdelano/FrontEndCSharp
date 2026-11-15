import { throwManagedException } from '../managed-exceptions.js';
import { wrap, unwrap } from '../msil-type-helpers.js';
import { formatIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.Int64.ToString(System.String)'] = function (ctx, value, format) {
    const result = formatIntegral(value, unwrap(format), 'System.Int64');
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
