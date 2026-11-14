import assert from 'node:assert/strict';
import test from 'node:test';

import { executeInstruction } from '../../JsRuntime/jit-engine/_executeInstruction.js';

function executeSizeof(typeName, genericParams = {}) {
    const stack = [];
    executeInstruction({
        instruction: { oc: 'sizeof', opr: { typeName } },
        stack,
        locals: [],
        methodArgs: [],
        genericParams,
        methodInfo: {}
    });
    return stack.pop();
}

test('sizeof returns exact fixed-width CLI primitive sizes', () => {
    assert.deepEqual(executeSizeof('System.Byte'), { t: 'System.UInt32', v: 1 });
    assert.deepEqual(executeSizeof('System.Char'), { t: 'System.UInt32', v: 2 });
    assert.deepEqual(executeSizeof('System.Int32'), { t: 'System.UInt32', v: 4 });
    assert.deepEqual(executeSizeof('System.Double'), { t: 'System.UInt32', v: 8 });
    assert.deepEqual(executeSizeof('System.Decimal'), { t: 'System.UInt32', v: 16 });
});

test('sizeof resolves generic primitive types', () => {
    assert.deepEqual(
        executeSizeof('TResult', { TResult: 'System.Int64' }),
        { t: 'System.UInt32', v: 8 });
});

test('sizeof rejects types without managed layout metadata', () => {
    assert.throws(
        () => executeSizeof('Example.Struct'),
        /without managed layout metadata/);
});

test('volatile prefix is a no-op in the single-threaded managed runtime', () => {
    const stack = [];
    executeInstruction({
        instruction: { oc: 'volatile.' },
        stack,
        locals: [],
        methodArgs: [],
        genericParams: {},
        methodInfo: {}
    });
    assert.deepEqual(stack, []);
});
