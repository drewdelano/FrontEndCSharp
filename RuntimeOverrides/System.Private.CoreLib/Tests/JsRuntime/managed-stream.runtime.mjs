import assert from "node:assert/strict";
import test from "node:test";

import {
    disposeManagedAsyncStream,
    readManagedAsyncStream,
    readManagedMemoryStream
} from "../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/managed-stream.js";
import { cancelCancellationSource } from "../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/task-cancellation.js";
import { getTaskCompletionSource } from "../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js";
import { unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";

// This focused JavaScript seam verifies ReadableStream chunk ownership and
// cancellation directly; the C# Chromium fixture owns the public API contract.

test("async browser streams preserve unread chunk bytes", async () => {
    let reads = 0;
    const stream = wrap({
        $fecsReadableStreamReader: {
            read: async () => reads++ === 0
                ? { done: false, value: Uint8Array.from([1, 2, 3, 4]) }
                : { done: true }
        }
    }, "System.IO.Stream");
    const first = wrap(new Array(2), "System.Byte[]");
    const second = wrap(new Array(2), "System.Byte[]");

    assert.equal(await read(stream, first, 0, 2), 2);
    assert.deepEqual(unwrap(first).map(unwrap), [1, 2]);
    assert.equal(await read(stream, second, 0, 2), 2);
    assert.deepEqual(unwrap(second).map(unwrap), [3, 4]);
    assert.equal(reads, 1);
    assert.equal(await read(stream, second, 0, 2), 0);
});

test("async browser stream reads propagate cancellation", async () => {
    let rejectRead;
    let canceled = false;
    const stream = wrap({
        $fecsReadableStreamReader: {
            read: () => new Promise((resolve, reject) => rejectRead = reject),
            cancel: () => {
                canceled = true;
                rejectRead(new Error("canceled"));
            }
        }
    }, "System.IO.Stream");
    const source = wrap({
        $fecsCancellationRequested: false,
        $fecsCancellationCallbacks: new Set()
    }, "System.Threading.CancellationTokenSource");
    const token = wrap({
        $fecsCancellationSource: source
    }, "System.Threading.CancellationToken");
    const task = readManagedAsyncStream(
        {}, stream, wrap(new Array(1), "System.Byte[]"), wrap(0), wrap(1), token);
    const completion = getTaskCompletionSource(unwrap(task));

    cancelCancellationSource(source);
    await completionOf(completion);

    assert.equal(canceled, true);
    assert.equal(completion.isCanceled, true);
});

test("async browser stream failures become managed IOExceptions", async () => {
    const stream = wrap({
        $fecsReadableStreamReader: {
            read: async () => { throw new Error("connection reset"); }
        }
    }, "System.IO.Stream");
    const task = readManagedAsyncStream(
        {}, stream, wrap(new Array(1), "System.Byte[]"), wrap(0), wrap(1));
    const completion = getTaskCompletionSource(unwrap(task));

    await completionOf(completion);

    assert.equal(completion.isFaulted, true);
    assert.equal(completion.exception.t, "System.IO.IOException");
});

test("disposing an async browser stream cancels its reader", () => {
    let canceled = false;
    const stream = wrap({
        $fecsReadableStreamReader: {
            cancel: () => { canceled = true; }
        }
    }, "System.IO.Stream");

    disposeManagedAsyncStream(stream);

    assert.equal(canceled, true);
    assert.equal(unwrap(stream).$fecsDisposed, true);
});

test("managed memory streams read their constructor-backed buffer", async () => {
    const stream = wrap({
        _buffer: wrap([wrap(10), wrap(20), wrap(30)], "System.Byte[]"),
        _position: wrap(0, "System.Int32"),
        _length: wrap(3, "System.Int32"),
        _isOpen: wrap(true, "System.Boolean")
    }, "System.IO.MemoryStream");
    const first = wrap(new Array(2), "System.Byte[]");
    const second = wrap(new Array(2), "System.Byte[]");

    assert.equal(unwrap(readManagedMemoryStream(
        {}, stream, first, wrap(0), wrap(2))), 2);
    assert.deepEqual(unwrap(first).map(unwrap), [10, 20]);
    assert.equal(await read(stream, second, 0, 2), 1);
    assert.deepEqual(unwrap(second).slice(0, 1).map(unwrap), [30]);
    assert.equal(await read(stream, second, 0, 2), 0);
});

test("browser-created memory streams use their explicit byte store", async () => {
    const stream = wrap({
        _isOpen: wrap(true, "System.Boolean"),
        $fecsMemoryBytes: wrap([wrap(40), wrap(50)], "System.Byte[]"),
        $fecsMemoryPosition: 0,
        $fecsMemoryLength: 2
    }, "System.IO.MemoryStream");
    const buffer = wrap(new Array(2), "System.Byte[]");

    assert.equal(await read(stream, buffer, 0, 2), 2);
    assert.deepEqual(unwrap(buffer).map(unwrap), [40, 50]);
    assert.equal(unwrap(stream).$fecsMemoryPosition, 2);
});

async function read(stream, buffer, offset, count) {
    const task = readManagedAsyncStream(
        {}, stream, buffer, wrap(offset), wrap(count));
    const completion = getTaskCompletionSource(unwrap(task));
    await completionOf(completion);
    return unwrap(completion.result);
}

async function completionOf(completion) {
    if (completion.isCompleted) return;
    await new Promise(resolve => completion.registerContinuation(resolve));
}
