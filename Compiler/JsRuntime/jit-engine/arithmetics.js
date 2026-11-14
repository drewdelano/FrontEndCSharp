import { throwManagedException } from "../managed-exceptions.js";
import { wrap, unwrap } from "../msil-type-helpers.js";

const integerTypes = Object.freeze({
    "System.SByte": { bitWidth: 8, bigint: false }, "System.Byte": { bitWidth: 8, bigint: false },
    "System.Int16": { bitWidth: 16, bigint: false }, "System.UInt16": { bitWidth: 16, bigint: false },
    "System.Int32": { bitWidth: 32, bigint: false }, "System.UInt32": { bitWidth: 32, bigint: false },
    "$NativeInt32": { bitWidth: 32, bigint: false }, "$NativeUInt32": { bitWidth: 32, bigint: false },
    "System.Int64": { bitWidth: 64, bigint: true }, "System.UInt64": { bitWidth: 64, bigint: true }
});

const binary = operation => ({ stack }) => {
    const right = stack.pop();
    const left = stack.pop();
    stack.push(wrap(operation(unwrap(left), unwrap(right)), left.t));
};
const unary = operation => ({ stack }) => {
    const value = stack.pop();
    stack.push(wrap(operation(unwrap(value)), value.t));
};
const checked = (opcode, operation, unsigned) => ({ stack, ctx }) => {
    const right = stack.pop();
    const left = stack.pop();
    if (!left || !right) throw new Error(`Stack underflow in '${opcode}': left=${JSON.stringify(left)}, right=${JSON.stringify(right)}, stack depth was ${stack.length + 2}`);
    stack.push(checkedBinaryInteger(ctx, left, right, opcode, operation, unsigned));
};
const divide = (opcode, operation, unsigned) => ({ stack, ctx }) => {
    const right = stack.pop();
    const left = stack.pop();
    stack.push(divideOrRemainder(ctx, left, right, opcode, operation, unsigned));
};

export const instructionHandlers = Object.freeze({
    add: binary((left, right) => left + right),
    'add.ovf': checked('add.ovf', (left, right) => left + right, false),
    'add.ovf.un': checked('add.ovf.un', (left, right) => left + right, true),
    sub: binary((left, right) => left - right),
    'sub.ovf': checked('sub.ovf', (left, right) => left - right, false),
    'sub.ovf.un': checked('sub.ovf.un', (left, right) => left - right, true),
    mul: binary((left, right) => left * right),
    'mul.ovf': checked('mul.ovf', (left, right) => left * right, false),
    'mul.ovf.un': checked('mul.ovf.un', (left, right) => left * right, true),
    div: divide('div', (left, right) => left / right, false),
    'div.un': divide('div.un', (left, right) => left / right, true),
    rem: divide('rem', (left, right) => left % right, false),
    'rem.un': divide('rem.un', (left, right) => left % right, true),
    shl: binary((value, amount) => value << amount),
    shr: binary((value, amount) => value >> amount),
    'shr.un': binary((value, amount) => value >>> amount),
    and: binary((left, right) => left & right), or: binary((left, right) => left | right),
    xor: binary((left, right) => left ^ right), not: unary(value => ~value), neg: unary(value => -value)
});

function checkedBinaryInteger(ctx, left, right, opcode, operation, unsigned) {
    const type = requireMatchingIntegerOperands(left, right, opcode);
    const result = operation(
        toStackInteger(left, type.bitWidth, unsigned),
        toStackInteger(right, type.bitWidth, unsigned));
    const minimum = unsigned ? 0n : -(1n << BigInt(type.bitWidth - 1));
    const maximum = unsigned ? (1n << BigInt(type.bitWidth)) - 1n : (1n << BigInt(type.bitWidth - 1)) - 1n;
    if (result < minimum || result > maximum) throwManagedException(ctx, 'System.OverflowException');
    return wrap(type.bigint ? result : Number(result), left.t);
}
function divideOrRemainder(ctx, left, right, opcode, operation, unsigned) {
    if (left?.t === 'System.Single' || left?.t === 'System.Double') {
        if (right?.t !== left.t || unsigned) throw new TypeError(`${opcode} requires matching numeric operands.`);
        return wrap(operation(unwrap(left), unwrap(right)), left.t);
    }
    const type = requireMatchingIntegerOperands(left, right, opcode);
    const dividend = toStackInteger(left, type.bitWidth, unsigned);
    const divisor = toStackInteger(right, type.bitWidth, unsigned);
    if (divisor === 0n) throwManagedException(ctx, 'System.DivideByZeroException');
    if (!unsigned && dividend === -(1n << BigInt(type.bitWidth - 1)) && divisor === -1n) {
        throwManagedException(ctx, 'System.OverflowException');
    }
    const result = operation(dividend, divisor);
    return wrap(type.bigint ? result : Number(result), left.t);
}
function requireMatchingIntegerOperands(left, right, opcode) {
    const type = integerTypes[left?.t];
    if (type === undefined || integerTypes[right?.t] !== type) throw new TypeError(`${opcode} requires matching integer operands.`);
    return type;
}
function toStackInteger(value, bitWidth, unsigned) {
    const integer = BigInt(unwrap(value));
    return unsigned ? BigInt.asUintN(bitWidth, integer) : BigInt.asIntN(bitWidth, integer);
}
