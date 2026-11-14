import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.CheckRequestMessage(System.Net.Http.HttpRequestMessage)'] = function (ctx, request) {
    const requestValue = unwrap(request);
    if ((unwrap(requestValue._sendStatus) ?? 0) !== 0) {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'The request message was already sent. Cannot send the same request message multiple times.');
    }

    requestValue._sendStatus = wrap(1, 'System.Int32');
};
