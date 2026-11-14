import { createManagedException } from '../managed-exceptions.js';
import { initializeObject } from '../msil.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { getTimeSpanTicks } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';
import {
    cancelCancellationSource,
    isCancellationRequested,
    registerCancellationCallback
} from '../runtime-assets/System.Private.CoreLib.fecs/task-cancellation.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

const taskType = 'System.Threading.Tasks.Task`1[TResult]';
const taskTypeArguments = { TResult: 'System.Net.Http.HttpResponseMessage' };

globalThis.DotNet.Methods['System.Net.Http.HttpClient.<SendAsync>g__Core|83_0(System.Net.Http.HttpRequestMessage, System.Net.Http.HttpCompletionOption, System.Threading.CancellationTokenSource, System.Boolean, System.Threading.CancellationTokenSource, System.Threading.CancellationToken)'] = function (ctx, $this, request, completionOption, cancellationTokenSource, disposeCancellationTokenSource, pendingRequestsCancellationTokenSource, originalCancellationToken) {
    const completion = unwrap(completionOption);
    if (completion !== 0 && completion !== 1) {
        const source = new JavaScriptTaskCompletionSource();
        source.setException(createManagedException(
            'System.ArgumentOutOfRangeException',
            'The HTTP completion option is invalid.'));
        return wrap(source.task, taskType, taskTypeArguments);
    }

    const client = unwrap($this);
    addDefaultHeaders(client, unwrap(request));
    unwrap(request).$fecsResponseHeadersRead = completion === 1;
    const handler = client._handler;
    const sendAsync = globalThis.DotNet.Methods[
        'System.Net.Http.HttpMessageHandler.SendAsync(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)'];
    if (typeof sendAsync !== 'function') {
        throw new Error('The browser HttpClientHandler transport is unavailable.');
    }
    return sendAsync(
        ctx,
        handler,
        request,
        createTransportCancellationToken(
            client,
            cancellationTokenSource,
            originalCancellationToken));
};

function createTransportCancellationToken(
    client,
    preparedCancellationTokenSource,
    originalCancellationToken) {
    const source = initializeObject('System.Threading.CancellationTokenSource');
    const sourceValue = unwrap(source);
    sourceValue.$fecsCancellationRequested = false;
    sourceValue.$fecsCancellationCallbacks = new Set();
    sourceValue.$fecsDisposed = false;

    const token = initializeObject('System.Threading.CancellationToken');
    unwrap(token).$fecsCancellationSource = source;
    const unregisterOriginal = registerCancellationCallback(
        originalCancellationToken,
        () => cancelCancellationSource(source));
    const preparedTimeout = unwrap(preparedCancellationTokenSource)
        ?.$fecsHttpTimeoutMilliseconds;
    const timeoutMilliseconds = typeof preparedTimeout === 'number'
        ? preparedTimeout
        : getTimeoutMilliseconds(client);
    sourceValue.$fecsHttpTimeoutMilliseconds = timeoutMilliseconds;
    const timeoutHandle = !isCancellationRequested(token) && timeoutMilliseconds >= 0
        ? setTimeout(() => cancelCancellationSource(source), timeoutMilliseconds)
        : null;
    sourceValue.$fecsCancellationCleanup = () => {
        unregisterOriginal();
        if (timeoutHandle !== null) clearTimeout(timeoutHandle);
    };
    return token;
}

function getTimeoutMilliseconds(client) {
    if (typeof client.$fecsTimeoutMilliseconds === 'number') {
        return client.$fecsTimeoutMilliseconds;
    }
    if (client._timeout !== null && client._timeout !== undefined) {
        return Number(getTimeSpanTicks(client._timeout)) / 10000;
    }
    return 100000;
}

function addDefaultHeaders(client, request) {
    const defaults = unwrap(client.$fecsDefaultRequestHeaders)?.$fecsHeaders;
    if (!(defaults instanceof Map) || defaults.size === 0) {
        return;
    }

    if (request._headers === null || request._headers === undefined) {
        request._headers = initializeObject('System.Net.Http.Headers.HttpRequestHeaders');
        unwrap(request._headers).$fecsHeaders = new Map();
    }
    const headers = unwrap(request._headers).$fecsHeaders;
    for (const [name, values] of defaults) {
        if (!headers.has(name)) {
            headers.set(name, [...values]);
        }
    }
}
