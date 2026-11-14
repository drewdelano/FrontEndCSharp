import assert from "node:assert/strict";
import test from "node:test";

import {
    getTaskResult,
    JavaScriptTaskException,
    JavaScriptTaskCompletionSource,
    getTaskCompletionSource,
    registerAwaiterContinuation,
    setTaskExceptionOrCancellation,
    waitForTask
} from "../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js";

async function drainMicrotasks() {
    await Promise.resolve();
}

test("a new source exposes one pending task", () => {
    const source = new JavaScriptTaskCompletionSource();

    assert.equal(source.isCompleted, false);
    assert.equal(source.isCompletedSuccessfully, false);
    assert.equal(source.isFaulted, false);
    assert.equal(source.isCanceled, false);
    assert.equal(source.result, null);
    assert.equal(source.exception, null);
    assert.strictEqual(getTaskCompletionSource(source.task), source);
});

test("successful completion stores the result and runs every continuation once", async () => {
    const source = new JavaScriptTaskCompletionSource();
    const calls = [];

    source.registerContinuation(() => calls.push("first"));
    source.registerContinuation(() => calls.push("second"));
    source.setResult(42);

    assert.equal(source.isCompletedSuccessfully, true);
    assert.equal(source.result, 42);
    assert.deepEqual(calls, []);

    await drainMicrotasks();
    assert.deepEqual(calls, ["first", "second"]);
});

test("faulted completion preserves the exact exception", () => {
    const source = new JavaScriptTaskCompletionSource();
    const exception = { t: "System.Exception", v: { message: "boom" } };

    source.setException(exception);

    assert.equal(source.isFaulted, true);
    assert.strictEqual(source.exception, exception);
    assert.deepEqual(source.exceptions, [exception]);
    assert.equal(source.result, null);
});

test("completion is a single terminal transition", () => {
    const source = new JavaScriptTaskCompletionSource();

    assert.equal(source.trySetResult(null), true);
    assert.equal(source.trySetResult(null), false);
    assert.equal(source.trySetException({}), false);
    assert.throws(() => source.setResult(null), /already completed/);
    assert.throws(() => source.setException({}), /already completed/);
});

test("canceled completion preserves its cancellation exception", () => {
    const source = new JavaScriptTaskCompletionSource();
    const exception = { t: "System.Threading.Tasks.TaskCanceledException", v: {} };

    source.setCanceled(exception);

    assert.equal(source.isCanceled, true);
    assert.strictEqual(source.exception, exception);
});

test("async method cancellation exceptions complete the task as canceled", () => {
    const source = new JavaScriptTaskCompletionSource();
    const exception = { t: "System.OperationCanceledException", v: {} };

    setTaskExceptionOrCancellation(source, exception);

    assert.equal(source.isCanceled, true);
    assert.strictEqual(source.exception, exception);
});

test("multi-fault completion preserves every exception in order", () => {
    const source = new JavaScriptTaskCompletionSource();
    const first = { t: "System.InvalidOperationException", v: {} };
    const second = { t: "System.ArgumentException", v: {} };

    source.setExceptions([first, second]);

    assert.strictEqual(source.exception, first);
    assert.deepEqual(source.exceptions, [first, second]);
});

test("a canceled generic Task result throws a managed AggregateException", () => {
    const source = new JavaScriptTaskCompletionSource();
    const exception = {
        t: "System.Threading.Tasks.TaskCanceledException",
        v: { _message: { t: "System.String", v: "A task was canceled." } }
    };
    source.setCanceled(exception);

    assert.throws(
        () => getTaskResult(
            { stackTrace: "managed stack" },
            { t: "System.Threading.Tasks.Task`1[System.Int32]", v: source.task }),
        error => {
            assert.equal(error.name, "System.AggregateException");
            assert.equal(error.dotNetException.t, "System.AggregateException");
            assert.strictEqual(error.dotNetException.v._innerException, exception);
            assert.equal(error.dotNetException.v._stackTraceString.v, "managed stack");
            return true;
        });
});

test("a continuation registered after completion is still asynchronous", async () => {
    const source = new JavaScriptTaskCompletionSource();
    const calls = [];
    source.setResult(null);

    source.registerContinuation(() => calls.push("late"));
    assert.deepEqual(calls, []);

    await drainMicrotasks();
    assert.deepEqual(calls, ["late"]);
});

test("foreign task objects fail closed", () => {
    assert.throws(
        () => getTaskCompletionSource({}),
        /not owned by a JavaScriptTaskCompletionSource/);
});

test("TaskAwaiter continuation resumes when its source completes", async () => {
    const source = new JavaScriptTaskCompletionSource();
    const calls = [];
    const awaiter = {
        t: "System.Runtime.CompilerServices.TaskAwaiter`1[System.Int32]",
        v: {
            task: {
                t: "System.Threading.Tasks.Task`1[System.Int32]",
                v: source.task
            }
        }
    };

    registerAwaiterContinuation(awaiter, () => calls.push("resumed"));
    source.setResult({ t: "System.Int32", v: 42 });

    await drainMicrotasks();
    assert.deepEqual(calls, ["resumed"]);
});

test("YieldAwaiter continuation always resumes asynchronously", async () => {
    const calls = [];
    const awaiter = {
        t: "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter",
        v: {}
    };

    registerAwaiterContinuation(awaiter, () => calls.push("resumed"));
    assert.deepEqual(calls, []);

    await drainMicrotasks();
    assert.deepEqual(calls, ["resumed"]);
});

test("unknown awaiter types fail closed", () => {
    assert.throws(
        () => registerAwaiterContinuation({ t: "Example.UnknownAwaiter", v: {} }, () => {}),
        /is not supported by the JavaScript scheduler/);
});

test("waitForTask bridges successful completion to the JavaScript event loop", async () => {
    const source = new JavaScriptTaskCompletionSource();
    const wait = waitForTask(source.task);

    source.setResult(42);

    assert.equal(await wait, 42);
});

test("waitForTask rejects with the original wrapped exception", async () => {
    const source = new JavaScriptTaskCompletionSource();
    const exception = { t: "System.Exception", v: { message: "boom" } };
    const wait = waitForTask(source.task);

    source.setException(exception);

    await assert.rejects(wait, error => {
        assert.ok(error instanceof JavaScriptTaskException);
        assert.equal(error.status, "faulted");
        assert.strictEqual(error.dotNetException, exception);
        assert.strictEqual(error.cause, exception);
        return true;
    });
});

test("waitForTask reports a faulted entry task to the managed debugger", async () => {
    const source = new JavaScriptTaskCompletionSource();
    const exception = { t: "Tests.ManagedException", v: {} };
    const reported = [];
    Object.defineProperty(globalThis, "__FrontEndCSharpDebugger", {
        configurable: true,
        value: {
            reportUnhandledTaskException(value) {
                reported.push(value);
            }
        }
    });

    try {
        const wait = waitForTask(source.task);
        source.setException(exception);
        await assert.rejects(wait, JavaScriptTaskException);

        assert.deepEqual(reported, [exception]);
    } finally {
        delete globalThis.__FrontEndCSharpDebugger;
    }
});
