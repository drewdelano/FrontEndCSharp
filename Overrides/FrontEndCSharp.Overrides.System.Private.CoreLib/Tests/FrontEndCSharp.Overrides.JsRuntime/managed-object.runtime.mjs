import assert from "node:assert/strict";
import test from "node:test";

import {
    managedPrimitiveEquals,
    managedPrimitiveHash,
    objectEquals,
    objectGetHashCode,
    primitiveEquals,
    primitiveGetHashCode,
    runtimeIdentityHash
} from "../../JsRuntime/runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js";
import { unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("managed object hashes are stable identities", () => {
    const first = wrap({}, "System.Object");
    const second = wrap({}, "System.Object");

    assert.equal(runtimeIdentityHash(null), 0);
    assert.equal(runtimeIdentityHash(first), runtimeIdentityHash(first));
    assert.notEqual(runtimeIdentityHash(first), runtimeIdentityHash(second));
    assert.equal(unwrap(objectGetHashCode({}, first)), runtimeIdentityHash(first));
});

test("System.Object.Equals preserves reference identity", () => {
    const value = {};
    const first = wrap(value, "System.Object");
    const same = wrap(value, "System.Object");
    const other = wrap({}, "System.Object");

    assert.equal(unwrap(objectEquals({}, first, same)), true);
    assert.equal(unwrap(objectEquals({}, first, other)), false);
});

test("primitive hashes and equality match CoreLib value contracts", () => {
    assert.equal(managedPrimitiveHash(wrap(-17, "System.Int16")), -17);
    assert.equal(managedPrimitiveHash(wrap(0x100000002n, "System.Int64")), 3);
    assert.equal(managedPrimitiveHash(wrap(-0, "System.Single")), 0);
    assert.equal(managedPrimitiveHash(wrap(Number.NaN, "System.Single")), 0x7f800000);
    assert.equal(managedPrimitiveHash(wrap(Number.NaN, "System.Double")), 0x7ff00000);
    assert.equal(managedPrimitiveHash(wrap("", "System.String")), 757602046);
    assert.equal(managedPrimitiveHash(wrap("alpha", "System.String")), -983331422);

    assert.equal(managedPrimitiveEquals(
        wrap(Number.NaN, "System.Double"),
        wrap(Number.NaN, "System.Double")), true);
    assert.equal(unwrap(primitiveEquals(
        {},
        wrap(1, "System.Int32"),
        wrap(1, "System.Int32"))), true);
});

test("String.GetHashCode uses one stable randomized Marvin seed", () => {
    const first = wrap("same text", "System.String");
    const second = wrap("same text", "System.String");
    const firstHash = unwrap(primitiveGetHashCode({}, first));

    assert.equal(firstHash, unwrap(primitiveGetHashCode({}, first)));
    assert.equal(firstHash, unwrap(primitiveGetHashCode({}, second)));
});
