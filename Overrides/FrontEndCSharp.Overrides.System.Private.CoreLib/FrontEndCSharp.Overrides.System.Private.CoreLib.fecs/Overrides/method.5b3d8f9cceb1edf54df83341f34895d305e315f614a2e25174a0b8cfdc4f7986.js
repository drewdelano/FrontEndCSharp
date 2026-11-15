import { throwManagedException } from '../managed-exceptions.js';
import { wrap, unwrap } from '../msil-type-helpers.js';
import { formatIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.UInt32.ToString(System.String, System.IFormatProvider)'] = function (ctx, value, format, _provider) {
    const result = formatIntegral(value, unwrap(format), 'System.UInt32');
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
