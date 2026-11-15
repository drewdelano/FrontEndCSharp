import { unwrap, wrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource, JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

const getAsyncName =
    'System.Net.Http.HttpClient.GetAsync(System.String, System.Threading.CancellationToken)';
const getContentName = 'System.Net.Http.HttpResponseMessage.get_Content()';
const readFromJsonName =
    'System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<T>(System.Net.Http.HttpContent, System.Threading.CancellationToken)';
const taskType = 'System.Threading.Tasks.Task`1[TResult]';

globalThis.DotNet.Methods['System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync<TValue>(System.Net.Http.HttpClient, System.String, System.Threading.CancellationToken)'] = function (ctx, client, requestUri, cancellationToken) {
    const targetType = ctx.genericParams?.TValue;
    const responseTask = requireMethod(getAsyncName)(ctx, client, requestUri, cancellationToken);
    const responseSource = getTaskCompletionSource(unwrap(responseTask));
    const source = new JavaScriptTaskCompletionSource();

    const completeResponse = () => {
        if (copyFailure(responseSource, source)) return;

        try {
            const content = requireMethod(getContentName)(ctx, responseSource.result);
            const previousGenericParams = ctx.genericParams;
            let contentTask;
            try {
                ctx.genericParams = { ...previousGenericParams, T: targetType };
                contentTask = requireMethod(readFromJsonName)(ctx, content, cancellationToken);
            } finally {
                ctx.genericParams = previousGenericParams;
            }

            const contentSource = getTaskCompletionSource(unwrap(contentTask));
            const completeContent = () => {
                if (copyFailure(contentSource, source)) return;
                source.setResult(contentSource.result);
            };
            if (contentSource.isCompleted) completeContent();
            else contentSource.registerContinuation(completeContent);
        } catch (error) {
            if (!error?.dotNetException) throw error;
            source.setException(error.dotNetException);
        }
    };

    if (responseSource.isCompleted) completeResponse();
    else responseSource.registerContinuation(completeResponse);
    return wrap(source.task, taskType, { TResult: targetType });
};

function copyFailure(from, to) {
    if (from.isFaulted) {
        to.setException(from.exception);
        return true;
    }
    if (from.isCanceled) {
        to.setCanceled(from.exception);
        return true;
    }
    return false;
}

function requireMethod(name) {
    const method = globalThis.DotNet.Methods[name];
    if (typeof method !== 'function') {
        throw new Error(`The managed method '${name}' is unavailable.`);
    }
    return method;
}
