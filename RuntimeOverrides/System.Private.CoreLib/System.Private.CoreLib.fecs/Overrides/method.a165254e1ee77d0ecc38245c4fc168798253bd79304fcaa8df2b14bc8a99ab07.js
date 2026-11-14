import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncTaskMethodBuilder.get_Task()'] = function (ctx, $this) {
    const taskMethodBuilder = unwrap($this);
    if (taskMethodBuilder.task === null || typeof taskMethodBuilder.task !== "object") {
        throw new Error("AsyncTaskMethodBuilder was not initialized by Create().");
    }

    return taskMethodBuilder.task;
};
