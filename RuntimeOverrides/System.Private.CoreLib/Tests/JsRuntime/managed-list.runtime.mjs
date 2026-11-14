import assert from "node:assert/strict";
import test from "node:test";

import {
    initializeList,
    initializeListFromEnumerable,
    listAdd,
    listClear,
    listContains,
    listCount,
    listEnumeratorCurrent,
    listEnumeratorMoveNext,
    listGet,
    listGetEnumerator,
    listIndexOf,
    listInsert,
    listRemove,
    listRemoveAt,
    listSet,
    listToArray
} from "../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/managed-list.js";
import { DotNetError, unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";

function createList() {
    const list = wrap({}, "System.Collections.Generic.List`1[T]", { T: "System.String" });
    initializeList({}, list);
    return list;
}

test("managed lists initialize from enumerable sources", () => {
    const list = wrap({}, "System.Collections.Generic.List`1[T]", { T: "System.String" });
    initializeListFromEnumerable({}, list, wrap([
        wrap("one", "System.String"),
        wrap("two", "System.String")
    ], "System.String[]"));

    assert.equal(unwrap(listCount({}, list)), 2);
    assert.equal(unwrap(listGet({}, list, wrap(1, "System.Int32"))), "two");
    assert.deepEqual(
        unwrap(listToArray({}, list)).map(unwrap),
        ["one", "two"]);
});

test("managed lists preserve indexed mutation behavior", () => {
    const list = createList();
    assert.equal(unwrap(listAdd({}, list, wrap("one", "System.String"))), 0);
    assert.equal(unwrap(listAdd({}, list, wrap("three", "System.String"))), 1);
    listInsert({}, list, wrap(1, "System.Int32"), wrap("two", "System.String"));
    assert.equal(unwrap(listCount({}, list)), 3);
    assert.equal(unwrap(listGet({}, list, wrap(1, "System.Int32"))), "two");

    listSet({}, list, wrap(1, "System.Int32"), wrap("second", "System.String"));
    assert.equal(unwrap(listContains({}, list, wrap("second", "System.String"))), true);
    assert.equal(unwrap(listIndexOf({}, list, wrap("three", "System.String"))), 2);
    assert.equal(unwrap(listRemove({}, list, wrap("one", "System.String"))), true);
    listRemoveAt({}, list, wrap(1, "System.Int32"));
    assert.equal(unwrap(listCount({}, list)), 1);

    listClear({}, list);
    assert.equal(unwrap(listCount({}, list)), 0);
});

test("managed list enumerators preserve order and detect mutation", () => {
    const list = createList();
    listAdd({}, list, wrap("one", "System.String"));
    listAdd({}, list, wrap("two", "System.String"));
    const enumerator = listGetEnumerator({}, list);

    assert.equal(unwrap(listEnumeratorMoveNext({}, enumerator)), true);
    assert.equal(unwrap(listEnumeratorCurrent({}, enumerator)), "one");
    assert.equal(unwrap(listEnumeratorMoveNext({}, enumerator)), true);
    assert.equal(unwrap(listEnumeratorCurrent({}, enumerator)), "two");
    assert.equal(unwrap(listEnumeratorMoveNext({}, enumerator)), false);

    const invalidated = listGetEnumerator({}, list);
    listAdd({}, list, wrap("three", "System.String"));
    assert.throws(
        () => listEnumeratorMoveNext({}, invalidated),
        error => error instanceof DotNetError && error.name === "System.InvalidOperationException");
});

test("managed lists validate indexes", () => {
    const list = createList();
    assert.throws(
        () => listGet({}, list, wrap(0, "System.Int32")),
        error => error instanceof DotNetError && error.name === "System.ArgumentOutOfRangeException");
});

test("managed list lookup honors managed Equals overrides", () => {
    const previousDotNet = globalThis.DotNet;
    try {
        globalThis.DotNet = {
            Types: { "Tests.Value": { baseType: null } },
            Methods: {
                "Tests.Value.Equals(System.Object)": (_ctx, self, other) =>
                    wrap(unwrap(self).id === unwrap(other).id, "System.Boolean")
            }
        };
        const list = wrap(
            {},
            "System.Collections.Generic.List`1[T]",
            { T: "Tests.Value" });
        initializeList({}, list);
        listAdd({}, list, wrap({ id: 3 }, "Tests.Value"));

        assert.equal(
            unwrap(listContains({}, list, wrap({ id: 3 }, "Tests.Value"))),
            true);
        assert.equal(
            unwrap(listIndexOf({}, list, wrap({ id: 3 }, "Tests.Value"))),
            0);
        assert.equal(
            unwrap(listRemove({}, list, wrap({ id: 3 }, "Tests.Value"))),
            true);
    } finally {
        globalThis.DotNet = previousDotNet;
    }
});
