import { unwrap, wrap } from '../msil-type-helpers.js';
import { JavaScriptTaskCompletionSource, tryGetTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';
import { requireNonNullTaskArgument } from '../runtime-assets/System.Private.CoreLib.fecs/task-argument-validation.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.Run<TResult>(System.Func`1[TResult])'] = function (ctx, functionDelegate) {
    requireNonNullTaskArgument(ctx, functionDelegate, 'function');
    const source = new JavaScriptTaskCompletionSource();
    queueMicrotask(() => {
        try {
            const result = invokeDelegate(ctx, functionDelegate);
            const innerSource = tryGetTaskCompletionSource(unwrap(result));
            if (innerSource === null) {
                source.setResult(result);
                return;
            }
            const completeFromInner = () => {
                if (innerSource.isFaulted) {
                    source.setException(innerSource.exception);
                } else if (innerSource.isCanceled) {
                    source.setCanceled(innerSource.exception);
                } else {
                    source.setResult(innerSource.result);
                }
            };
            if (innerSource.isCompleted) {
                completeFromInner();
            } else {
                innerSource.registerContinuation(completeFromInner);
            }
        } catch (error) {
            if (!error?.dotNetException) {
                throw error;
            }
            source.setException(error.dotNetException);
        }
    });
    return wrap(source.task, 'System.Threading.Tasks.Task`1[TResult]');
};
