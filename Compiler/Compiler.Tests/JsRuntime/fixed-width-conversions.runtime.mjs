import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";
import { wrap } from "../../JsRuntime/msil-type-helpers.js";

function convert(opcode, value) {
    const stack = [value];
    executeInstruction({ instruction: { oc: opcode }, stack });
    assert.equal(stack.length, 1);
    return stack[0];
}

test("conv.i1 truncates to eight bits and sign-extends to int32", () => {
    assert.deepEqual(convert("conv.i1", wrap(0x7f, "System.Int32")), wrap(127, "System.Int32"));
    assert.deepEqual(convert("conv.i1", wrap(0x80, "System.Int32")), wrap(-128, "System.Int32"));
    assert.deepEqual(convert("conv.i1", wrap(0xff, "System.Int32")), wrap(-1, "System.Int32"));
    assert.deepEqual(convert("conv.i1", wrap(0x100, "System.Int32")), wrap(0, "System.Int32"));
});

test("conv.i2 truncates to sixteen bits and sign-extends to int32", () => {
    assert.deepEqual(convert("conv.i2", wrap(0x7fff, "System.Int32")), wrap(32767, "System.Int32"));
    assert.deepEqual(convert("conv.i2", wrap(0x8000, "System.Int32")), wrap(-32768, "System.Int32"));
    assert.deepEqual(convert("conv.i2", wrap(0xffff, "System.Int32")), wrap(-1, "System.Int32"));
    assert.deepEqual(convert("conv.i2", wrap(0x10000, "System.Int32")), wrap(0, "System.Int32"));
});

test("conv.u1 and conv.u2 truncate and zero-extend to int32", () => {
    assert.deepEqual(convert("conv.u1", wrap(-1, "System.Int32")), wrap(255, "System.Int32"));
    assert.deepEqual(convert("conv.u1", wrap(0x100, "System.Int32")), wrap(0, "System.Int32"));
    assert.deepEqual(convert("conv.u2", wrap(-1, "System.Int32")), wrap(65535, "System.Int32"));
    assert.deepEqual(convert("conv.u2", wrap(0x10000, "System.Int32")), wrap(0, "System.Int32"));
});

test("fixed-width conversions truncate int64 operands at the target width", () => {
    assert.deepEqual(convert("conv.i1", wrap(0x180n, "System.Int64")), wrap(-128, "System.Int32"));
    assert.deepEqual(convert("conv.i2", wrap(0x18000n, "System.Int64")), wrap(-32768, "System.Int32"));
    assert.deepEqual(convert("conv.u1", wrap(-1n, "System.Int64")), wrap(255, "System.Int32"));
    assert.deepEqual(convert("conv.u2", wrap(-1n, "System.Int64")), wrap(65535, "System.Int32"));
});

test("fixed-width conversions truncate floating-point operands toward zero", () => {
    assert.deepEqual(convert("conv.i1", wrap(130.9, "System.Double")), wrap(-126, "System.Int32"));
    assert.deepEqual(convert("conv.i2", wrap(-32769.9, "System.Double")), wrap(32767, "System.Int32"));
    assert.deepEqual(convert("conv.u1", wrap(-1.9, "System.Single")), wrap(255, "System.Int32"));
    assert.deepEqual(convert("conv.u2", wrap(65537.9, "System.Double")), wrap(1, "System.Int32"));
});

test("fixed-width conversions reject nonnumeric operands", () => {
    assert.throws(
        () => convert("conv.i1", wrap("1", "System.String")),
        /requires an integer or floating-point operand/
    );
});
