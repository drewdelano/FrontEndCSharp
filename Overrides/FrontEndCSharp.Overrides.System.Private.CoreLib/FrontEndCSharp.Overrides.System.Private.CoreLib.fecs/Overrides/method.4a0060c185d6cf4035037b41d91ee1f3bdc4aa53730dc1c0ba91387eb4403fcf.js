import { wrap } from '../msil-type-helpers.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Create()'] = function (ctx) {
    const completionSource = new JavaScriptTaskCompletionSource();
    return wrap({
        completionSource,
        task: wrap(completionSource.task, "System.Threading.Tasks.Task")
    }, "System.Runtime.CompilerServices.AsyncTaskMethodBuilder");
};
