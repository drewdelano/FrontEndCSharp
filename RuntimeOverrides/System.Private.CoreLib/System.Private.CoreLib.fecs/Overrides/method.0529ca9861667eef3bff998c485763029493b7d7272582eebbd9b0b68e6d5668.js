import { unwrap, wrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource, JavaScriptTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';
import { createCanceledTask, isCancellationRequested } from '../runtime-assets/System.Private.CoreLib.fecs/task-cancellation.js';
import { requireNonNullTaskArgument } from '../runtime-assets/System.Private.CoreLib.fecs/task-argument-validation.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.Run(System.Func`1[TResult], System.Threading.CancellationToken)'] = function (ctx, functionDelegate, cancellationToken) {
    requireNonNullTaskArgument(ctx, functionDelegate, 'function');
    if (isCancellationRequested(cancellationToken)) {
        return createCanceledTask('System.Threading.Tasks.Task');
    }

    const source = new JavaScriptTaskCompletionSource();
    queueMicrotask(() => {
        try {
            const innerSource = getTaskCompletionSource(unwrap(invokeDelegate(ctx, functionDelegate)));
            const completeFromInner = () => {
                if (innerSource.isFaulted) {
                    source.setException(innerSource.exception);
                } else if (innerSource.isCanceled) {
                    source.setCanceled(innerSource.exception);
                } else {
                    source.setResult(null);
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
    return wrap(source.task, 'System.Threading.Tasks.Task');
};
