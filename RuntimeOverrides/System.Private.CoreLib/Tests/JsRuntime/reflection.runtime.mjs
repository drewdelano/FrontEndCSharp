import assert from "node:assert/strict";
import test from "node:test";

import { getMemberName } from "../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/reflection.js";
import { unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("member names support managed Type values", () => {
    assert.equal(
        unwrap(getMemberName(wrap("Tests.Sample", "System.Type"))),
        "Sample");
    assert.equal(
        unwrap(getMemberName(wrap("Tests.Container+Nested", "System.Type"))),
        "Nested");
});
