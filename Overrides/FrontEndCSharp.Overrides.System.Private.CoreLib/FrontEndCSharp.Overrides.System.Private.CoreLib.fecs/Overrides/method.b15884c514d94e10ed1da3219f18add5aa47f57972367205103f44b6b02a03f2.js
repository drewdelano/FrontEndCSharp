import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].get_Task()'] = function (ctx, $this) {
    const taskMethodBuilder = unwrap($this);
    if (taskMethodBuilder.task === null || typeof taskMethodBuilder.task !== "object") {
        throw new Error("AsyncTaskMethodBuilder<TResult> was not initialized by Create().");
    }

    return taskMethodBuilder.task;
};
