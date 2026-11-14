import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.HttpHeaders.TryGetValues(System.String, System.Collections.Generic.IEnumerable`1[T]&)'] = function (ctx, $this, name, values) {
    const headerName = unwrap(name);
    if (headerName === null || headerName === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Header name cannot be null.');
    }

    const headers = unwrap($this).$fecsHeaders;
    const headerValues = headers instanceof Map
        ? headers.get(String(headerName).toLowerCase())
        : undefined;
    if (!Array.isArray(headerValues)) {
        values.value = null;
        return wrap(false, 'System.Boolean');
    }

    values.value = wrap(
        headerValues.map(value => wrap(value, 'System.String')),
        'System.String[]');
    return wrap(true, 'System.Boolean');
};
