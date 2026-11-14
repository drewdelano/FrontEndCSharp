import { throwManagedException } from "../managed-exceptions.js";
import { wrap, unwrap } from "../msil-type-helpers.js";

const checkedNativeIntTarget = Object.freeze({ min: -2147483648n, max: 2147483647n, type: "$NativeInt32", bigint: false });
const checkedInt8Target = Object.freeze({ min: -128n, max: 127n, type: "System.Int32", bigint: false });
const checkedInt16Target = Object.freeze({ min: -32768n, max: 32767n, type: "System.Int32", bigint: false });
const checkedInt32Target = Object.freeze({ min: -2147483648n, max: 2147483647n, type: "System.Int32", bigint: false });
const checkedInt64Target = Object.freeze({ min: -9223372036854775808n, max: 9223372036854775807n, type: "System.Int64", bigint: true });
const checkedNativeUIntTarget = Object.freeze({ min: 0n, max: 4294967295n, type: "$NativeUInt32", bigint: false });
const checkedUInt8Target = Object.freeze({ min: 0n, max: 255n, type: "System.Int32", bigint: false });
const checkedUInt16Target = Object.freeze({ min: 0n, max: 65535n, type: "System.Int32", bigint: false });
const checkedUInt32Target = Object.freeze({ min: 0n, max: 4294967295n, type: "System.UInt32", bigint: false });
const checkedUInt64Target = Object.freeze({ min: 0n, max: 18446744073709551615n, type: "System.UInt64", bigint: true });

const checkedConversion = (target, unsignedSource) => ({ stack, ctx }) => {
    stack.push(convertCheckedInteger(ctx, stack.pop(), target, unsignedSource));
};
const conversion = converter => ({ stack }) => stack.push(converter(stack.pop()));
const contextConversion = converter => ({ stack, ctx }) => stack.push(converter(ctx, stack.pop()));

export const instructionHandlers = Object.freeze({
    'conv.ovf.i': checkedConversion(checkedNativeIntTarget, false),
    'conv.ovf.i.un': checkedConversion(checkedNativeIntTarget, true),
    'conv.ovf.i1': checkedConversion(checkedInt8Target, false),
    'conv.ovf.i1.un': checkedConversion(checkedInt8Target, true),
    'conv.ovf.i2': checkedConversion(checkedInt16Target, false),
    'conv.ovf.i2.un': checkedConversion(checkedInt16Target, true),
    'conv.ovf.i4': checkedConversion(checkedInt32Target, false),
    'conv.ovf.i4.un': checkedConversion(checkedInt32Target, true),
    'conv.ovf.i8': checkedConversion(checkedInt64Target, false),
    'conv.ovf.i8.un': checkedConversion(checkedInt64Target, true),
    'conv.ovf.u': checkedConversion(checkedNativeUIntTarget, false),
    'conv.ovf.u.un': checkedConversion(checkedNativeUIntTarget, true),
    'conv.ovf.u1': checkedConversion(checkedUInt8Target, false),
    'conv.ovf.u1.un': checkedConversion(checkedUInt8Target, true),
    'conv.ovf.u2': checkedConversion(checkedUInt16Target, false),
    'conv.ovf.u2.un': checkedConversion(checkedUInt16Target, true),
    'conv.ovf.u4': checkedConversion(checkedUInt32Target, false),
    'conv.ovf.u4.un': checkedConversion(checkedUInt32Target, true),
    'conv.ovf.u8': checkedConversion(checkedUInt64Target, false),
    'conv.ovf.u8.un': checkedConversion(checkedUInt64Target, true),
    'conv.r.un': conversion(convertUnsignedIntegerToFloat), ckfinite: contextConversion(checkFinite),
    'conv.i': conversion(value => convertToNativeInteger(value, false)),
    'conv.i1': conversion(value => convertToShortInteger(value, 8, true)),
    'conv.i2': conversion(value => convertToShortInteger(value, 16, true)),
    'conv.i4': conversion(value => wrap(unwrap(value) | 0, 'System.Int32')),
    'conv.i8': conversion(value => wrap(BigInt(unwrap(value)), 'System.Int64')),
    'conv.u1': conversion(value => convertToShortInteger(value, 8, false)),
    'conv.u2': conversion(value => convertToShortInteger(value, 16, false)),
    'conv.u4': conversion(value => wrap(unwrap(value) >>> 0, 'System.UInt32')),
    'conv.u8': conversion(convertToUInt64),
    'conv.u': conversion(value => convertToNativeInteger(value, true)),
    'conv.r4': conversion(value => wrap(Number(unwrap(value)), 'System.Single')),
    'conv.r8': conversion(value => wrap(Number(unwrap(value)), 'System.Double'))
});

