import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";
import { DotNetError, wrap } from "../../JsRuntime/msil-type-helpers.js";

function execute(opcode, left, right) {
    const stack = [left, right];
    executeInstruction({
        ctx: { stackTrace: "   at Test.Arithmetic()" },
        instruction: { oc: opcode },
        stack
    });
    assert.equal(stack.length, 1);
    return stack[0];
}

function assertManagedException(typeName, action) {
    assert.throws(
        action,
        error => {
            assert.ok(error instanceof DotNetError);
            assert.equal(error.name, typeName);
            assert.equal(error.dotNetException.t, typeName);
            assert.equal(error.dotNetException.v._innerException, null);
            assert.equal(error.dotNetException.v._stackTraceString.v, "   at Test.Arithmetic()");
            return true;
        }
    );
}

test("integral division truncates toward zero for int32 and int64", () => {
    assert.deepEqual(
        execute("div", wrap(-7, "System.Int32"), wrap(2, "System.Int32")),
        wrap(-3, "System.Int32")
    );
    assert.deepEqual(
        execute("div", wrap(7n, "System.Int64"), wrap(-2n, "System.Int64")),
        wrap(-3n, "System.Int64")
    );
});

test("unsigned division and remainder reinterpret the operand bits", () => {
    assert.deepEqual(
        execute("div.un", wrap(-1, "System.Int32"), wrap(2, "System.Int32")),
        wrap(2147483647, "System.Int32")
    );
    assert.deepEqual(
        execute("rem.un", wrap(-1n, "System.Int64"), wrap(10n, "System.Int64")),
        wrap(5n, "System.Int64")
    );
});

test("integral division and remainder by zero throw DivideByZeroException", () => {
    for (const opcode of ["div", "div.un", "rem", "rem.un"]) {
        assertManagedException(
            "System.DivideByZeroException",
            () => execute(opcode, wrap(1, "System.Int32"), wrap(0, "System.Int32"))
        );
    }
});

test("minimum signed integer divided or remaindered by negative one throws OverflowException", () => {
    for (const opcode of ["div", "rem"]) {
        assertManagedException(
            "System.OverflowException",
            () => execute(opcode, wrap(-2147483648, "System.Int32"), wrap(-1, "System.Int32"))
        );
        assertManagedException(
            "System.OverflowException",
            () => execute(opcode, wrap(-9223372036854775808n, "System.Int64"), wrap(-1n, "System.Int64"))
        );
    }
});

test("floating-point division and remainder retain IEEE behavior", () => {
    assert.equal(execute("div", wrap(1, "System.Double"), wrap(0, "System.Double")).v, Number.POSITIVE_INFINITY);
    assert.ok(Number.isNaN(execute("rem", wrap(1, "System.Double"), wrap(0, "System.Double")).v));
});

test("checked arithmetic returns exact integral results and throws OverflowException", () => {
    assert.deepEqual(
        execute("mul.ovf", wrap(100000, "System.Int32"), wrap(10000, "System.Int32")),
        wrap(1000000000, "System.Int32")
    );
    assert.deepEqual(
        execute("add.ovf.un", wrap(2147483648, "System.UInt32"), wrap(1, "System.UInt32")),
        wrap(2147483649, "System.UInt32")
    );

    for (const opcode of ["add.ovf", "sub.ovf", "mul.ovf"]) {
        assertManagedException(
            "System.OverflowException",
            () => execute(opcode, wrap(2147483647, "System.Int32"), wrap(opcode === "sub.ovf" ? -1 : 2, "System.Int32"))
        );
    }
    assertManagedException(
        "System.OverflowException",
        () => execute("add.ovf.un", wrap(-1, "System.Int32"), wrap(1, "System.Int32"))
    );
});
