import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";
import { DotNetError, wrap } from "../../JsRuntime/msil-type-helpers.js";

function convert(opcode, value) {
    const stack = [value];
    executeInstruction({
        ctx: { stackTrace: "   at Test.CheckedConversion()" },
        instruction: { oc: opcode },
        stack
    });
    assert.equal(stack.length, 1);
    return stack[0];
}

function assertOverflow(opcode, value) {
    assert.throws(
        () => convert(opcode, value),
        error => {
            assert.ok(error instanceof DotNetError);
            assert.equal(error.name, "System.OverflowException");
            assert.equal(error.message, "Arithmetic operation resulted in an overflow.");
            assert.equal(error.dotNetException.t, "System.OverflowException");
            assert.equal(error.dotNetException.v._stackTraceString.v, "   at Test.CheckedConversion()");
            return true;
        }
    );
}

const targetCases = [
    { suffix: "i", min: -2147483648n, max: 2147483647n, type: "$NativeInt32", value: 1 },
    { suffix: "i1", min: -128n, max: 127n, type: "System.Int32", value: 1 },
    { suffix: "i2", min: -32768n, max: 32767n, type: "System.Int32", value: 1 },
    { suffix: "i4", min: -2147483648n, max: 2147483647n, type: "System.Int32", value: 1 },
    { suffix: "i8", min: -9223372036854775808n, max: 9223372036854775807n, type: "System.Int64", value: 1n },
    { suffix: "u", min: 0n, max: 4294967295n, type: "$NativeUInt32", value: 1 },
    { suffix: "u1", min: 0n, max: 255n, type: "System.Int32", value: 1 },
    { suffix: "u2", min: 0n, max: 65535n, type: "System.Int32", value: 1 },
    { suffix: "u4", min: 0n, max: 4294967295n, type: "System.UInt32", value: 1 },
    { suffix: "u8", min: 0n, max: 18446744073709551615n, type: "System.UInt64", value: 1n }
];

test("every checked conversion opcode is recognized and produces its target stack type", () => {
    for (const { suffix, type, value } of targetCases) {
        assert.deepEqual(
            convert(`conv.ovf.${suffix}`, wrap(1, "System.Int32")),
            wrap(value, type)
        );
        assert.deepEqual(
            convert(`conv.ovf.${suffix}.un`, wrap(1, "System.Int32")),
            wrap(value, type)
        );
    }
});

test("checked conversions accept target boundaries and reject adjacent representable values", () => {
    for (const { suffix, min, max, type } of targetCases.filter(({ suffix }) => suffix !== "i8" && suffix !== "u8")) {
        const minResult = convert(`conv.ovf.${suffix}`, wrap(min, "System.Int64"));
        const maxResult = convert(`conv.ovf.${suffix}`, wrap(max, "System.Int64"));
        assert.equal(minResult.t, type);
        assert.equal(BigInt(minResult.v), min);
        assert.equal(maxResult.t, type);
        assert.equal(BigInt(maxResult.v), max);

        assertOverflow(`conv.ovf.${suffix}`, wrap(min - 1n, "System.Int64"));
        assertOverflow(`conv.ovf.${suffix}`, wrap(max + 1n, "System.Int64"));
    }

    assert.deepEqual(
        convert("conv.ovf.i8", wrap(-9223372036854775808n, "System.Int64")),
        wrap(-9223372036854775808n, "System.Int64")
    );
    assert.deepEqual(
        convert("conv.ovf.i8", wrap(9223372036854775807n, "System.Int64")),
        wrap(9223372036854775807n, "System.Int64")
    );
    assertOverflow("conv.ovf.i8.un", wrap(-1n, "System.Int64"));

    assert.deepEqual(
        convert("conv.ovf.u8", wrap(0n, "System.Int64")),
        wrap(0n, "System.UInt64")
    );
    assert.deepEqual(
        convert("conv.ovf.u8.un", wrap(-1n, "System.Int64")),
        wrap(18446744073709551615n, "System.UInt64")
    );
    assertOverflow("conv.ovf.u8", wrap(-1n, "System.Int64"));
    assertOverflow("conv.ovf.u8", wrap(18446744073709551616, "System.Double"));
});

test("the .un variants reinterpret int32 and int64 source bits as unsigned", () => {
    assert.deepEqual(
        convert("conv.ovf.i1", wrap(-1, "System.Int32")),
        wrap(-1, "System.Int32")
    );
    assertOverflow("conv.ovf.i1.un", wrap(-1, "System.Int32"));

    assert.deepEqual(
        convert("conv.ovf.u4.un", wrap(-1, "System.Int32")),
        wrap(4294967295, "System.UInt32")
    );
    assert.deepEqual(
        convert("conv.ovf.i8.un", wrap(-1, "System.Int32")),
        wrap(4294967295n, "System.Int64")
    );
    assertOverflow("conv.ovf.u4.un", wrap(-1n, "System.Int64"));

    assertOverflow("conv.ovf.u8", wrap(-1n, "System.Int64"));
    assert.deepEqual(
        convert("conv.ovf.u8.un", wrap(-1n, "System.Int64")),
        wrap(18446744073709551615n, "System.UInt64")
    );
});

test("checked floating-point conversions truncate toward zero before checking range", () => {
    assert.deepEqual(
        convert("conv.ovf.i1", wrap(127.9, "System.Double")),
        wrap(127, "System.Int32")
    );
    assert.deepEqual(
        convert("conv.ovf.i1.un", wrap(-128.9, "System.Double")),
        wrap(-128, "System.Int32")
    );
    assertOverflow("conv.ovf.i1", wrap(128, "System.Double"));
    assertOverflow("conv.ovf.i4", wrap(Number.NaN, "System.Double"));
    assertOverflow("conv.ovf.i4", wrap(Number.POSITIVE_INFINITY, "System.Double"));
    assertOverflow("conv.ovf.i4", wrap(Number.NEGATIVE_INFINITY, "System.Double"));
});

test("checked conversions reject nonnumeric operands", () => {
    assert.throws(
        () => convert("conv.ovf.i4", wrap("1", "System.String")),
        /requires an integer or floating-point operand/
    );
});
