import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";
import { DotNetError, wrap } from "../../JsRuntime/msil-type-helpers.js";

globalThis.DotNet = {
    Types: {
        "Tests.Base": { isClass: true, baseType: "System.Object", interfaces: [] },
        "Tests.Derived": { isClass: true, baseType: "Tests.Base", interfaces: [] },
        "Tests.Unrelated": { isClass: true, baseType: "System.Object", interfaces: [] }
    }
};

function execute(opcode, stack, operand = null, genericParams = {}) {
    executeInstruction({
        ctx: { stackTrace: "   at Test.ArrayOperation()" },
        genericParams,
        instruction: { oc: opcode, opr: operand },
        stack
    });
    return stack;
}

function assertManagedException(typeName, action) {
    assert.throws(
        action,
        error => {
            assert.ok(error instanceof DotNetError);
            assert.equal(error.name, typeName);
            assert.equal(error.dotNetException.t, typeName);
            assert.equal(error.dotNetException.v._stackTraceString.v, "   at Test.ArrayOperation()");
            return true;
        }
    );
}

test("array operations on null throw NullReferenceException", () => {
    assertManagedException(
        "System.NullReferenceException",
        () => execute("ldlen", [wrap(null, "System.Int32[]")])
    );
    assertManagedException(
        "System.NullReferenceException",
        () => execute("ldelem.i4", [wrap(null, "System.Int32[]"), wrap(0, "System.Int32")])
    );
    assertManagedException(
        "System.NullReferenceException",
        () => execute("ldelema", [wrap(null, "System.Int32[]"), wrap(0, "System.Int32")], { typeName: "System.Int32" })
    );
    assertManagedException(
        "System.NullReferenceException",
        () => execute("stelem.i4", [wrap(null, "System.Int32[]"), wrap(0, "System.Int32"), wrap(1, "System.Int32")])
    );
});

test("newarr with a negative length throws OverflowException", () => {
    assertManagedException(
        "System.OverflowException",
        () => execute("newarr", [wrap(-1, "System.Int32")], { typeName: "System.Int32" })
    );
});

test("array loads and stores outside the array throw IndexOutOfRangeException", () => {
    const array = wrap([wrap(0, "System.Int32")], "System.Int32[]");
    for (const index of [-1, 1]) {
        assertManagedException(
            "System.IndexOutOfRangeException",
            () => execute("ldelem.i4", [array, wrap(index, "System.Int32")])
        );
        assertManagedException(
            "System.IndexOutOfRangeException",
            () => execute("ldelema", [array, wrap(index, "System.Int32")], { typeName: "System.Int32" })
        );
        assertManagedException(
            "System.IndexOutOfRangeException",
            () => execute("stelem.i4", [array, wrap(index, "System.Int32"), wrap(1, "System.Int32")])
        );
    }
});

test("ldelema returns a live managed reference cell for one array element", () => {
    const first = wrap(1, "System.Int32");
    const second = wrap(2, "System.Int32");
    const replacement = wrap(42, "System.Int32");
    const array = wrap([first, second], "System.Int32[]");
    const stack = execute(
        "ldelema",
        [array, wrap(1, "System.Int32")],
        { typeName: "System.Int32" }
    );
    const cell = stack.pop();

    assert.strictEqual(cell.value, second);
    execute("stind.i4", [cell, replacement]);
    assert.strictEqual(array.v[1], replacement);
    assert.strictEqual(array.v[0], first);

    const loadStack = execute(
        "ldelema",
        [array, wrap(1, "System.Int32")],
        { typeName: "System.Int32" }
    );
    execute("ldind.i4", loadStack);
    assert.strictEqual(loadStack.pop(), replacement);
});

test("ldelema resolves a generic element operand before validating the runtime array type", () => {
    const element = wrap(42, "System.Int32");
    const stack = execute(
        "ldelema",
        [wrap([element], "System.Int32[]"), wrap(0, "System.Int32")],
        { typeName: "T" },
        { T: "System.Int32" }
    );

    assert.strictEqual(stack.pop().value, element);
});

test("ldelema enforces the runtime array element type", () => {
    const array = wrap([wrap({}, "Tests.Derived")], "Tests.Derived[]");

    assertManagedException(
        "System.ArrayTypeMismatchException",
        () => execute(
            "ldelema",
            [array, wrap(0, "System.Int32")],
            { typeName: "Tests.Base" }
        )
    );
});

test("reference-array stores enforce the runtime element type", () => {
    const array = wrap([null], "Tests.Base[]");
    const derived = wrap({}, "Tests.Derived");
    execute("stelem.ref", [array, wrap(0, "System.Int32"), derived]);
    assert.strictEqual(array.v[0], derived);

    execute("stelem.ref", [array, wrap(0, "System.Int32"), wrap(null, "System.Object")]);
    assert.equal(array.v[0].v, null);

    assertManagedException(
        "System.ArrayTypeMismatchException",
        () => execute("stelem.ref", [array, wrap(0, "System.Int32"), wrap({}, "Tests.Unrelated")])
    );
});
