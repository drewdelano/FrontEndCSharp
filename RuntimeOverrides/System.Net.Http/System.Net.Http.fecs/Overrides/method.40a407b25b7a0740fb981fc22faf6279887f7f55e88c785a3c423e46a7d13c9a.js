import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpRequestMessage.Dispose()'] = function (ctx, $this) {
    const request = unwrap($this);
    request._sendStatus = wrap(3, 'System.Int32');
    const content = unwrap(request._content);
    if (content) {
        content.$fecsDisposed = true;
    }
};
