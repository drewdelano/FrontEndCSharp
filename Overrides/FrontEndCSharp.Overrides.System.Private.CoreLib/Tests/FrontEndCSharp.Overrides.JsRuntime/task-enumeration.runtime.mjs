import assert from "node:assert/strict";
import test from "node:test";

import { materializeTaskEnumerable } from "../../JsRuntime/runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-enumeration.js";
import { DotNetError, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("managed task enumerables are materialized and disposed", () => {
    const previousDotNet = globalThis.DotNet;
    const first = wrap({}, "System.Threading.Tasks.Task");
    const second = wrap({}, "System.Threading.Tasks.Task");
    const values = [first, second];
    let index = -1;
    let disposed = false;

    globalThis.DotNet = {
        Types: {
            Sequence: { baseType: "System.Object" },
            Enumerator: { baseType: "System.Object" }
        },
        Methods: {
            "Sequence.GetEnumerator()": () => wrap({}, "Enumerator"),
            "Enumerator.MoveNext()": () => wrap(++index < values.length, "System.Boolean"),
            "Enumerator.get_Current()": () => values[index],
            "Enumerator.Dispose()": () => { disposed = true; }
        }
    };

    try {
        const result = materializeTaskEnumerable({}, wrap({}, "Sequence"));
        assert.deepEqual(result, values);
        assert.equal(disposed, true);
    } finally {
        globalThis.DotNet = previousDotNet;
    }
});

test("array-backed task enumerables retain their array identity", () => {
    const tasks = [wrap({}, "System.Threading.Tasks.Task")];
    assert.strictEqual(
        materializeTaskEnumerable({}, wrap(tasks, "System.Threading.Tasks.Task[]")),
        tasks);
});

test("managed enumeration resolves explicit interface implementations", () => {
    const previousDotNet = globalThis.DotNet;
    const value = wrap("value", "System.String");
    let moved = false;
    let disposed = false;
    globalThis.DotNet = {
        Types: {
            Sequence: { baseType: "System.Object" },
            Enumerator: { baseType: "System.Object" }
        },
        Methods: {
            "Sequence.System.Collections.Generic.IEnumerable`1[T].GetEnumerator()":
                ctx => {
                    assert.equal(ctx.genericParams.TSource, "System.String");
                    return wrap({}, "Enumerator", { T: "System.String" });
                },
            "Enumerator.MoveNext()": () => wrap(!moved && (moved = true), "System.Boolean"),
            "Enumerator.System.Collections.Generic.IEnumerator`1[T].get_Current()": () => value,
            "Enumerator.System.IDisposable.Dispose()": () => { disposed = true; }
        }
    };

    try {
        assert.deepEqual(materializeTaskEnumerable(
            {},
            wrap({}, "Sequence", { TSource: "System.String" })), [value]);
        assert.equal(disposed, true);
    } finally {
        globalThis.DotNet = previousDotNet;
    }
});

test("a missing enumerable method throws MissingMethodException", () => {
    const previousDotNet = globalThis.DotNet;
    globalThis.DotNet = {
        Types: { Sequence: { baseType: null } },
        Methods: {}
    };

    try {
        assert.throws(
            () => materializeTaskEnumerable({ stackTrace: "enumeration" }, wrap({}, "Sequence")),
            error => {
                assert.ok(error instanceof DotNetError);
                assert.equal(error.name, "System.MissingMethodException");
                return true;
            });
    } finally {
        globalThis.DotNet = previousDotNet;
    }
});
