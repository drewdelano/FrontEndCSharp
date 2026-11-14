import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.CheckRequestBeforeSend(System.Net.Http.HttpRequestMessage)'] = function (ctx, $this, request) {
    const client = unwrap($this);
    if (unwrap(client._disposed)) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access a disposed HttpClient.');
    }

    const requestValue = unwrap(request);
    if ((unwrap(requestValue._sendStatus) ?? 0) !== 0) {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'The request message was already sent. Cannot send the same request message multiple times.');
    }
    const requestUri = unwrap(requestValue._requestUri);
    const uri = requestUri?.$fecsHttpUri;
    const baseAddress = unwrap(client._baseAddress)?.$fecsHttpUri;
    if ((typeof uri !== 'string' || !/^https?:\/\//i.test(uri))
        && (typeof uri !== 'string' || typeof baseAddress !== 'string')) {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'The request URI must be an absolute HTTP or HTTPS URI.');
    }
    requestValue._sendStatus = wrap(1, 'System.Int32');
    client._operationStarted = wrap(true, 'System.Boolean');
};
