import { wrap } from '../msil-type-helpers.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].Create()'] = function (ctx) {
    const completionSource = new JavaScriptTaskCompletionSource();
    return wrap({
        completionSource,
        task: wrap(completionSource.task, 'System.Threading.Tasks.Task`1[TResult]')
    }, 'System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult]');
};
