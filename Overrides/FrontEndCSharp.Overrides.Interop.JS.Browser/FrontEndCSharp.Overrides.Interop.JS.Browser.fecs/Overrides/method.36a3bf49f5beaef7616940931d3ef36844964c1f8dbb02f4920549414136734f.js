import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.BrowserHttpRequestOptions.SetBrowserRedirect(System.Net.Http.HttpRequestMessage, Interop.JS.Browser.BrowserRedirectMode)'] = function (ctx, request, redirect) {
    requireRequest(ctx, request);
    const value = unwrap(redirect);
    const fetchValues = ['follow', 'error', 'manual'];
    if (!Number.isInteger(value) || value < 0 || value >= fetchValues.length) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException');
    }
    unwrap(request).$fecsFetchRedirect = fetchValues[value];
};

function requireRequest(ctx, request) {
    if (request === null || unwrap(request) === null) {
        throwManagedException(ctx, 'System.ArgumentNullException');
    }
}
