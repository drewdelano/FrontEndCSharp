import { createManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource, JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

const stringTaskType = 'System.Threading.Tasks.Task`1[TResult]';
const stringTaskArguments = { TResult: 'System.String' };

globalThis.DotNet.Methods['System.Net.Http.HttpClient.GetStringAsync(System.String)'] = function (ctx, $this, requestUri) {
    const getAsync = requireMethod(
        'System.Net.Http.HttpClient.GetAsync(System.String)');
    const requestTask = getAsync(ctx, $this, requestUri);
    const requestSource = getTaskCompletionSource(unwrap(requestTask));
    const source = new JavaScriptTaskCompletionSource();

    const complete = () => {
        if (requestSource.isFaulted) {
            source.setException(requestSource.exception);
            return;
        }
        if (requestSource.isCanceled) {
            source.setCanceled(requestSource.exception);
            return;
        }

        const response = unwrap(requestSource.result);
        const statusCode = Number(unwrap(response?._statusCode));
        if (statusCode < 200 || statusCode > 299) {
            source.setException(createManagedException(
                'System.Net.Http.HttpRequestException',
                `Response status code does not indicate success: ${statusCode}.`));
            return;
        }

        const content = unwrap(response?._content);
        const bytes = content?.$fecsBytes instanceof Uint8Array
            ? content.$fecsBytes
            : new Uint8Array();
        source.setResult(wrap(new TextDecoder('utf-8').decode(bytes), 'System.String'));
    };
    if (requestSource.isCompleted) {
        complete();
    } else {
        requestSource.registerContinuation(complete);
    }

    return wrap(source.task, stringTaskType, stringTaskArguments);
};

function requireMethod(name) {
    const method = globalThis.DotNet.Methods[name];
    if (typeof method !== 'function') {
        throw new Error(`The managed method '${name}' is unavailable.`);
    }
    return method;
}
