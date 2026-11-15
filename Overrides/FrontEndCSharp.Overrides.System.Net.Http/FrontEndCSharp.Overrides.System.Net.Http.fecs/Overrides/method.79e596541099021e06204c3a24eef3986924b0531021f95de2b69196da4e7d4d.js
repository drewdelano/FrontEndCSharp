import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.HttpHeaders.Add(System.String, System.String)'] = function (ctx, $this, name, value) {
    const headerName = unwrap(name);
    const headerValue = unwrap(value);
    if (headerName === null || headerName === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Header name cannot be null.');
    }
    if (!/^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$/.test(String(headerName))) {
        throwManagedException(ctx, 'System.FormatException', 'The header name is invalid.');
    }

    const headersOwner = unwrap($this);
    headersOwner.$fecsHeaders ??= new Map();
    const normalizedName = String(headerName).toLowerCase();
    const values = headersOwner.$fecsHeaders.get(normalizedName) ?? [];
    values.push(headerValue === null || headerValue === undefined ? '' : String(headerValue));
    headersOwner.$fecsHeaders.set(normalizedName, values);
};
