import { unwrap, wrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource, JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';
import { requireNonNullTaskArgument } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-argument-validation.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.Run(System.Func`1[TResult])'] = function (ctx, functionDelegate) {
    requireNonNullTaskArgument(ctx, functionDelegate, 'function');
    const source = new JavaScriptTaskCompletionSource();
    queueMicrotask(() => {
        try {
            const innerTask = invokeDelegate(ctx, functionDelegate);
            const innerSource = getTaskCompletionSource(unwrap(innerTask));
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
