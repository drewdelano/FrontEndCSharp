import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.BrowserHttpRequestOptions.SetBrowserIntegrity(System.Net.Http.HttpRequestMessage, System.String)'] = function (ctx, request, integrity) {
    requireRequest(ctx, request);
    const value = unwrap(integrity);
    if (value === null || value === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException');
    }
    unwrap(request).$fecsFetchIntegrity = String(value);
};

function requireRequest(ctx, request) {
    if (request === null || unwrap(request) === null) {
        throwManagedException(ctx, 'System.ArgumentNullException');
    }
}
