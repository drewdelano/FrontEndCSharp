import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.HttpHeaders.Contains(System.String)'] = function (ctx, $this, name) {
    const headerName = unwrap(name);
    if (headerName === null || headerName === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Header name cannot be null.');
    }

    const headers = unwrap($this).$fecsHeaders;
    return wrap(headers instanceof Map && headers.has(String(headerName).toLowerCase()), 'System.Boolean');
};
