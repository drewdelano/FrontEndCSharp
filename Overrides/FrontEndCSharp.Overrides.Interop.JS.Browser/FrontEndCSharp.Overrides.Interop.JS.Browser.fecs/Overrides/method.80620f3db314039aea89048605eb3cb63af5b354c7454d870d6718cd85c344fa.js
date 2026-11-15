import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.BrowserHttpRequestOptions.SetBrowserCredentials(System.Net.Http.HttpRequestMessage, Interop.JS.Browser.BrowserRequestCredentials)'] = function (ctx, request, credentials) {
    if (request === null || unwrap(request) === null) {
        throwManagedException(ctx, 'System.ArgumentNullException');
    }
    const value = unwrap(credentials);
    const fetchValues = ['omit', 'same-origin', 'include'];
    if (!Number.isInteger(value) || value < 0 || value >= fetchValues.length) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException');
    }
    unwrap(request).$fecsFetchCredentials = fetchValues[value];
};
