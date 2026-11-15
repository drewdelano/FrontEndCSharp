import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpRequestMessage.MarkAsSent()'] = function (ctx, $this) {
    const request = unwrap($this);
    const wasSent = (unwrap(request._sendStatus) ?? 0) !== 0;
    if (!wasSent) {
        request._sendStatus = wrap(1, 'System.Int32');
    }
    return wrap(!wasSent, 'System.Boolean');
};
