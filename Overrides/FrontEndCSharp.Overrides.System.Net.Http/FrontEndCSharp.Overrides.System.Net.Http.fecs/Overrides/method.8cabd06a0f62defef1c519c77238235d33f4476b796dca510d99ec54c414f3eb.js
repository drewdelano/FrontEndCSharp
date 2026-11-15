import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

const taskType = 'System.Threading.Tasks.Task`1[TResult]';
const taskTypeArguments = { TResult: 'System.Byte[]' };

globalThis.DotNet.Methods['System.Net.Http.HttpContent.ReadAsByteArrayAsync()'] = function (ctx, $this) {
    const content = unwrap($this);
    if (content.$fecsDisposed) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access disposed HTTP content.');
    }

    const source = new JavaScriptTaskCompletionSource();
    source.setResult(toManagedArray(getBytes(content)));
    return wrap(source.task, taskType, taskTypeArguments);
};

function toManagedArray(bytes) {
    return wrap(
        Array.from(bytes, value => wrap(value, 'System.Byte')),
        'System.Byte[]');
}

function getBytes(content) {
    if (content.$fecsBytes instanceof Uint8Array) {
        return content.$fecsBytes;
    }
    const managedBytes = unwrap(content._content);
    return Array.isArray(managedBytes)
        ? Uint8Array.from(managedBytes, value => Number(unwrap(value)))
        : new Uint8Array();
}
