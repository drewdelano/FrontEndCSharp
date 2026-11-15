import assert from "node:assert/strict";
import test from "node:test";

import {
    getLazyValue,
    initializeLazy,
    isLazyValueCreated
} from "../../JsRuntime/runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-lazy.js";
import { DotNetError, throwDotNetException, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("factory Lazy values initialize once", () => {
    const context = { stackTrace: "lazy" };
    const lazy = wrap({}, "System.Lazy`1[System.Int32]");
    const value = wrap(42, "System.Int32");
    let calls = 0;
    initializeLazy(context, lazy, wrap(() => {
        calls++;
        return value;
    }, "System.Func`1[System.Int32]"));

    assert.equal(isLazyValueCreated(context, lazy), false);
    assert.strictEqual(getLazyValue(context, lazy), value);
    assert.strictEqual(getLazyValue(context, lazy), value);
    assert.equal(calls, 1);
    assert.equal(isLazyValueCreated(context, lazy), true);
});

test("factory Lazy values cache managed failures", () => {
    const context = { stackTrace: "lazy failure" };
    const lazy = wrap({}, "System.Lazy`1[System.Int32]");
    const exception = wrap({ _message: null }, "System.InvalidOperationException");
    let calls = 0;
    initializeLazy(context, lazy, wrap(() => {
        calls++;
        throwDotNetException(context, exception);
    }, "System.Func`1[System.Int32]"));

    for (let attempt = 0; attempt < 2; attempt++) {
        assert.throws(
            () => getLazyValue(context, lazy),
            error => error instanceof DotNetError
                && error.dotNetException === exception);
    }
    assert.equal(calls, 1);
    assert.equal(isLazyValueCreated(context, lazy), false);
});

test("factory Lazy values reject recursive access", () => {
    const context = { stackTrace: "lazy recursion" };
    const lazy = wrap({}, "System.Lazy`1[System.Int32]");
    let calls = 0;
    initializeLazy(context, lazy, wrap(() => {
        calls++;
        return getLazyValue(context, lazy);
    }, "System.Func`1[System.Int32]"));

    for (let attempt = 0; attempt < 2; attempt++) {
        assert.throws(
            () => getLazyValue(context, lazy),
            error => error instanceof DotNetError
                && error.name === "System.InvalidOperationException");
    }
    assert.equal(calls, 1);
});
