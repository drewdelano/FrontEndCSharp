import { wrap } from '../msil-type-helpers.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';
import { createCanceledTask, isCancellationRequested } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-cancellation.js';
import { requireNonNullTaskArgument } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-argument-validation.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.Run(System.Action, System.Threading.CancellationToken)'] = function (ctx, action, cancellationToken) {
    requireNonNullTaskArgument(ctx, action, 'action');
    if (isCancellationRequested(cancellationToken)) {
        return createCanceledTask('System.Threading.Tasks.Task');
    }

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
