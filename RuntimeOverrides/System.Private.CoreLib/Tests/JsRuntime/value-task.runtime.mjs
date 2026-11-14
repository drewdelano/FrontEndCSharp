import assert from "node:assert/strict";
import test from "node:test";

import {
    createAsyncValueTaskMethodBuilder,
    getBuilderValueTask,
    getValueTaskAwaiter,
    getValueTaskAwaiterResult,
    setAsyncValueTaskException
} from "../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/value-task.js";
import { DotNetError, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("ValueTask awaiters rethrow cancellation without AggregateException", () => {
    const context = { genericParams: { TResult: "System.Int32" }, stackTrace: "managed" };
    const builder = createAsyncValueTaskMethodBuilder(context);
    const cancellation = wrap({
        _message: null,
        _innerException: null
    }, "System.OperationCanceledException");
    setAsyncValueTaskException(builder, cancellation);
    const valueTask = getBuilderValueTask(context, builder);
    const awaiter = getValueTaskAwaiter(context, valueTask, true);

    assert.throws(
        () => getValueTaskAwaiterResult(context, awaiter, true),
        error => error instanceof DotNetError
            && error.name === "System.OperationCanceledException"
            && error.dotNetException === cancellation);
});
