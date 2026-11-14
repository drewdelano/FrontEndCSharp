import { initializeObject } from '../msil.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.PostAsync(System.String, System.Net.Http.HttpContent)'] = function (ctx, $this, requestUri, content) {
    const getPost = requireMethod('System.Net.Http.HttpMethod.get_Post()');
    const createRequest = requireMethod(
        'System.Net.Http.HttpRequestMessage..ctor(System.Net.Http.HttpMethod, System.String)');
    const setContent = requireMethod(
        'System.Net.Http.HttpRequestMessage.set_Content(System.Net.Http.HttpContent)');
    const sendAsync = requireMethod(
        'System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage)');

    const request = initializeObject('System.Net.Http.HttpRequestMessage');
    createRequest(ctx, request, getPost(ctx), requestUri);
    setContent(ctx, request, content);
    return sendAsync(ctx, $this, request);
};

function requireMethod(name) {
    const method = globalThis.DotNet.Methods[name];
    if (typeof method !== 'function') {
        throw new Error(`The managed method '${name}' is unavailable.`);
    }
    return method;
}
