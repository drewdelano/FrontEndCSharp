import { throwManagedException } from "../managed-exceptions.js";
import {
    getManagedTypeArguments,
    getManagedTypeName,
    isManagedNull,
    isReferenceType,
    isTypeAssignableTo,
    resolveManagedTypeReference,
    wrap,
    unwrap
} from "../msil-type-helpers.js";

function loadToken({ stack, genericParams, instruction: { opr } }) {
                // Load metadata token - push RuntimeFieldHandle, RuntimeTypeHandle, or RuntimeMethodHandle
                const tokenInfo = opr;
                if (tokenInfo.tokenType === 'Field') {
                    // For field tokens, create a RuntimeFieldHandle with the field data
                    const handle = {
                        fieldName: tokenInfo.tokenValue,
                        fieldData: tokenInfo.fieldData
                    };
                    stack.push(wrap(handle, 'System.RuntimeFieldHandle'));
                } else if (tokenInfo.tokenType === 'Type') {
                    const reference = tokenInfo.isGenericParameter
                        ? tokenInfo.tokenValue
                        : tokenInfo.tokenValue !== null
                            && typeof tokenInfo.tokenValue === 'object'
                            ? tokenInfo.tokenValue
                            : {
                            typeName: tokenInfo.tokenValue,
                            genericParams: tokenInfo.genericTypeArguments
                        };
                    const resolved = resolveManagedTypeReference(reference, genericParams);
                    const handle = {
                        typeName: getManagedTypeName(resolved),
                        genericTypeArguments: getManagedTypeArguments(resolved)
                    };
                    stack.push(wrap(handle, 'System.RuntimeTypeHandle'));
                } else if (tokenInfo.tokenType === 'Method') {
                    const declaringType = resolveManagedTypeReference({
                        typeName: tokenInfo.tokenValue.typeName,
                        genericParams: tokenInfo.genericTypeArguments
                    }, genericParams);
                    const handle = {
                        methodName: {
                            ...tokenInfo.tokenValue,
                            typeName: getManagedTypeName(declaringType)
                        },
                        genericTypeArguments: getManagedTypeArguments(declaringType)
                    };
                    stack.push(wrap(handle, 'System.RuntimeMethodHandle'));
                }
}
function isInstance({ stack, genericParams, instruction: { opr, oc } }) {
                const obj = stack.pop();
                if (obj === null) {
                    stack.push(null);
                    return;
                }
                requireWrappedObject(obj, oc);

                const targetType = resolveTargetType(opr, genericParams);
                stack.push(isTypeAssignableTo(getRuntimeType(obj), targetType) ? obj : null);
}
function castClass({ stack, genericParams, ctx, instruction: { opr, oc } }) {
                const obj = stack.pop();
                if (obj === null) {
                    stack.push(null);
                    return;
                }
                requireWrappedObject(obj, oc);

                const targetType = resolveTargetType(opr, genericParams);
                const runtimeType = getRuntimeType(obj);
                if (!isTypeAssignableTo(runtimeType, targetType)) {
                    throwManagedException(
                        ctx,
                        "System.InvalidCastException",
                        `Unable to cast object of type '${runtimeType}' to type '${targetType}'.`
                    );
                }
                stack.push(obj);
}
function box({ stack, genericParams, instruction: { opr } }) {
                const value = stack.pop();
                const sourceType = resolveManagedTypeReference(opr, genericParams);
                // A box instruction over a generic parameter is a no-op when the
                // runtime type argument is already a reference type.
                if (isReferenceType(sourceType)) {
                    stack.push(value);
                    return;
                }
                // Box value type - wrap with Object type but preserve the box operand type (not value.t)
                // The operand tells us what type is being boxed, which is the correct type for unboxing
                const reinterpretedType = wrap(unwrap(value), sourceType);
                const boxed = wrap(reinterpretedType, "System.Object");
                stack.push(boxed);
}
function unbox(opts) {
                const { stack, genericParams, ctx, instruction: { opr, oc } } = opts;
                const objWrapped = stack.pop();
                const targetType = resolveTargetType(opr, genericParams);

                if (typeof objWrapped === 'undefined') {
                    throw new Error(`Evaluation stack underflow executing ${oc}.`);
                }
                if (isManagedNull(objWrapped)) {
                    if (oc === 'unbox.any' && isReferenceType(targetType)) {
                        stack.push(null);
                        return;
                    }
                    throwManagedException(ctx, "System.NullReferenceException");
                }

                const obj = unwrap(objWrapped);

                if (oc === 'unbox.any' && isReferenceType(targetType)) {
                    const value = objWrapped.t === 'System.Object'
                        && obj !== null
                        && typeof obj === 'object'
                        && typeof obj.t === 'string'
                        ? obj
                        : objWrapped;
                    const runtimeType = getRuntimeType(value);
                    if (!isTypeAssignableTo(runtimeType, targetType)) {
                        throwManagedException(
                            ctx,
                            "System.InvalidCastException",
                            `Unable to cast object of type '${runtimeType}' to type '${targetType}'.`
                        );
                    }
                    stack.push(value);
                    return;
                }

                if (objWrapped.t !== "System.Object") {
                    throwManagedException(
                        ctx,
                        "System.InvalidCastException",
                        `Unable to cast object of type '${objWrapped.t}' to type '${targetType}'.`
                    );
                }

                // Check if the cast is valid
                const actualType = getManagedTypeName(obj.t);

                if (targetType !== actualType) {
                    throwManagedException(
                        ctx,
                        "System.InvalidCastException",
                        `Unable to cast object of type '${actualType}' to type '${targetType}'.`
                    );
                }

                // Unbox value type - extract value and rewrap with correct type
                stack.push(obj);
}
function initializeValueType({ stack }) {
                // initobj: Initialize a value type to its default value
                // The stack should have an address from ldloca (which we convert to ldloc)
                // Since we can't track addresses in JS, we just pop the value and it's already initialized
                // The local variable was already initialized to its default when the method started
                stack.pop(); // Pop the address/value that was loaded
                // No-op: the local is already at its default value
}
function sizeOf({ stack, genericParams, instruction: { opr } }) {
    stack.push(wrap(getTypeSize(resolveTargetType(opr, genericParams)), 'System.UInt32'));
}

