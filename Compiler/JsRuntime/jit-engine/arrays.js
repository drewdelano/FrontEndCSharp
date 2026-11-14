import { throwManagedException } from "../managed-exceptions.js";
import {
    formatManagedTypeReference,
    getDefaultValue,
    isReferenceType,
    isTypeAssignableTo,
    resolveManagedGenericArguments,
    resolveManagedTypeReference,
    unwrap,
    wrap
} from "../msil-type-helpers.js";

function loadLength({ stack, ctx }) {
    stack.push(wrap(requireArray(ctx, stack.pop(), 'ldlen').length, 'System.Int32'));
}

function newArray({ stack, ctx, genericParams, instruction: { opr } }) {
    const length = unwrap(stack.pop());
    if (!Number.isInteger(length)) throw new TypeError('newarr requires an integer length.');
    if (length < 0) throwManagedException(ctx, 'System.OverflowException');
    const elementReference = opr ?? 'System.Object';
    const resolvedElement = resolveManagedTypeReference(elementReference, genericParams);
    const defaultValue = getDefaultValue(resolvedElement);
    stack.push(wrap(
        new Array(length).fill(defaultValue),
        `${formatManagedTypeReference(resolvedElement)}[]`));
}

function loadNative({ stack, ctx }) {
    const index = unwrap(stack.pop());
    const array = requireArray(ctx, stack.pop(), 'ldelem.i');
    stack.push(loadNativeIntegerElement(getArrayElement(ctx, array, index, 'ldelem.i')));
}

function loadElement({ stack, ctx }) {
    const index = unwrap(stack.pop());
    const array = requireArray(ctx, stack.pop(), 'ldelem');
    if (typeof array === 'string') {
        if (index < 0 || index >= array.length) throwManagedException(ctx, 'System.IndexOutOfRangeException');
        stack.push(wrap(array.charCodeAt(index), 'System.Char'));
        return;
    }
    stack.push(getArrayElement(ctx, array, index, 'ldelem'));
}

function loadElementAddress({ stack, ctx, genericParams, instruction: { opr } }) {
    const index = unwrap(stack.pop());
    const wrappedArray = stack.pop();
    const array = requireArray(ctx, wrappedArray, 'ldelema');
    requireArrayIndex(ctx, array, index, 'ldelema');

    const requestedElementType = formatManagedTypeReference(opr, genericParams);
    const runtimeElementType = getArrayElementType(wrappedArray.t);
    if (runtimeElementType !== requestedElementType) {
        throwManagedException(ctx, 'System.ArrayTypeMismatchException');
    }

    const cell = {};
    Object.defineProperty(cell, 'value', {
        enumerable: true,
        get: () => array[index],
        set: value => {
            if (isReferenceArrayType(wrappedArray.t)
                && !canStoreReference(wrappedArray.t, value, genericParams)) {
                throwManagedException(ctx, 'System.ArrayTypeMismatchException');
            }
            array[index] = value;
        }
    });
    stack.push(cell);
}

function storeNative({ stack, ctx }) {
    const value = normalizeNativeIntegerElement(stack.pop());
    const index = unwrap(stack.pop());
    const array = requireArray(ctx, stack.pop(), 'stelem.i');
    requireArrayIndex(ctx, array, index, 'stelem.i');
    array[index] = value;
}

function storeElement(opts) {
    const { stack, ctx, genericParams, instruction: { oc } } = opts;
    const value = stack.pop();
    const index = unwrap(stack.pop());
    const wrappedArray = stack.pop();
    const array = requireArray(ctx, wrappedArray, oc);
    requireArrayIndex(ctx, array, index, oc);
    if ((oc === 'stelem.ref' || (oc === 'stelem' && isReferenceArrayType(wrappedArray.t)))
        && !canStoreReference(wrappedArray.t, value, genericParams)) {
        throwManagedException(ctx, 'System.ArrayTypeMismatchException');
    }
    array[index] = value;
}

export const instructionHandlers = Object.freeze({
    ldlen: loadLength, newarr: newArray, 'ldelem.i': loadNative,
    ldelema: loadElementAddress,
    ldelem: loadElement, 'ldelem.i4': loadElement, 'ldelem.i8': loadElement,
    'ldelem.i1': loadElement, 'ldelem.i2': loadElement, 'ldelem.u1': loadElement,
    'ldelem.u2': loadElement, 'ldelem.u4': loadElement, 'ldelem.r4': loadElement,
    'ldelem.r8': loadElement, 'ldelem.ref': loadElement,
    'stelem.i': storeNative, stelem: storeElement, 'stelem.i4': storeElement,
    'stelem.i8': storeElement, 'stelem.i1': storeElement, 'stelem.i2': storeElement,
    'stelem.r4': storeElement, 'stelem.r8': storeElement, 'stelem.ref': storeElement
});

function getArrayElement(ctx, array, index, opcode) {
    requireArrayIndex(ctx, array, index, opcode);
    const element = array[index];
    if (element === undefined) throw new Error(`${opcode}: array element at index ${index} is undefined (uninitialized)`);
    return element;
}
function requireArrayIndex(ctx, array, index, opcode) {
    if (!Number.isInteger(index)) throw new TypeError(`${opcode} requires an integer index.`);
    if (index < 0 || index >= array.length) throwManagedException(ctx, 'System.IndexOutOfRangeException');
}
function requireArray(ctx, wrappedArray, opcode) {
    const array = unwrap(wrappedArray);
    if (array === null) throwManagedException(ctx, 'System.NullReferenceException');
    if (!Array.isArray(array) && typeof array !== 'string') throw new TypeError(`${opcode} requires an array reference.`);
    return array;
}
function canStoreReference(arrayType, value, genericParams) {
    if (unwrap(value) === null) return true;
    if (typeof arrayType !== 'string' || !arrayType.endsWith('[]')) return false;
    if (typeof value?.t !== 'string') return false;

    const elementType = arrayType.slice(0, -2);
    const valueType = formatManagedTypeReference({
        typeName: value.t,
        genericParams: resolveManagedGenericArguments(value.$typeArgs, genericParams)
    });
    return valueType === elementType || isTypeAssignableTo(value.t, elementType);
}
function isReferenceArrayType(arrayType) {
    if (typeof arrayType !== 'string' || !arrayType.endsWith('[]')) return false;
    const elementType = arrayType.slice(0, -2);
    return isReferenceType(elementType);
}
function getArrayElementType(arrayType) {
    if (typeof arrayType !== 'string' || !arrayType.endsWith('[]')) {
        throw new TypeError('ldelema requires a typed array reference.');
    }
    return arrayType.slice(0, -2);
}
function loadNativeIntegerElement(element) {
    const rawValue = unwrap(element);
    switch (element.t) {
        case '$NativeInt32':
        case 'System.IntPtr': return wrap(rawValue | 0, '$NativeInt32');
        case '$NativeUInt32':
        case 'System.UIntPtr': return wrap(rawValue >>> 0, '$NativeUInt32');
        default: throw new TypeError('ldelem.i requires a native integer element.');
    }
}
function normalizeNativeIntegerElement(value) {
    const rawValue = unwrap(value);
    switch (value.t) {
        case '$NativeInt32': return wrap(rawValue | 0, '$NativeInt32');
        case '$NativeUInt32': return wrap(rawValue >>> 0, '$NativeUInt32');
        default: throw new TypeError('stelem.i requires a native integer value.');
    }
}
