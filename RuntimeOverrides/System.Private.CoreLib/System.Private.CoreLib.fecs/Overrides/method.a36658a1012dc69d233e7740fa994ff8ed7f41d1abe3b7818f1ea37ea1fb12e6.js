import { wrap, unwrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource, JavaScriptTaskStatus } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.get_Status()'] = function (ctx, $this) {
    const status = getTaskCompletionSource(unwrap($this)).status;
    const taskStatus = status === JavaScriptTaskStatus.succeeded
        ? 5
        : status === JavaScriptTaskStatus.canceled
            ? 6
            : status === JavaScriptTaskStatus.faulted
                ? 7
                : 1;
    return wrap(taskStatus, 'System.Threading.Tasks.TaskStatus');
};
