import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)'] = function (ctx, $this, request, cancellationToken) {
    return requireGeneralSend()(ctx, $this, request, responseContentRead(), cancellationToken);
};

function requireGeneralSend() {
    const method = globalThis.DotNet.Methods[
        'System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage, System.Net.Http.HttpCompletionOption, System.Threading.CancellationToken)'];
    if (typeof method !== 'function') throw new Error('The general HttpClient.SendAsync overload is unavailable.');
    return method;
}

function responseContentRead() {
    return wrap(0, 'System.Net.Http.HttpCompletionOption');
}
