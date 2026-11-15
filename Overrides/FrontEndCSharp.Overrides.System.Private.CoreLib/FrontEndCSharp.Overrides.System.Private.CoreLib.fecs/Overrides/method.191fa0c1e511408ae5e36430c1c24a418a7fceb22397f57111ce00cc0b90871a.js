import { throwManagedException } from '../managed-exceptions.js';
import { wrap } from '../msil-type-helpers.js';
import { formatIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.UInt128.ToString()'] = function (ctx, value) {
    const result = formatIntegral(value, null, 'System.UInt128');
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
