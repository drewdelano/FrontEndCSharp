import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.BrowserHttpRequestOptions.SetBrowserCache(System.Net.Http.HttpRequestMessage, Interop.JS.Browser.BrowserRequestCache)'] = function (ctx, request, cache) {
    requireRequest(ctx, request);
    const value = unwrap(cache);
    const fetchValues = ['default', 'no-store', 'reload', 'no-cache', 'force-cache', 'only-if-cached'];
    if (!Number.isInteger(value) || value < 0 || value >= fetchValues.length) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException');
    }
    unwrap(request).$fecsFetchCache = fetchValues[value];
};

function requireRequest(ctx, request) {
    if (request === null || unwrap(request) === null) {
        throwManagedException(ctx, 'System.ArgumentNullException');
    }
}
