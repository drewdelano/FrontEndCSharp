import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";
import { ExecuteMSIL } from "../../JsRuntime/msil.js";
import { DotNetError, wrap } from "../../JsRuntime/msil-type-helpers.js";

function execute(opcode, value) {
    const stack = [value];
    executeInstruction({
        ctx: { stackTrace: "   at Test.FloatingConversion()" },
        instruction: { oc: opcode },
        stack
    });
    return stack;
}

test("conv.r.un converts int32 source bits as unsigned", () => {
    assert.deepEqual(
        execute("conv.r.un", wrap(-1, "System.Int32")),
        [wrap(4294967295, "System.Double")]
    );
    assert.deepEqual(
        execute("conv.r.un", wrap(-2147483648, "System.Int32")),
        [wrap(2147483648, "System.Double")]
    );
});

test("conv.r.un converts int64 source bits as unsigned", () => {
    assert.deepEqual(
        execute("conv.r.un", wrap(-1n, "System.Int64")),
        [wrap(Number(18446744073709551615n), "System.Double")]
    );
    assert.deepEqual(
        execute("conv.r.un", wrap(9223372036854775807n, "System.Int64")),
        [wrap(Number(9223372036854775807n), "System.Double")]
    );
});

test("conv.r.un requires an integer stack operand", () => {
    assert.throws(
        () => execute("conv.r.un", wrap(1.5, "System.Double")),
        /requires an integer operand/
    );
    assert.throws(
        () => execute("conv.r.un", wrap("1", "System.String")),
        /requires an integer operand/
    );
});

test("ckfinite leaves finite floating-point values unchanged", () => {
    const doubleValue = wrap(-0, "System.Double");
    const singleValue = wrap(Math.fround(3.5), "System.Single");

    assert.strictEqual(execute("ckfinite", doubleValue)[0], doubleValue);
    assert.strictEqual(execute("ckfinite", singleValue)[0], singleValue);
});

test("ckfinite throws a managed ArithmeticException for every non-finite value", () => {
    for (const value of [Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY]) {
        assert.throws(
            () => execute("ckfinite", wrap(value, "System.Double")),
            error => {
                assert.ok(error instanceof DotNetError);
                assert.equal(error.name, "System.ArithmeticException");
                assert.equal(error.dotNetException.t, "System.ArithmeticException");
                assert.equal(error.dotNetException.v._stackTraceString.v, "   at Test.FloatingConversion()");
                return true;
            }
        );
    }
});

test("ckfinite requires a floating-point operand", () => {
    assert.throws(
        () => execute("ckfinite", wrap(1, "System.Int32")),
        /requires a floating-point operand/
    );
});

test("ArithmeticException from ckfinite is catchable by managed code", () => {
    const result = ExecuteMSIL(
        {},
        {
            fullMethodName: "Test.CheckFinite()",
            paramType: [],
            localTypes: [],
            hasThis: false,
            exceptionHandlers: [{
                Flags: "Clause",
                CatchType: "System.ArithmeticException",
                TryStartBlockIndex: 0,
                TryEndBlockIndex: 1,
                HandlerStartBlockIndex: 1,
                HandlerEndBlockIndex: 2
            }]
        },
        [
            {
                ilOffset: 0,
                instructions: [
                    { oc: "ldc.r8", opr: Number.NaN },
                    { oc: "ckfinite", opr: null },
                    { oc: "ldc.i4.0", opr: null },
                    { oc: "ret", opr: null }
                ]
            },
            {
                ilOffset: 10,
                instructions: [
                    { oc: "pop", opr: null },
                    { oc: "ldc.i4.1", opr: null },
                    { oc: "ret", opr: null }
                ]
            }
        ],
        []
    );

    assert.deepEqual(result, wrap(1, "System.Int32"));
});
