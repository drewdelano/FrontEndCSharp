import { createManagedException, throwManagedException } from '../managed-exceptions.js';
import { initializeObject } from '../msil.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { getTimeSpanTicks } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import {
    cancelCancellationSource,
    isCancellationRequested,
    registerCancellationCallback
} from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-cancellation.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

const taskType = 'System.Threading.Tasks.Task`1[TResult]';
const taskTypeArguments = { TResult: 'System.Net.Http.HttpResponseMessage' };

globalThis.DotNet.Methods['System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage, System.Net.Http.HttpCompletionOption, System.Threading.CancellationToken)'] = function (ctx, $this, request, completionOption, cancellationToken) {
    const client = unwrap($this);
    const requestValue = unwrap(request);
    validateRequest(ctx, client, requestValue);

    const completion = unwrap(completionOption);
    if (completion !== 0 && completion !== 1) {
        const source = new JavaScriptTaskCompletionSource();
        source.setException(createManagedException(
            'System.ArgumentOutOfRangeException',
            'The HTTP completion option is invalid.'));
        return wrap(source.task, taskType, taskTypeArguments);
    }

    addDefaultHeaders(client, requestValue);
    requestValue.$fecsResponseHeadersRead = completion === 1;
    const sendAsync = globalThis.DotNet.Methods[
        'System.Net.Http.HttpMessageHandler.SendAsync(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)'];
    if (typeof sendAsync !== 'function') {
        throw new Error('The browser HttpClientHandler transport is unavailable.');
    }
    return sendAsync(
        ctx,
        client._handler,
        request,
        createTransportCancellationToken(client, cancellationToken));
};

function validateRequest(ctx, client, request) {
    if (!request) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'The request cannot be null.');
    }
    if (unwrap(client._disposed)) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access a disposed HttpClient.');
    }
    if ((unwrap(request._sendStatus) ?? 0) !== 0) {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'The request message was already sent. Cannot send the same request message multiple times.');
    }
    const requestUri = unwrap(request._requestUri)?.$fecsHttpUri;
    const baseAddress = unwrap(client._baseAddress)?.$fecsHttpUri;
    if ((typeof requestUri !== 'string' || !/^https?:\/\//i.test(requestUri))
        && (typeof requestUri !== 'string' || typeof baseAddress !== 'string')) {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'The request URI must be an absolute HTTP or HTTPS URI.');
    }
    request._sendStatus = wrap(1, 'System.Int32');
    client._operationStarted = wrap(true, 'System.Boolean');
}

function createTransportCancellationToken(client, originalCancellationToken) {
    const source = wrap({}, 'System.Threading.CancellationTokenSource');
    const sourceValue = unwrap(source);
    sourceValue.$fecsCancellationRequested = false;
    sourceValue.$fecsCancellationCallbacks = new Set();
    sourceValue.$fecsDisposed = false;

    const token = wrap({}, 'System.Threading.CancellationToken');
    unwrap(token).$fecsCancellationSource = source;
    const unregisterOriginal = registerCancellationCallback(
        originalCancellationToken,
        () => cancelCancellationSource(source));
    const timeoutMilliseconds = getTimeoutMilliseconds(client);
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
