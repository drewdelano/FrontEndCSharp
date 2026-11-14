import { initializeObject } from '../msil.js';
import { isManagedNull, unwrap, wrap } from '../msil-type-helpers.js';
import { createCanceledTask, isCancellationRequested } from '../runtime-assets/System.Private.CoreLib.fecs/task-cancellation.js';

globalThis.DotNet.Methods['System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync<TValue>(System.Net.Http.HttpClient, System.String, TValue, System.Text.Json.JsonSerializerOptions, System.Threading.CancellationToken)'] = function (ctx, client, requestUri, value, options, cancellationToken) {
    if (isCancellationRequested(cancellationToken)) {
        return createCanceledTask(
            'System.Threading.Tasks.Task`1[TResult]',
            { TResult: 'System.Net.Http.HttpResponseMessage' });
    }
    const effectiveOptions = getOptions(ctx, options);
    const json = requireMethod(
        'System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, System.Text.Json.JsonSerializerOptions)')(
        ctx,
        value,
        effectiveOptions);
    const content = initializeObject('System.Net.Http.StringContent');
    requireMethod('System.Net.Http.StringContent..ctor(System.String)')(ctx, content, json);
    unwrap(content).$fecsHeaders = new Map([
        ['content-type', ['application/json; charset=utf-8']]
    ]);
    return requireMethod(
        'System.Net.Http.HttpClient.PutAsync(System.String, System.Net.Http.HttpContent)')(
        ctx,
        client,
        requestUri,
        content);
}

function getOptions(ctx, options) {
    if (!isManagedNull(options)) return options;
    const result = initializeObject('System.Text.Json.JsonSerializerOptions');
    requireMethod(
        'System.Text.Json.JsonSerializerOptions..ctor(System.Text.Json.JsonSerializerDefaults)')(
        ctx,
        result,
        wrap(1, 'System.Text.Json.JsonSerializerDefaults'));
    return result;
}

function requireMethod(name) {
    const method = globalThis.DotNet.Methods[name];
    if (typeof method !== 'function') {
        throw new Error(`The managed method '${name}' is unavailable.`);
    }
    return method;
}
