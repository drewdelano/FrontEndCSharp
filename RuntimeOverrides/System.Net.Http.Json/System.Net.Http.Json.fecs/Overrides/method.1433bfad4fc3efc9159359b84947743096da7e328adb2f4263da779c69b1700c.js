import { throwManagedException } from '../managed-exceptions.js';
import { initializeObject } from '../msil.js';
import { isManagedNull, unwrap, wrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource, JavaScriptTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

const readAsStringName =
    'System.Net.Http.HttpContent.ReadAsStringAsync(System.Threading.CancellationToken)';
const optionsConstructorName =
    'System.Text.Json.JsonSerializerOptions..ctor(System.Text.Json.JsonSerializerDefaults)';
const deserializeName =
    'System.Text.Json.JsonSerializer.Deserialize<TValue>(System.String, System.Text.Json.JsonSerializerOptions)';
const taskType = 'System.Threading.Tasks.Task`1[TResult]';

globalThis.DotNet.Methods['System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<T>(System.Net.Http.HttpContent, System.Threading.CancellationToken)'] = function (ctx, content, cancellationToken) {
    if (isManagedNull(content)) {
        throwManagedException(
            ctx,
            'System.ArgumentNullException',
            'Value cannot be null. (Parameter \'content\')');
    }

    const targetType = ctx.genericParams?.T;
    if (targetType === null || targetType === undefined) {
        throwManagedException(
            ctx,
            'System.NotSupportedException',
            'The HTTP JSON target type could not be resolved.');
    }

    const readAsString = requireMethod(readAsStringName);
    const contentTask = readAsString(ctx, content, cancellationToken);
    const contentSource = getTaskCompletionSource(unwrap(contentTask));
    const source = new JavaScriptTaskCompletionSource();

    const complete = () => {
        if (contentSource.isFaulted) {
            source.setException(contentSource.exception);
            return;
        }
        if (contentSource.isCanceled) {
            source.setCanceled(contentSource.exception);
            return;
        }

        try {
            const options = initializeObject('System.Text.Json.JsonSerializerOptions');
            requireMethod(optionsConstructorName)(
                ctx,
                options,
                wrap(1, 'System.Text.Json.JsonSerializerDefaults'));
            const previousGenericParams = ctx.genericParams;
            try {
                ctx.genericParams = { ...previousGenericParams, TValue: targetType };
                const result = requireMethod(deserializeName)(
                    ctx,
                    contentSource.result,
                    options);
                source.setResult(result);
            } finally {
                ctx.genericParams = previousGenericParams;
            }
        } catch (error) {
            if (!error?.dotNetException) throw error;
            source.setException(error.dotNetException);
        }
    };

    if (contentSource.isCompleted) {
        complete();
    } else {
        contentSource.registerContinuation(complete);
    }

    return wrap(source.task, taskType, { TResult: targetType });
};

function requireMethod(name) {
    const method = globalThis.DotNet.Methods[name];
    if (typeof method !== 'function') {
        throw new Error(`The managed method '${name}' is unavailable.`);
    }
    return method;
}
