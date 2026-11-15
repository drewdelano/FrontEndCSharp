import assert from "node:assert/strict";
import test from "node:test";

import {
    dictionaryAdd,
    dictionaryClear,
    dictionaryContainsKey,
    dictionaryCount,
    dictionaryEnumeratorCurrent,
    dictionaryEnumeratorKey,
    dictionaryEnumeratorMoveNext,
    dictionaryEnumeratorValue,
    dictionaryGet,
    dictionaryGetEnumerator,
    dictionaryRemove,
    dictionarySet,
    initializeDictionary,
    keyValuePairKey,
    keyValuePairValue
} from "../../JsRuntime/runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js";
import { DotNetError, unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";

function createDictionary() {
    const dictionary = wrap(
        {},
        "System.Collections.Generic.Dictionary`2[TKey, TValue]",
        { TKey: "System.String", TValue: "System.Int32" });
    initializeDictionary({}, dictionary);
    return dictionary;
}

test("managed dictionaries preserve mutation and lookup behavior", () => {
    const dictionary = createDictionary();
    const one = wrap("one", "System.String");
    const two = wrap("two", "System.String");

    dictionaryAdd({}, dictionary, one, wrap(1, "System.Int32"));
    dictionaryAdd({}, dictionary, two, wrap(2, "System.Int32"));
    assert.equal(unwrap(dictionaryCount({}, dictionary)), 2);
    assert.equal(unwrap(dictionaryGet({}, dictionary, one)), 1);
    assert.equal(unwrap(dictionaryContainsKey({}, dictionary, two)), true);

    dictionarySet({}, dictionary, one, wrap(11, "System.Int32"));
    assert.equal(unwrap(dictionaryGet({}, dictionary, one)), 11);
    assert.equal(unwrap(dictionaryRemove({}, dictionary, two)), true);
    assert.equal(unwrap(dictionaryRemove({}, dictionary, two)), false);
    assert.equal(unwrap(dictionaryCount({}, dictionary)), 1);

    dictionaryClear({}, dictionary);
    assert.equal(unwrap(dictionaryCount({}, dictionary)), 0);
});

test("managed dictionaries reject null and duplicate keys", () => {
    const dictionary = createDictionary();
    const key = wrap("same", "System.String");
    dictionaryAdd({}, dictionary, key, wrap(1, "System.Int32"));

    assert.throws(
        () => dictionaryAdd({}, dictionary, key, wrap(2, "System.Int32")),
        error => error instanceof DotNetError && error.name === "System.ArgumentException");
    assert.throws(
        () => dictionaryAdd({}, dictionary, null, wrap(2, "System.Int32")),
        error => error instanceof DotNetError && error.name === "System.ArgumentNullException");
});

test("managed dictionary enumerators expose generic and nongeneric projections", () => {
    const dictionary = createDictionary();
    dictionaryAdd({}, dictionary, wrap("first", "System.String"), wrap(1, "System.Int32"));
    dictionaryAdd({}, dictionary, wrap("second", "System.String"), wrap(2, "System.Int32"));

    const enumerator = dictionaryGetEnumerator({}, dictionary);
    assert.equal(unwrap(dictionaryEnumeratorMoveNext({}, enumerator)), true);
    assert.equal(unwrap(dictionaryEnumeratorKey({}, enumerator)), "first");
    assert.equal(unwrap(dictionaryEnumeratorValue({}, enumerator)), 1);

    const pair = dictionaryEnumeratorCurrent({}, enumerator);
    assert.equal(unwrap(keyValuePairKey({}, pair)), "first");
    assert.equal(unwrap(keyValuePairValue({}, pair)), 1);

    assert.equal(unwrap(dictionaryEnumeratorMoveNext({}, enumerator)), true);
    assert.equal(unwrap(dictionaryEnumeratorKey({}, enumerator)), "second");
    assert.equal(unwrap(dictionaryEnumeratorMoveNext({}, enumerator)), false);
});

test("managed dictionary enumerators detect mutation", () => {
    const dictionary = createDictionary();
    dictionaryAdd({}, dictionary, wrap("first", "System.String"), wrap(1, "System.Int32"));
    const enumerator = dictionaryGetEnumerator({}, dictionary);
    dictionarySet({}, dictionary, wrap("second", "System.String"), wrap(2, "System.Int32"));

    assert.throws(
        () => dictionaryEnumeratorMoveNext({}, enumerator),
        error => error instanceof DotNetError && error.name === "System.InvalidOperationException");
});

test("managed dictionaries route custom comparers through retained managed methods", () => {
    const previousDotNet = globalThis.DotNet;
    let hashCalls = 0;
    let equalsCalls = 0;
    try {
        globalThis.DotNet = {
            Types: { "Tests.OrdinalIgnoreCaseComparer": { baseType: null } },
            Methods: {
                "Tests.OrdinalIgnoreCaseComparer.GetHashCode(System.String)": (_ctx, _self, value) => {
                    hashCalls++;
                    const text = unwrap(value).toUpperCase();
                    let hash = 0;
                    for (const character of text) hash = (Math.imul(hash, 31) + character.charCodeAt(0)) | 0;
                    return wrap(hash, "System.Int32");
                },
                "Tests.OrdinalIgnoreCaseComparer.Equals(System.String, System.String)": (_ctx, _self, left, right) => {
                    equalsCalls++;
                    return wrap(
                        unwrap(left).toUpperCase() === unwrap(right).toUpperCase(),
                        "System.Boolean");
                }
            }
        };
        const dictionary = wrap(
            {},
            "System.Collections.Generic.Dictionary`2[TKey, TValue]",
            { TKey: "System.String", TValue: "System.Int32" });
        const comparer = wrap({}, "Tests.OrdinalIgnoreCaseComparer");
        initializeDictionary({}, dictionary, null, comparer);

        dictionaryAdd({}, dictionary, wrap("alpha", "System.String"), wrap(1, "System.Int32"));
        assert.equal(
            unwrap(dictionaryGet({}, dictionary, wrap("ALPHA", "System.String"))),
            1);
        assert.throws(
            () => dictionaryAdd(
                {},
                dictionary,
                wrap("Alpha", "System.String"),
                wrap(2, "System.Int32")),
            error => error instanceof DotNetError && error.name === "System.ArgumentException");
        assert.ok(hashCalls >= 3);
        assert.ok(equalsCalls >= 2);
    } finally {
        globalThis.DotNet = previousDotNet;
    }
});

test("default dictionary equality honors managed key GetHashCode and Equals overrides", () => {
    const previousDotNet = globalThis.DotNet;
    try {
        globalThis.DotNet = {
            Types: { "Tests.Key": { baseType: null } },
            Methods: {
                "Tests.Key.GetHashCode()": (_ctx, self) =>
                    wrap(unwrap(self).id | 0, "System.Int32"),
                "Tests.Key.Equals(System.Object)": (_ctx, self, other) =>
                    wrap(unwrap(self).id === unwrap(other).id, "System.Boolean")
            }
        };
        const dictionary = wrap(
            {},
            "System.Collections.Generic.Dictionary`2[TKey, TValue]",
            { TKey: "Tests.Key", TValue: "System.String" });
        initializeDictionary({}, dictionary);
        dictionaryAdd(
            {},
            dictionary,
            wrap({ id: 7 }, "Tests.Key"),
            wrap("value", "System.String"));

        assert.equal(
            unwrap(dictionaryGet({}, dictionary, wrap({ id: 7 }, "Tests.Key"))),
            "value");
        assert.throws(
            () => dictionaryAdd(
                {},
                dictionary,
                wrap({ id: 7 }, "Tests.Key"),
                wrap("duplicate", "System.String")),
            error => error instanceof DotNetError && error.name === "System.ArgumentException");
    } finally {
        globalThis.DotNet = previousDotNet;
    }
});
