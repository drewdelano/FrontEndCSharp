import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.AuthenticationHeaderValue..ctor(System.String, System.String)'] = function (ctx, $this, scheme, parameter) {
    const header = unwrap($this);
    const schemeValue = unwrap(scheme);
    const parameterValue = unwrap(parameter);
    if (schemeValue === null || schemeValue === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Authentication scheme cannot be null.');
    }
    if (!/^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$/.test(String(schemeValue))) {
        throwManagedException(ctx, 'System.FormatException', 'The authentication scheme is invalid.');
    }
    if (parameterValue !== null
        && parameterValue !== undefined
        && /[\r\n]/.test(String(parameterValue))) {
        throwManagedException(ctx, 'System.FormatException', 'The authentication parameter is invalid.');
    }

    const normalizedScheme = String(schemeValue);
    const normalizedParameter = parameterValue === null || parameterValue === undefined
        ? null
        : String(parameterValue);
    header._scheme = wrap(normalizedScheme, 'System.String');
    header._parameter = normalizedParameter === null
        ? null
        : wrap(normalizedParameter, 'System.String');
    header.$fecsHeaderValue = normalizedParameter === null
        ? normalizedScheme
        : `${normalizedScheme} ${normalizedParameter}`;
};