function convertToShortInteger(value, bitWidth, signed) {
    const rawValue = unwrap(value);
    let result;

    if (typeof rawValue === 'bigint') {
        result = Number(signed
            ? BigInt.asIntN(bitWidth, rawValue)
            : BigInt.asUintN(bitWidth, rawValue));
    } else if (typeof rawValue === 'number') {
        const shift = 32 - bitWidth;
        result = signed
            ? (rawValue << shift) >> shift
            : rawValue & (2 ** bitWidth - 1);
    } else {
        throw new TypeError('Fixed-width conversion requires an integer or floating-point operand.');
    }

    // ECMA-335 III.1.1.1: short integer conversions leave an int32 on the stack.
    return wrap(result, 'System.Int32');
}

function convertCheckedInteger(ctx, value, target, unsignedSource) {
    const integer = getCheckedSourceInteger(ctx, value, unsignedSource);
    if (integer < target.min || integer > target.max) {
        throwOverflowException(ctx);
    }

    return wrap(target.bigint ? integer : Number(integer), target.type);
}

function getCheckedSourceInteger(ctx, value, unsignedSource) {
    const rawValue = unwrap(value);
    if (typeof rawValue === "bigint") {
        return unsignedSource
            ? BigInt.asUintN(64, rawValue)
            : BigInt.asIntN(64, rawValue);
    }

    if (typeof rawValue !== "number") {
        throw new TypeError("Checked conversion requires an integer or floating-point operand.");
    }

    if (value.t === "System.Single" || value.t === "System.Double") {
        if (!Number.isFinite(rawValue)) {
            throwOverflowException(ctx);
        }
        return BigInt(Math.trunc(rawValue));
    }

    return BigInt(unsignedSource ? rawValue >>> 0 : rawValue | 0);
}

function throwOverflowException(ctx) {
    throwManagedException(ctx, "System.OverflowException");
}

function convertUnsignedIntegerToFloat(value) {
    const rawValue = unwrap(value);
    switch (value.t) {
        case "System.Int64":
        case "System.UInt64":
            if (typeof rawValue !== "bigint") {
                break;
            }
            return wrap(Number(BigInt.asUintN(64, rawValue)), "System.Double");

        case "System.SByte":
        case "System.Byte":
        case "System.Int16":
        case "System.UInt16":
        case "System.Int32":
        case "System.UInt32":
        case "$NativeInt32":
        case "$NativeUInt32":
            if (typeof rawValue !== "number" || !Number.isInteger(rawValue)) {
                break;
            }
            return wrap(rawValue >>> 0, "System.Double");
    }

    throw new TypeError("conv.r.un requires an integer operand.");
}

function checkFinite(ctx, value) {
    switch (value.t) {
        case "System.Single":
        case "System.Double":
            if (typeof value.v !== "number") {
                break;
            }
            if (!Number.isFinite(value.v)) {
                throwArithmeticException(ctx);
            }
            return value;
    }

    throw new TypeError("ckfinite requires a floating-point operand.");
}

function throwArithmeticException(ctx) {
    throwManagedException(ctx, "System.ArithmeticException");
}

function convertToNativeInteger(value, unsigned) {
    const rawValue = unwrap(value);
    let integer;
    if (typeof rawValue === "bigint") {
        integer = unsigned
            ? Number(BigInt.asUintN(32, rawValue))
            : Number(BigInt.asIntN(32, rawValue));
    } else if (typeof rawValue === "number") {
        const truncatedValue = Math.trunc(rawValue);
        integer = unsigned ? truncatedValue >>> 0 : truncatedValue | 0;
    } else {
        throw new TypeError("Native conversion requires an integer or floating-point operand.");
    }

    return wrap(integer, unsigned ? "$NativeUInt32" : "$NativeInt32");
}

function convertToUInt64(value) {
    const rawValue = unwrap(value);
    if (typeof rawValue === "bigint") {
        return wrap(BigInt.asUintN(64, rawValue), "System.UInt64");
    }

    if (typeof rawValue !== "number") {
        throw new TypeError("Unsigned 64-bit conversion requires an integer or floating-point operand.");
    }

    switch (value.t) {
        case "System.Single":
        case "System.Double":
            return wrap(
                BigInt.asUintN(64, BigInt(Number.isNaN(rawValue) ? 0 : Math.trunc(rawValue))),
                "System.UInt64"
            );

        default:
            // ECMA-335 defines conv.u8 from the 32-bit stack type as a zero extension.
            return wrap(BigInt(rawValue >>> 0), "System.UInt64");
    }
}
