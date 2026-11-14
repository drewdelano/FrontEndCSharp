import assert from "node:assert/strict";
import test from "node:test";

import {
    fillRandomBytes,
    initializeRandom,
    nextRandom,
    nextRandomBelow,
    nextRandomDouble,
    nextRandomRange
} from "../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/managed-random.js";
import { DotNetError, unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("equal Random seeds produce equal bounded sequences and bytes", () => {
    const context = { stackTrace: "random" };
    const first = wrap({}, "System.Random");
    const second = wrap({}, "System.Random");
    const seed = wrap(123456789, "System.Int32");
    initializeRandom(context, first, seed);
    initializeRandom(context, second, seed);

    assert.equal(unwrap(nextRandom(context, first)), unwrap(nextRandom(context, second)));
    assert.equal(
        unwrap(nextRandomBelow(context, first, wrap(1000, "System.Int32"))),
        unwrap(nextRandomBelow(context, second, wrap(1000, "System.Int32"))));
    assert.equal(
        unwrap(nextRandomRange(
            context,
            first,
            wrap(-100, "System.Int32"),
            wrap(100, "System.Int32"))),
        unwrap(nextRandomRange(
            context,
            second,
            wrap(-100, "System.Int32"),
            wrap(100, "System.Int32"))));
    assert.equal(
        unwrap(nextRandomDouble(context, first)),
        unwrap(nextRandomDouble(context, second)));

    const firstBytes = wrap(Array.from({ length: 17 }, () => wrap(0, "System.Byte")), "System.Byte[]");
    const secondBytes = wrap(Array.from({ length: 17 }, () => wrap(0, "System.Byte")), "System.Byte[]");
    fillRandomBytes(context, first, firstBytes);
    fillRandomBytes(context, second, secondBytes);
    assert.deepEqual(unwrap(firstBytes), unwrap(secondBytes));
});

test("Random validates bounds and keeps results in range", () => {
    const context = { stackTrace: "random bounds" };
    const random = wrap({}, "System.Random");
    initializeRandom(context, random, wrap(42, "System.Int32"));

    for (let index = 0; index < 100; index++) {
        const value = unwrap(nextRandomRange(
            context,
            random,
            wrap(-3, "System.Int32"),
            wrap(4, "System.Int32")));
        assert.ok(value >= -3 && value < 4);
    }
    assert.equal(unwrap(nextRandomBelow(context, random, wrap(0, "System.Int32"))), 0);
    assert.equal(unwrap(nextRandomRange(
        context,
        random,
        wrap(5, "System.Int32"),
        wrap(5, "System.Int32"))), 5);
    assert.throws(
        () => nextRandomBelow(context, random, wrap(-1, "System.Int32")),
        error => error instanceof DotNetError
            && error.name === "System.ArgumentOutOfRangeException");
    assert.throws(
        () => nextRandomRange(
            context,
            random,
            wrap(2, "System.Int32"),
            wrap(1, "System.Int32")),
        error => error instanceof DotNetError
            && error.name === "System.ArgumentOutOfRangeException");
});
