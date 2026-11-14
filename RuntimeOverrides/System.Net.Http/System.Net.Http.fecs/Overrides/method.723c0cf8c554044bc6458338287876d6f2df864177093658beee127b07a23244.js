import { initializeObject } from '../msil.js';
import { createManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import {
    createCanceledTask,
    createTaskCanceledException,
    isCancellationRequested,
    registerCancellationCallback
} from '../runtime-assets/System.Private.CoreLib.fecs/task-cancellation.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

const taskType = 'System.Threading.Tasks.Task`1[TResult]';
const taskTypeArguments = { TResult: 'System.Net.Http.HttpResponseMessage' };

globalThis.DotNet.Methods['System.Net.Http.HttpMessageHandler.SendAsync(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)'] = function (ctx, $this, request, cancellationToken) {
    if (unwrap($this).$fecsDisposed) {
        const source = new JavaScriptTaskCompletionSource();
        source.setException(createManagedException(
            'System.ObjectDisposedException',
            'Cannot access a disposed object.'));
        return wrap(source.task, taskType, taskTypeArguments);
    }
    if (isCancellationRequested(cancellationToken)) {
        cleanupCancellationToken(cancellationToken);
        return createCanceledTask(taskType, taskTypeArguments);
    }

    const method = callManaged(ctx, 'System.Net.Http.HttpRequestMessage.get_Method()', request);
    const methodName = unwrap(callManaged(ctx, 'System.Net.Http.HttpMethod.get_Method()', method));
    const requestUri = callManaged(ctx, 'System.Net.Http.HttpRequestMessage.get_RequestUri()', request);
    const uriValue = unwrap(requestUri)?.$fecsHttpUri;
    const source = new JavaScriptTaskCompletionSource();

    if (typeof uriValue !== 'string' || uriValue.length === 0) {
        source.setException(createManagedException(
            'System.InvalidOperationException',
            'The request URI must be an absolute HTTP or HTTPS URI.'));
        return wrap(source.task, taskType, taskTypeArguments);
    }
    if (methodName === 'CONNECT' || methodName === 'TRACE' || methodName === 'TRACK') {
        source.setException(createManagedException(
            'System.PlatformNotSupportedException',
            `HTTP method '${methodName}' is forbidden by the browser Fetch API.`));
        return wrap(source.task, taskType, taskTypeArguments);
    }

    const requestValue = unwrap(request);
    const requestContent = unwrap(requestValue._content);
    if ((methodName === 'GET' || methodName === 'HEAD') && requestContent) {
        source.setException(createManagedException(
            'System.PlatformNotSupportedException',
            `The browser Fetch API does not allow a body on an HTTP ${methodName} request.`));
        return wrap(source.task, taskType, taskTypeArguments);
    }
    if (requestContent && !(requestContent.$fecsBytes instanceof Uint8Array)) {
        source.setException(createManagedException(
            'System.PlatformNotSupportedException',
            'Browser request streaming is not supported. Use buffered HttpContent.'));
        return wrap(source.task, taskType, taskTypeArguments);
    }

    const fetchHeaders = new Headers();
    appendHeaders(fetchHeaders, unwrap(requestValue._headers)?.$fecsHeaders);
    appendHeaders(fetchHeaders, requestContent?.$fecsHeaders);
    const abortController = new AbortController();
    const cancellationSource = unwrap(
        unwrap(cancellationToken)?.$fecsCancellationSource);
    const timeoutMilliseconds = Number(
        cancellationSource?.$fecsHttpTimeoutMilliseconds);
    const nativeTimeoutSignal = timeoutMilliseconds >= 0
        && Number.isFinite(timeoutMilliseconds)
        && typeof AbortSignal.timeout === 'function'
        && typeof AbortSignal.any === 'function'
            ? AbortSignal.timeout(Math.ceil(timeoutMilliseconds))
            : null;
    const fetchOptions = {
        method: methodName,
        headers: fetchHeaders,
        signal: nativeTimeoutSignal === null
            ? abortController.signal
            : AbortSignal.any([abortController.signal, nativeTimeoutSignal])
    };
    if (typeof requestValue.$fecsFetchCredentials === 'string') {
        fetchOptions.credentials = requestValue.$fecsFetchCredentials;
    }
    if (typeof requestValue.$fecsFetchCache === 'string') {
        fetchOptions.cache = requestValue.$fecsFetchCache;
    }
    if (typeof requestValue.$fecsFetchMode === 'string') {
        fetchOptions.mode = requestValue.$fecsFetchMode;
    }
    if (typeof requestValue.$fecsFetchRedirect === 'string') {
        fetchOptions.redirect = requestValue.$fecsFetchRedirect;
    }
    if (typeof requestValue.$fecsFetchIntegrity === 'string') {
        fetchOptions.integrity = requestValue.$fecsFetchIntegrity;
    }
    if (requestContent) {
        fetchOptions.body = requestContent.$fecsBytes;
    }

    let canceledByToken = false;
    const unregisterCancellation = registerCancellationCallback(
        cancellationToken,
        () => {
            canceledByToken = true;
            abortController.abort();
        });
    const cleanup = () => {
        unregisterCancellation();
        cleanupCancellationToken(cancellationToken);
    };

    fetch(uriValue, fetchOptions)
        .then(async fetchResponse => {
            const responseHeadersRead = requestValue.$fecsResponseHeadersRead === true;
            const bufferedBytes = responseHeadersRead
                ? null
                : new Uint8Array(await fetchResponse.arrayBuffer());
            const response = initializeObject('System.Net.Http.HttpResponseMessage');
            callManaged(
                ctx,
                'System.Net.Http.HttpResponseMessage..ctor(System.Net.HttpStatusCode)',
                response,
                wrap(fetchResponse.status, 'System.Net.HttpStatusCode'));
            const responseValue = unwrap(response);
            responseValue._requestMessage = request;
            responseValue._reasonPhrase = wrap(fetchResponse.statusText, 'System.String');

            const responseHeaders = new Map();
            const contentHeaders = new Map();
            fetchResponse.headers.forEach((value, name) => {
                const normalizedName = name.toLowerCase();
                const target = normalizedName.startsWith('content-')
                    ? contentHeaders
                    : responseHeaders;
                target.set(normalizedName, [value]);
            });
            responseValue.$fecsHeaders = responseHeaders;

            const content = initializeObject('System.Net.Http.ByteArrayContent');
            const contentValue = unwrap(content);
            if (responseHeadersRead) {
                contentValue.$fecsReadableStreamReader = fetchResponse.body?.getReader() ?? null;
            } else {
                contentValue.$fecsBytes = bufferedBytes;
            }
            contentValue.$fecsHeaders = contentHeaders;
            contentValue._content = wrap([], 'System.Byte[]');
            contentValue._offset = wrap(0, 'System.Int32');
            contentValue._count = wrap(bufferedBytes?.length ?? 0, 'System.Int32');
            responseValue._content = content;
            cleanup();
            source.setResult(response);
        })
        .catch(error => {
            cleanup();
            if (canceledByToken || nativeTimeoutSignal?.aborted === true) {
                source.setCanceled(createTaskCanceledException());
                return;
            }
            source.setException(createManagedException(
                'System.Net.Http.HttpRequestException',
                error instanceof Error ? error.message : 'The browser Fetch request failed.'));
        });

    return wrap(source.task, taskType, taskTypeArguments);
};

function cleanupCancellationToken(cancellationToken) {
    const source = unwrap(unwrap(cancellationToken)?.$fecsCancellationSource);
    const cleanup = source?.$fecsCancellationCleanup;
    if (typeof cleanup !== 'function') return;
    source.$fecsCancellationCleanup = null;
    cleanup();
}

function callManaged(ctx, signature, ...args) {
    const method = globalThis.DotNet.Methods[signature];
    if (typeof method !== 'function') {
        throw new Error(`Managed HTTP dependency '${signature}' is unavailable.`);
    }
    return method(ctx, ...args);
}

function appendHeaders(target, source) {
    if (!(source instanceof Map)) {
        return;
    }
    for (const [name, values] of source) {
        for (const value of values) {
            target.append(name, value);
        }
    }
}
