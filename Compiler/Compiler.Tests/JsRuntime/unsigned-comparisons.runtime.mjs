import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";
import { wrap } from "../../JsRuntime/msil-type-helpers.js";

const targetOffset = 20;

function branch(opcode, left, right) {
    const opts = {
        instruction: { oc: opcode, opr: targetOffset },
        stack: [left, right],
        blocks: [{ ilOffset: 0 }, { ilOffset: targetOffset }],
        currentBlockIndex: 0,
        instructionIndex: 1
    };

    executeInstruction(opts);
    return opts.currentBlockIndex === 1;
}

function compare(opcode, left, right) {
    const stack = [left, right];
    executeInstruction({ instruction: { oc: opcode }, stack });
    return stack.pop();
}

test("unsigned branch comparisons reinterpret int32 operands", () => {
    const minusOne = wrap(-1, "System.Int32");
    const one = wrap(1, "System.Int32");

    assert.equal(branch("bgt.un", minusOne, one), true);
    assert.equal(branch("bgt.un.s", minusOne, one), true);
    assert.equal(branch("ble.un", minusOne, one), false);
    assert.equal(branch("ble.un.s", minusOne, one), false);
});

test("unsigned branch comparisons reinterpret int64 operands", () => {
    const minusOne = wrap(-1n, "System.Int64");
    const one = wrap(1n, "System.Int64");

    assert.equal(branch("bgt.un", minusOne, one), true);
    assert.equal(branch("ble.un", minusOne, one), false);
});

test("unsigned branch comparisons branch when floating-point operands are unordered", () => {
    const nan = wrap(Number.NaN, "System.Double");
    const one = wrap(1, "System.Double");

    for (const opcode of ["bgt.un", "bgt.un.s", "ble.un", "ble.un.s"]) {
        assert.equal(branch(opcode, nan, one), true, `${opcode} with a NaN left operand`);
        assert.equal(branch(opcode, one, nan), true, `${opcode} with a NaN right operand`);
    }
});

test("unsigned branch comparisons preserve ordered floating-point comparisons", () => {
    const larger = wrap(1.5, "System.Double");
    const smaller = wrap(1.4, "System.Double");

    assert.equal(branch("bgt.un", larger, smaller), true);
    assert.equal(branch("ble.un", larger, smaller), false);
});

test("clt.un reinterprets integer operands and returns an int32", () => {
    assert.deepEqual(
        compare("clt.un", wrap(1, "System.Int32"), wrap(-1, "System.Int32")),
        wrap(1, "System.Int32")
    );
    assert.deepEqual(
        compare("clt.un", wrap(-1n, "System.Int64"), wrap(1n, "System.Int64")),
        wrap(0, "System.Int32")
    );
});

test("clt.un returns one when floating-point operands are unordered", () => {
    const nan = wrap(Number.NaN, "System.Single");
    const one = wrap(1, "System.Single");

    assert.deepEqual(compare("clt.un", nan, one), wrap(1, "System.Int32"));
    assert.deepEqual(compare("clt.un", one, nan), wrap(1, "System.Int32"));
});

test("all existing unsigned relational comparisons use int64 and unordered semantics", () => {
    const minusOne = wrap(-1n, "System.Int64");
    const one = wrap(1n, "System.Int64");
    const nan = wrap(Number.NaN, "System.Double");
    const zero = wrap(0, "System.Double");

    assert.equal(branch("bge.un", minusOne, one), true);
    assert.equal(branch("blt.un", minusOne, one), false);
    assert.equal(branch("bge.un", nan, zero), true);
    assert.equal(branch("blt.un", zero, nan), true);
    assert.deepEqual(compare("cgt.un", nan, zero), wrap(1, "System.Int32"));
});

test("cgt.un continues to support reference comparisons against null", () => {
    assert.deepEqual(
        compare("cgt.un", wrap({}, "Example.Reference"), null),
        wrap(1, "System.Int32")
    );
    assert.deepEqual(compare("cgt.un", null, null), wrap(0, "System.Int32"));
});
