import { unwrap } from '../msil-type-helpers.js';
import { createAggregateException, getTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.get_Exception()'] = function (ctx, $this) {
    const source = getTaskCompletionSource(unwrap($this));
    if (!source.isFaulted) {
        return null;
    }
    return createAggregateException(source.exceptions);
};
