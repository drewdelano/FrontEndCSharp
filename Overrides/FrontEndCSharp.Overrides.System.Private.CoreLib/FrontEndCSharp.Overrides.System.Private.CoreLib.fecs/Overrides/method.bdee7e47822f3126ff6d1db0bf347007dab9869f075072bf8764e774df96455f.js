import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { formatGuid } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-formatting.js';

globalThis.DotNet.Methods['System.Guid.ToString(System.String, System.IFormatProvider)'] = function (ctx, value, format, _provider) {
    const result = formatGuid(value, unwrap(format));
    if (result === null) throwManagedException(ctx, 'System.FormatException', 'Format specifier was invalid.');
    return wrap(result, 'System.String');
};
