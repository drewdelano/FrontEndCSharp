import { noCancellationToken } from '../runtime-assets/System.Private.CoreLib.fecs/task-continuation.js';
import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage)'] = function (ctx, $this, request) {
    return requireGeneralSend()(ctx, $this, request, responseContentRead(), noCancellationToken);
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
