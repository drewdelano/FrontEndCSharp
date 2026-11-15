import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { createCanceledTask, isCancellationRequested } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-cancellation.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

const taskType = 'System.Threading.Tasks.Task`1[TResult]';
const taskTypeArguments = { TResult: 'System.String' };

globalThis.DotNet.Methods['System.Net.Http.HttpContent.ReadAsStringAsync(System.Threading.CancellationToken)'] = function (ctx, $this, cancellationToken) {
    const content = unwrap($this);
    if (content.$fecsDisposed) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access disposed HTTP content.');
    }
    if (isCancellationRequested(cancellationToken)) {
        return createCanceledTask(taskType, taskTypeArguments);
    }

    const bytes = getBytes(content);
    const source = new JavaScriptTaskCompletionSource();
    source.setResult(wrap(decode(ctx, content, bytes), 'System.String'));
    return wrap(source.task, taskType, taskTypeArguments);
};

function getBytes(content) {
    if (content.$fecsBytes instanceof Uint8Array) {
        return content.$fecsBytes;
    }
    const managedBytes = unwrap(content._content);
    return Array.isArray(managedBytes)
        ? Uint8Array.from(managedBytes, value => Number(unwrap(value)))
        : new Uint8Array();
}

function decode(ctx, content, bytes) {
    const contentType = content.$fecsHeaders?.get('content-type')?.[0] ?? '';
    const match = /(?:^|;)\s*charset\s*=\s*(?:"([^"]+)"|([^;\s]+))/i.exec(contentType);
    const charset = match?.[1] ?? match?.[2] ?? 'utf-8';
    try {
        return new TextDecoder(charset).decode(bytes);
    } catch {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            `The character set '${charset}' is not supported.`);
    }
}
