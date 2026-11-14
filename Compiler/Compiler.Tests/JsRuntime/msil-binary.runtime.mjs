import assert from 'node:assert/strict';
import test from 'node:test';

import { decodeMsilPackage } from '../../JsRuntime/msil-binary.js';

const validPackage = Uint8Array.from([
    0x46, 0x45, 0x43, 0x53, 0x49, 0x4c, 0x0d, 0x0a, // FECSIL\r\n
    7,                                                    // version
    2,                                                    // strings
    0, 12,                                                // no prefix, 12-byte suffix
    0x6d, 0x65, 0x74, 0x68, 0x6f, 0x64, 0x2e, 0x61,
    0x6c, 0x70, 0x68, 0x61,                               // "method.alpha"
    7, 4,                                                 // "method." prefix, 4-byte suffix
    0x62, 0x65, 0x74, 0x61,                               // "method.beta"
    1,                                                    // opcodes
    0x20, 0x00, 1,                                        // method.beta -> string 1
    0,                                                    // types
    1,                                                    // methods
    0,                                                    // key -> string 0
    1,                                                    // registration name -> string 1
    8, 0,                                                 // empty method metadata object
    1,                                                    // blocks
    0,                                                    // IL offset
    1,                                                    // instructions
    0x20, 0x00,                                           // ldc.i4
    1,                                                    // has operand
    3, 13                                                 // signed integer -7
]);

test('binary MSIL decoding inflates the interpreter block notation', () => {
    const decoded = decodeMsilPackage(validPackage);

    assert.equal(decoded.version, 7);
    assert.equal(decoded.types.size, 0);
    assert.equal(decoded.methodDefinitions[0].name, 'method.beta');
    assert.deepEqual(decoded.methods.get('method.alpha'), [
        {
            ilOffset: 0,
            instructions: [{ oc: 'method.beta', opr: -7 }]
        }
    ]);
});

test('binary MSIL decoding rejects incompatible versions', () => {
    const incompatible = validPackage.slice();
    incompatible[8] = 6;

    assert.throws(
        () => decodeMsilPackage(incompatible),
        /version 6 is unsupported/);
});

test('binary MSIL decoding preserves a negative-zero Single operand', () => {
    const singlePackage = new Uint8Array(validPackage.length + 3);
    singlePackage.set(validPackage.subarray(0, validPackage.length - 2));
    singlePackage.set([9, 0x00, 0x00, 0x00, 0x80], validPackage.length - 2);

    const decoded = decodeMsilPackage(singlePackage);

    assert.ok(Object.is(
        decoded.methods.get('method.alpha')[0].instructions[0].opr,
        -0));
});

test('binary MSIL decoding rejects a trie prefix longer than the previous string', () => {
    const invalidPrefix = validPackage.slice();
    invalidPrefix[24] = 13;

    assert.throws(
        () => decodeMsilPackage(invalidPrefix),
        /prefix length 13 exceeds the previous string's 12 bytes/);
});

test('binary MSIL decoding rejects trailing data', () => {
    const trailing = new Uint8Array(validPackage.length + 1);
    trailing.set(validPackage);

    assert.throws(
        () => decodeMsilPackage(trailing),
        /trailing data/);
});
