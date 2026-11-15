import { initializeObject } from '../msil.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.GetAsync(System.String, System.Net.Http.HttpCompletionOption, System.Threading.CancellationToken)'] = function (ctx, $this, requestUri, completionOption, cancellationToken) {
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
};

function requireMethod(name) {
    const method = globalThis.DotNet.Methods[name];
    if (typeof method !== 'function') throw new Error(`The managed HTTP dependency '${name}' is unavailable.`);
    return method;
}
