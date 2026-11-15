import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.HttpHeaders.GetValues(System.String)'] = function (ctx, $this, name) {
    const headerName = unwrap(name);
    if (headerName === null || headerName === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Header name cannot be null.');
    }

    const headers = unwrap($this).$fecsHeaders;
    const headerValues = headers instanceof Map
        ? headers.get(String(headerName).toLowerCase())
        : undefined;
    if (!Array.isArray(headerValues)) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'The requested header was not found.');
    }

    return wrap(
        headerValues.map(value => wrap(value, 'System.String')),
        'System.String[]');
};
