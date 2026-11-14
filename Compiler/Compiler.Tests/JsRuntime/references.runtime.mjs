import assert from 'node:assert/strict';
import test from 'node:test';

import { executeInstruction } from '../../JsRuntime/jit-engine/_executeInstruction.js';
import { wrap } from '../../JsRuntime/msil-type-helpers.js';

function execute(opcode, stack, methodArgs = [], operand = undefined) {
    executeInstruction({
        instruction: { oc: opcode, opr: operand },
        stack,
        locals: [],
        methodArgs,
        genericTypeArgs: {},
        methodInfo: {}
    });
}

test('indirect loads read managed reference cells', () => {
    const value = wrap(42, 'System.Int32');
    const stack = [{ value }];

    execute('ldind.i4', stack);

    assert.deepEqual(stack, [value]);
});

test('indirect stores update managed reference cells', () => {
    const cell = { value: wrap(1, 'System.Int32') };
    const replacement = wrap(42, 'System.Int32');
    const stack = [cell, replacement];

    execute('stind.i4', stack);

    assert.deepEqual(stack, []);
    assert.deepEqual(cell.value, replacement);
});

test('indirect loads reject values that are not managed reference cells', () => {
    const stack = [wrap(42, 'System.Int32')];

    assert.throws(
        () => execute('ldind.i4', stack),
        /managed reference cell/);
});

test('indirect stores reject values that are not managed reference cells', () => {
    const stack = [wrap(1, 'System.Int32'), wrap(42, 'System.Int32')];

    assert.throws(
        () => execute('stind.i4', stack),
        /managed reference cell/);
});

test('argument addresses provide reference cells for ordinary arguments', () => {
    const original = wrap(1, 'System.Int32');
    const replacement = wrap(42, 'System.Int32');
    const methodArgs = [original];
    const stack = [];

    execute('ldarga', stack, methodArgs, 0);
    execute('ldobj', stack, methodArgs);
    assert.deepEqual(stack.pop(), original);

    execute('ldarga', stack, methodArgs, 0);
    stack.push(replacement);
    execute('stobj', stack, methodArgs);
    assert.deepEqual(methodArgs[0], replacement);
});
