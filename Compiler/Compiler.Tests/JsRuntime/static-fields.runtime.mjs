import assert from 'node:assert/strict';
import test from 'node:test';

import { handleFieldsInstruction } from '../../JsRuntime/jit-engine/fields.js';
import { createManagedException } from '../../JsRuntime/managed-exceptions.js';
import { throwDotNetException, unwrap, wrap } from '../../JsRuntime/msil-type-helpers.js';

function installType(initializer) {
    globalThis.DotNet = {
        Methods: initializer === undefined
            ? {}
            : { 'Example.Type..cctor()': initializer },
        Types: {
            'Example.Type': {
                statics: {},
                staticsDefinition: {
                    Value: { typeName: 'System.Int32' }
                }
            }
        }
    };
}

function accessStaticField(opcode, stack, ctx = {}) {
    handleFieldsInstruction({
        ctx,
        instruction: {
            oc: opcode,
            opr: {
                typeName: 'Example.Type',
                fieldName: 'Value'
            }
        },
        stack,
        genericParams: {}
    });
}

test('static field access runs the declaring type initializer once', () => {
    let calls = 0;
    installType(ctx => {
        calls++;
        accessStaticField('stsfld', [wrap(42, 'System.Int32')], ctx);
    });

    const firstStack = [];
    accessStaticField('ldsfld', firstStack);
    const secondStack = [];
    accessStaticField('ldsfld', secondStack);

    assert.equal(calls, 1);
    assert.equal(firstStack[0].v, 42);
    assert.equal(secondStack[0].v, 42);
});

test('static field access caches native type initializer failures', () => {
    const failure = new Error('initialization failed');
    let calls = 0;
    installType(() => {
        calls++;
        throw failure;
    });

    assert.throws(() => accessStaticField('ldsfld', []), error => error === failure);
    assert.throws(() => accessStaticField('ldsfld', []), error => error === failure);
    assert.equal(calls, 1);
});

test('static field access caches a managed TypeInitializationException', () => {
    const innerException = createManagedException('System.InvalidOperationException', 'initialization failed');
    let calls = 0;
    installType(ctx => {
        calls++;
        throwDotNetException(ctx, innerException);
    });

    const thrown = [];
    for (let attempt = 0; attempt < 2; attempt++) {
        assert.throws(
            () => accessStaticField('ldsfld', [], { stackTrace: `attempt ${attempt}` }),
            error => {
                thrown.push(error.dotNetException);
                assert.equal(error.name, 'System.TypeInitializationException');
                assert.equal(error.message, "The type initializer for 'Example.Type' threw an exception.");
                assert.equal(unwrap(error.dotNetException.v._typeName), 'Example.Type');
                assert.equal(unwrap(error.dotNetException.v._HResult), -2146233036);
                assert.strictEqual(error.dotNetException.v._innerException, innerException);
                return true;
            }
        );
    }

    assert.equal(calls, 1);
    assert.strictEqual(thrown[0], thrown[1]);
});
