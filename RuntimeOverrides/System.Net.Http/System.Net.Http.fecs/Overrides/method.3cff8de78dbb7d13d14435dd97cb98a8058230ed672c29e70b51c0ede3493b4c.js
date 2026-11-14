import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.set_BaseAddress(System.Uri)'] = function (ctx, $this, value) {
    const client = unwrap($this);
    if (unwrap(client._disposed)) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access a disposed HttpClient.');
    }
    if (unwrap(client._operationStarted)) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'HttpClient properties cannot be modified after the first request.');
    }

    const uri = unwrap(value);
    if (uri !== null && uri !== undefined) {
        const uriValue = uri.$fecsHttpUri;
        if (typeof uriValue !== 'string' || !/^https?:\/\//i.test(uriValue)) {
            throwManagedException(ctx, 'System.ArgumentException', 'The base address must be an absolute HTTP or HTTPS URI.');
        }
    }
    client._baseAddress = value;
};
