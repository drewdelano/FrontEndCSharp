import { initializeObject } from '../msil.js';
import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.GetAsync(System.String, System.Threading.CancellationToken)'] = function (ctx, $this, requestUri, cancellationToken) {
    return sendGet(ctx, $this, requestUri, wrap(0, 'System.Net.Http.HttpCompletionOption'), cancellationToken);
};

function sendGet(ctx, $this, requestUri, completionOption, cancellationToken) {
    const request = initializeObject('System.Net.Http.HttpRequestMessage');
    requireMethod('System.Net.Http.HttpRequestMessage..ctor(System.Net.Http.HttpMethod, System.String)')(
        ctx,
        request,
        requireMethod('System.Net.Http.HttpMethod.get_Get()')(ctx),
        requestUri);
    return requireMethod(
        'System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage, System.Net.Http.HttpCompletionOption, System.Threading.CancellationToken)')(
        ctx,
        $this,
        request,
        completionOption,
        cancellationToken);
}

function requireMethod(name) {
    const method = globalThis.DotNet.Methods[name];
    if (typeof method !== 'function') throw new Error(`The managed HTTP dependency '${name}' is unavailable.`);
    return method;
}
