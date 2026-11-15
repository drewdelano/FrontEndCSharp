import { initializeObject } from '../msil.js';
import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpRequestMessage..ctor(System.Net.Http.HttpMethod, System.String)'] = function (ctx, $this, method, requestUri) {
    if (method === null || method === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'HTTP method cannot be null.');
    }

    const request = unwrap($this);
    request._method = method;
    const uri = unwrap(requestUri);
    if (uri !== null && uri !== undefined) {
        const managedUri = initializeObject('System.Uri');
        const uriValue = unwrap(managedUri);
        uriValue._string = wrap(uri, 'System.String');
        uriValue._syntax = {};
        uriValue.$fecsHttpUri = uri;
        request._requestUri = managedUri;
    }
    request._sendStatus = wrap(0, 'System.Int32');
    request._versionPolicy = wrap(0, 'System.Net.Http.HttpVersionPolicy');
};
