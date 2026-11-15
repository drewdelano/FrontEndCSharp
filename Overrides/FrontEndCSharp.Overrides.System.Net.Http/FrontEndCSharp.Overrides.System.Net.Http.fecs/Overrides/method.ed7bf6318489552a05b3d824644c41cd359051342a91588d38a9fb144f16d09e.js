import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.PrepareRequestMessage(System.Net.Http.HttpRequestMessage)'] = function (ctx, $this, request) {
    const requestValue = unwrap(request);
    const requestUri = unwrap(requestValue._requestUri);
    const uri = resolveRequestUri(unwrap($this), requestUri?.$fecsHttpUri);
    if (uri === null) {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'The request URI must be an absolute HTTP or HTTPS URI.');
    }
    requestValue._requestUri = createUri(uri);
};

function resolveRequestUri(client, requestUri) {
    if (typeof requestUri === 'string' && /^https?:\/\//i.test(requestUri)) {
        return requestUri;
    }
    const baseAddress = unwrap(client._baseAddress)?.$fecsHttpUri;
    if (typeof requestUri !== 'string' || typeof baseAddress !== 'string') {
        return null;
    }
    try {
        const resolved = new URL(requestUri, baseAddress);
        return resolved.protocol === 'http:' || resolved.protocol === 'https:'
            ? resolved.href
            : null;
    } catch {
        return null;
    }
}

function createUri(value) {
    return wrap({
        _string: wrap(value, 'System.String'),
        _syntax: wrap({}, 'System.UriParser'),
        $fecsHttpUri: value,
        $fecsIsAbsolute: true
    }, 'System.Uri');
}
