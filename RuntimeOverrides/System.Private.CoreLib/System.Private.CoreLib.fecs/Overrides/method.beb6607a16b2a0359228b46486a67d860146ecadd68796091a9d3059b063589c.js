import { wrap, unwrap } from '../msil-type-helpers.js';
import { getTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.get_IsCompleted()'] = function (ctx, $this) {
    return wrap(getTaskCompletionSource(unwrap($this)).isCompleted, 'System.Boolean');
};
