import assert from "node:assert/strict";
import test from "node:test";

import {
    cancelCancellationSource,
    createCanceledTask,
    isCancellationRequested,
    registerCancellationCallback
} from "../../JsRuntime/runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-cancellation.js";
import { getTaskCompletionSource } from "../../JsRuntime/runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js";
import { unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("cancellation tokens expose their normalized cancellation state", () => {
    assert.equal(
        isCancellationRequested(wrap({ $isCancellationRequested: true }, "System.Threading.CancellationToken")),
        true);
    assert.equal(
        isCancellationRequested(wrap({ $isCancellationRequested: false }, "System.Threading.CancellationToken")),
        false);
});

test("source-backed tokens publish in-flight cancellation callbacks", () => {
    const source = wrap({
        $fecsCancellationRequested: false,
        $fecsCancellationCallbacks: new Set()
    }, "System.Threading.CancellationTokenSource");
    const token = wrap({
        $fecsCancellationSource: source
    }, "System.Threading.CancellationToken");
    let callbackCount = 0;
    registerCancellationCallback(token, () => callbackCount++);

    cancelCancellationSource(source);
    cancelCancellationSource(source);

    assert.equal(isCancellationRequested(token), true);
    assert.equal(callbackCount, 1);
});

test("cancellation callbacks can be unregistered", () => {
    const source = wrap({
        $fecsCancellationRequested: false,
        $fecsCancellationCallbacks: new Set()
    }, "System.Threading.CancellationTokenSource");
    const token = wrap({
        $fecsCancellationSource: source
    }, "System.Threading.CancellationToken");
    let callbackCount = 0;
    const unregister = registerCancellationCallback(token, () => callbackCount++);
    unregister();

    cancelCancellationSource(source);

    assert.equal(callbackCount, 0);
});

test("canceled tasks carry a managed TaskCanceledException", () => {
    const task = createCanceledTask("System.Threading.Tasks.Task");
    const source = getTaskCompletionSource(unwrap(task));

    assert.equal(source.isCanceled, true);
    assert.equal(source.exception.t, "System.Threading.Tasks.TaskCanceledException");
});
