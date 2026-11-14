import assert from "node:assert/strict";
import test from "node:test";

import {
    getManualResetValueTaskSourceResult,
    getManualResetValueTaskSourceStatus,
    getManualResetValueTaskSourceVersion,
    resetManualResetValueTaskSource,
    setManualResetValueTaskSourceException,
    setManualResetValueTaskSourceResult
} from "../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/async-iterator.js";
import { DotNetError, unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("manual reset value-task sources version and complete reusable operations", () => {
    const context = { stackTrace: "async iterator" };
    const core = wrap({}, "System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1[System.Int32]");

    resetManualResetValueTaskSource(core);
    const firstVersion = getManualResetValueTaskSourceVersion(core);
    assert.equal(unwrap(getManualResetValueTaskSourceStatus(context, core, firstVersion)), 0);

    const result = wrap(42, "System.Int32");
    setManualResetValueTaskSourceResult(core, result);
    assert.equal(unwrap(getManualResetValueTaskSourceStatus(context, core, firstVersion)), 1);
    assert.strictEqual(getManualResetValueTaskSourceResult(context, core, firstVersion), result);

    resetManualResetValueTaskSource(core);
    const secondVersion = getManualResetValueTaskSourceVersion(core);
    assert.notEqual(unwrap(secondVersion), unwrap(firstVersion));
    assert.throws(
        () => getManualResetValueTaskSourceStatus(context, core, firstVersion),
        error => error instanceof DotNetError
            && error.name === "System.InvalidOperationException");
});

test("manual reset value-task sources preserve managed exceptions", () => {
    const context = { stackTrace: "async iterator exception" };
    const core = wrap({}, "System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1[System.Boolean]");
    const exception = wrap({ _message: null }, "System.InvalidOperationException");

    resetManualResetValueTaskSource(core);
    const version = getManualResetValueTaskSourceVersion(core);
    setManualResetValueTaskSourceException(core, exception);

    assert.equal(unwrap(getManualResetValueTaskSourceStatus(context, core, version)), 2);
    assert.throws(
        () => getManualResetValueTaskSourceResult(context, core, version),
        error => error instanceof DotNetError
            && error.name === "System.InvalidOperationException"
            && error.dotNetException === exception);
});
