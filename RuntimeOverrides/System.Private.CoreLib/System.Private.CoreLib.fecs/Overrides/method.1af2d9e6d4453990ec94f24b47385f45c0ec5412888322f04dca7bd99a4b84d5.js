import { wrap } from '../msil-type-helpers.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';
import { requireNonNullTaskArgument } from '../runtime-assets/System.Private.CoreLib.fecs/task-argument-validation.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.Run(System.Action)'] = function (ctx, action) {
    requireNonNullTaskArgument(ctx, action, 'action');
    const source = new JavaScriptTaskCompletionSource();
    queueMicrotask(() => {
        try {
            invokeDelegate(ctx, action);
            source.setResult(null);
        } catch (error) {
            if (!error?.dotNetException) {
                throw error;
            }
            source.setException(error.dotNetException);
        }
    });
    return wrap(source.task, 'System.Threading.Tasks.Task');
};
