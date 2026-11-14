import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";
import { getDefaultValue, wrap } from "../../JsRuntime/msil-type-helpers.js";

function execute(opcode, stack) {
    executeInstruction({ instruction: { oc: opcode }, stack });
    return stack;
}

test("conv.i produces a signed 32-bit native integer", () => {
    assert.deepEqual(
        execute("conv.i", [wrap(4294967295, "System.UInt32")]),
        [wrap(-1, "$NativeInt32")]
    );
    assert.deepEqual(
        execute("conv.i", [wrap(4294967297n, "System.Int64")]),
        [wrap(1, "$NativeInt32")]
    );
    assert.deepEqual(
        execute("conv.i", [wrap(-3.9, "System.Double")]),
        [wrap(-3, "$NativeInt32")]
    );
});

test("conv.u produces an unsigned 32-bit native integer", () => {
    assert.deepEqual(
        execute("conv.u", [wrap(-1, "System.Int32")]),
        [wrap(4294967295, "$NativeUInt32")]
    );
    assert.deepEqual(
        execute("conv.u", [wrap(4294967297n, "System.UInt64")]),
        [wrap(1, "$NativeUInt32")]
    );
    assert.deepEqual(
        execute("conv.u", [wrap(-1.9, "System.Double")]),
        [wrap(4294967295, "$NativeUInt32")]
    );
});

test("conv.u8 zero-extends a 32-bit stack value", () => {
    assert.deepEqual(
        execute("conv.u8", [wrap(-1, "System.Int32")]),
        [wrap(4294967295n, "System.UInt64")]
    );
    assert.deepEqual(
        execute("conv.u8", [wrap(4294967295, "$NativeUInt32")]),
        [wrap(4294967295n, "System.UInt64")]
    );
});

test("native conversions reject nonnumeric operands", () => {
    assert.throws(
        () => execute("conv.i", [wrap("1", "System.String")]),
        /requires an integer or floating-point operand/
    );
    assert.throws(
        () => execute("conv.u", [wrap("1", "System.String")]),
        /requires an integer or floating-point operand/
    );
});

test("IntPtr and UIntPtr defaults use private native stack representations", () => {
    assert.deepEqual(getDefaultValue("System.IntPtr"), wrap(0, "$NativeInt32"));
    assert.deepEqual(getDefaultValue("System.UIntPtr"), wrap(0, "$NativeUInt32"));
});

test("ldelem.i loads signed and unsigned native integer elements", () => {
    assert.deepEqual(
        execute("ldelem.i", [
            wrap([wrap(-1, "$NativeInt32")], "System.IntPtr[]"),
            wrap(0, "System.Int32")
        ]),
        [wrap(-1, "$NativeInt32")]
    );
    assert.deepEqual(
        execute("ldelem.i", [
            wrap([wrap(4294967295, "$NativeUInt32")], "System.UIntPtr[]"),
            wrap(0, "System.Int32")
        ]),
        [wrap(4294967295, "$NativeUInt32")]
    );
});

test("stelem.i stores a native integer that ldelem.i can load", () => {
    const array = wrap([wrap(0, "$NativeInt32")], "System.IntPtr[]");

    assert.deepEqual(
        execute("stelem.i", [
            array,
            wrap(0, "System.Int32"),
            wrap(-2147483648, "$NativeInt32")
        ]),
        []
    );
    assert.deepEqual(array.v[0], wrap(-2147483648, "$NativeInt32"));
    assert.deepEqual(
        execute("ldelem.i", [array, wrap(0, "System.Int32")]),
        [wrap(-2147483648, "$NativeInt32")]
    );
});

test("native array opcodes reject non-native elements", () => {
    assert.throws(
        () => execute("ldelem.i", [
            wrap([wrap(1, "System.Int32")], "System.Int32[]"),
            wrap(0, "System.Int32")
        ]),
        /requires a native integer element/
    );
    assert.throws(
        () => execute("stelem.i", [
            wrap([wrap(0, "$NativeInt32")], "System.IntPtr[]"),
            wrap(0, "System.Int32"),
            wrap(1, "System.Int32")
        ]),
        /requires a native integer value/
    );
});

test("native array support does not change generic ldelem", () => {
    assert.deepEqual(
        execute("ldelem", [
            wrap([wrap(7, "System.Int32")], "System.Int32[]"),
            wrap(0, "System.Int32")
        ]),
        [wrap(7, "System.Int32")]
    );
});
