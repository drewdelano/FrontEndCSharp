import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.BrowserHttpRequestOptions.SetBrowserMode(System.Net.Http.HttpRequestMessage, Interop.JS.Browser.BrowserRequestMode)'] = function (ctx, request, mode) {
    requireRequest(ctx, request);
    const value = unwrap(mode);
    const fetchValues = ['cors', 'no-cors', 'same-origin'];
    if (!Number.isInteger(value) || value < 0 || value >= fetchValues.length) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException');
    }
    unwrap(request).$fecsFetchMode = fetchValues[value];
};

function requireRequest(ctx, request) {
    if (request === null || unwrap(request) === null) {
        throwManagedException(ctx, 'System.ArgumentNullException');
    }
}
