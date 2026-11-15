import { createManagedException } from '../../managed-exceptions.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';
import { JavaScriptTaskCompletionSource } from './task-completion-source.js';

export function isCancellationRequested(cancellationToken) {
    const token = unwrap(cancellationToken);
    if (token?.$isCancellationRequested === true) return true;
    return unwrap(token?.$fecsCancellationSource)?.$fecsCancellationRequested === true;
}

export function registerCancellationCallback(cancellationToken, callback) {
    if (typeof callback !== 'function') {
        throw new TypeError('The cancellation callback must be a function.');
    }
    if (isCancellationRequested(cancellationToken)) {
        callback();
        return () => {};
    }

    const source = unwrap(unwrap(cancellationToken)?.$fecsCancellationSource);
    if (source === null || source === undefined || source.$fecsDisposed === true) {
        return () => {};
    }
    source.$fecsCancellationCallbacks ??= new Set();
    source.$fecsCancellationCallbacks.add(callback);
    return () => source.$fecsCancellationCallbacks?.delete(callback);
}

export function cancelCancellationSource(cancellationTokenSource) {
    const source = unwrap(cancellationTokenSource);
    if (source === null || source === undefined || source.$fecsCancellationRequested === true) {
        return;
    }
    source.$fecsCancellationRequested = true;
    const callbacks = [...(source.$fecsCancellationCallbacks ?? [])];
    source.$fecsCancellationCallbacks?.clear();
    for (const callback of callbacks) callback();
}

export function createCanceledTask(typeName, genericTypeMap = null) {
    const source = new JavaScriptTaskCompletionSource();
    source.setCanceled(createTaskCanceledException());
    return wrap(source.task, typeName, genericTypeMap);
}

export function createTaskCanceledException() {
    return createManagedException('System.Threading.Tasks.TaskCanceledException');
}