export const instructionHandlers = Object.freeze({
    ldtoken: loadToken, isinst: isInstance, castclass: castClass, box,
    unbox, 'unbox.any': unbox, initobj: initializeValueType, sizeof: sizeOf
});

function requireWrappedObject(obj, opcode) {
    if (typeof obj !== 'object'
        || typeof obj.t !== 'string'
        || !Object.prototype.hasOwnProperty.call(obj, 'v')) {
        throw new TypeError(`${opcode} requires a wrapped object reference.`);
    }
}

function resolveTargetType(targetType, genericParams) {
    return getManagedTypeName(resolveManagedTypeReference(targetType, genericParams));
}

function getRuntimeType(obj) {
    if (obj.t === 'System.Object'
        && obj.v !== null
        && typeof obj.v === 'object'
        && typeof obj.v.t === 'string') {
        return obj.v.t;
    }
    return obj.t;
}

function getTypeSize(typeName) {
    switch (typeName) {
        case 'System.Boolean':
        case 'System.Byte':
        case 'System.SByte':
            return 1;
        case 'System.Char':
        case 'System.Int16':
        case 'System.UInt16':
            return 2;
        case 'System.Int32':
        case 'System.UInt32':
        case 'System.Single':
        case 'System.IntPtr':
        case 'System.UIntPtr':
            return 4;
        case 'System.Int64':
        case 'System.UInt64':
        case 'System.Double':
            return 8;
        case 'System.Decimal':
            return 16;
        default:
            throw new Error(`sizeof does not support type '${typeName}' without managed layout metadata.`);
    }
}
