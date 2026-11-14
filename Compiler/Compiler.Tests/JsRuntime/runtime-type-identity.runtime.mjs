import assert from "node:assert/strict";
import test from "node:test";

import { areRuntimeTypeObjectsEqual, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("runtime Type objects compare by represented CLR identity", () => {
    assert.equal(
        areRuntimeTypeObjectsEqual(
            wrap("Tests.Payload", "System.Type"),
            wrap("Tests.Payload", "System.Type")),
        true);
    assert.equal(
        areRuntimeTypeObjectsEqual(
            wrap("Tests.Payload", "System.Type"),
            wrap("Tests.Other", "System.Type")),
        false);
});

test("constructed runtime Type identity includes every generic argument", () => {
    const left = wrap("Tests.Pair`2[TFirst, TSecond]", "System.Type", {
        TFirst: "System.String",
        TSecond: "System.Int32"
    });
    const same = wrap("Tests.Pair`2[TFirst, TSecond]", "System.Type", {
        TSecond: "System.Int32",
        TFirst: "System.String"
    });
    const different = wrap("Tests.Pair`2[TFirst, TSecond]", "System.Type", {
        TFirst: "System.String",
        TSecond: "System.Int64"
    });

    assert.equal(areRuntimeTypeObjectsEqual(left, same), true);
    assert.equal(areRuntimeTypeObjectsEqual(left, different), false);
});

test("runtime Type equality preserves null and custom Type reference semantics", () => {
    const customType = wrap({}, "Tests.CustomType");

    assert.equal(areRuntimeTypeObjectsEqual(null, null), true);
    assert.equal(areRuntimeTypeObjectsEqual(null, wrap("Tests.Payload", "System.Type")), false);
    assert.equal(areRuntimeTypeObjectsEqual(customType, customType), true);
    assert.equal(areRuntimeTypeObjectsEqual(customType, wrap({}, "Tests.CustomType")), false);
});
