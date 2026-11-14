import { initializeObject } from "../msil.js";
import { throwManagedException } from "../managed-exceptions.js";
import {
    composeManagedGenericArguments,
    deRef,
    getManagedTypeArguments,
    getManagedTypeName,
    isManagedNull,
    isTypeAssignableTo,
    resolveManagedGenericArguments,
    resolveManagedGenericTypeArguments,
    resolveManagedTypeReference,
    unwrap,
    wrap
} from "../msil-type-helpers.js";

const integralStorageTypes = new Set([
    'System.SByte', 'System.Byte', 'System.Int16', 'System.UInt16',
    'System.Int32', 'System.UInt32', 'System.Int64', 'System.UInt64'
]);

function call({ ctx, genericParams, instruction, stack }) { invokeMethod(ctx, instruction.opr, stack, false, genericParams); }
function callVirtual(opts) {
    const { ctx, genericParams, instruction, stack } = opts;
    const constrainedType = opts.constrainedType;
    delete opts.constrainedType;
    invokeMethod(ctx, instruction.opr, stack, true, genericParams, constrainedType);
}
function newObject({ ctx, genericParams, instruction: { opr }, stack }) {
    const { typeName, genericTypeParams } = opr;
    const constructedTypeParams = resolveManagedGenericTypeArguments(
        typeName,
        genericTypeParams,
        genericParams);
    if (opr.isDelegate) {
        const constructorArguments = stack.splice(stack.length - opr.paramTypes.length, opr.paramTypes.length);
        const functionPointer = constructorArguments[1];
        if (functionPointer?.kind !== 'function-pointer') throw new Error('A managed delegate constructor requires a compiler-resolved function pointer.');
        stack.push(wrap({ invocations: [Object.freeze({ target: constructorArguments[0], functionPointer })] }, typeName, constructedTypeParams));
        return;
    }
    const instance = initializeObject(typeName, constructedTypeParams);
    const parameterLength = opr.paramTypes.length;
    invokeMethod(ctx, opr, [instance, ...stack.splice(stack.length - parameterLength, parameterLength)], false, genericParams);
    stack.push(instance);
}
function loadFunction({ genericParams, instruction, stack }) { stack.push(createFunctionPointer(instruction.opr, null, false, genericParams)); }
function loadVirtualFunction({ ctx, genericParams, instruction, stack }) {
    const target = stack.pop();
    if (typeof target === 'undefined') throw new Error('Evaluation stack underflow loading a virtual function pointer.');
    if (isManagedNull(target)) throwManagedException(ctx, 'System.NullReferenceException');
    stack.push(createFunctionPointer(instruction.opr, target, true, genericParams));
}
function callIndirect({ ctx, genericParams, stack }) {
    const functionPointer = stack.pop();
    if (functionPointer?.kind !== 'function-pointer') throw new Error('calli requires a compiler-resolved function pointer.');
    if (functionPointer.target !== null) {
        stack.splice(stack.length - functionPointer.signature.parameterTypes.length, 0, functionPointer.target);
    }
    invokeMethod(
        ctx,
        functionPointer.methodRef,
        stack,
        functionPointer.isVirtual,
        functionPointer.genericParams ?? genericParams);
}

export const instructionHandlers = Object.freeze({
    call, callvirt: callVirtual, newobj: newObject, ldftn: loadFunction,
    ldvirtftn: loadVirtualFunction, calli: callIndirect
});

function createFunctionPointer(methodRef, target, isVirtual, genericParams) {
    return Object.freeze({ kind: 'function-pointer', methodRef,
        signature: Object.freeze({ parameterTypes: Object.freeze([...methodRef.paramTypes]), returnType: methodRef.returnType }),
        genericParams, target, isVirtual });
}

export function invokeManagedDelegate(ctx, delegate, argumentsList, ambientGenericParams = ctx.genericParams) {
    const callback = unwrap(delegate);
    if (typeof callback === 'function') return callback(...argumentsList);

    const invocationList = delegate?.v?.invocations;
    if (!Array.isArray(invocationList) || invocationList.length === 0) {
        throw new Error('A managed delegate must contain at least one invocation.');
    }
    let result;
    for (const invocation of invocationList) {
        const invocationStack = [...argumentsList];
        if (invocation.functionPointer.methodRef.hasThis) invocationStack.unshift(invocation.target);
        invokeMethod(
            ctx,
            invocation.functionPointer.methodRef,
            invocationStack,
            invocation.functionPointer.isVirtual,
            invocation.functionPointer.genericParams ?? ambientGenericParams);
        result = invocationStack.length === 0 ? undefined : invocationStack.pop();
    }
    return result;
}

export function invokeManagedVirtualMethod(
    ctx,
    receiver,
    declaringType,
    methodName,
    parameterTypes = [],
    argumentsList = []) {
    const stack = [receiver, ...argumentsList];
    invokeMethod(ctx, {
        typeName: declaringType,
        methodName,
        paramTypes: parameterTypes,
        hasThis: true
    }, stack, true);
    return stack.length === 0 ? undefined : stack.pop();
}

function invokeMethod(
    ctx,
    methodRef,
    stack,
    isVirtual = false,
    ambientGenericParams = ctx.genericParams,
    constrainedType = undefined) {
    const { methodName, paramTypes, typeName, genericTypeParams, genericMethodParams } = methodRef;
    const argCount = paramTypes.length + (methodRef.hasThis ? 1 : 0);
    if (argCount > stack.length) throw new Error(`Evaluation stack underflow calling ${typeName}.${methodName}: required ${argCount} argument(s), but only ${stack.length} value(s) were available.`);
    const args = [ctx, ...stack.splice(stack.length - argCount, argCount)];
    if ((isVirtual || methodRef.isDelegateInvocation) && isManagedNull(args[1])) throwManagedException(ctx, 'System.NullReferenceException');
    const constrainedReceiverType = constrainedType === undefined
        ? null
        : validateConstrainedReceiver(args[1], constrainedType, ambientGenericParams);
    if (methodRef.hasThis) {
        args[1] = deRef(args[1]);
        const constrainedMetadata = constrainedReceiverType == null
            ? null
            : globalThis.DotNet.Types[constrainedReceiverType];
        if (typeof constrainedMetadata?.enumUnderlyingType === 'string'
            && args[1]?.t !== constrainedReceiverType) {
            // Enum locals are carried in their integral stack representation.
            // A constrained call supplies the concrete enum identity needed for
            // virtual dispatch and metadata-backed operations such as formatting.
            args[1] = wrap(unwrap(args[1]), constrainedReceiverType);
        }
        if ((isVirtual || typeName !== 'System.Object')
            && args[1]?.t === 'System.Object'
            && args[1].v !== null
            && typeof args[1].v === 'object'
            && typeof args[1].v.t === 'string'
            && Object.prototype.hasOwnProperty.call(args[1].v, 'v')) {
            // Interface and virtual dispatch on a boxed value type targets the
            // boxed value's runtime type, not System.Object.
            args[1] = args[1].v;
        }
    }

    if (isVirtual) {
        const arrayInterface = tryInvokeArrayInterface(
            ctx,
            methodRef,
            args,
            ambientGenericParams);
        if (arrayInterface.handled) {
            pushResult(stack, arrayInterface.result);
            return;
        }
    }

    if (methodRef.isDelegateInvocation) {
        const result = invokeManagedDelegate(ctx, args[1], args.slice(2), ambientGenericParams);
        if (result !== undefined) stack.push(result);
        return;
    }

    let method;
    let virtualTargetTypeParams = null;
    if (!isVirtual) method = globalThis.DotNet.Methods[`${typeName}.${methodName}`];
    else {
        let actualTypeName = args[1].t;
        let actualTypeParams = args[1].$typeArgs ?? null;
        while (typeof actualTypeName === 'string') {
            // Prefer an explicit implementation of the declared interface
            // contract before falling back to a same-named public method.
            method = globalThis.DotNet.Methods[`${actualTypeName}.${typeName}.${methodName}`]
                ?? globalThis.DotNet.Methods[`${actualTypeName}.${methodName}`];
            if (typeof method !== 'undefined') {
                virtualTargetTypeParams = actualTypeParams;
                break;
            }
            const type = globalThis.DotNet.Types[actualTypeName];
            if (typeof type === 'undefined') break;
            const resolvedBaseType = type.baseType == null
                ? null
                : resolveManagedTypeReference(type.baseType, actualTypeParams);
            actualTypeName = type.baseType == null
                ? null
                : getManagedTypeName(resolvedBaseType);
            actualTypeParams = resolvedBaseType == null
                ? null
                : getManagedTypeArguments(resolvedBaseType);
        }
        // An exact interface contract can be supplied by a runtime override
        // even when the receiver's managed implementation is intentionally not
        // retained (for example, a framework comparer implemented internally).
        method ??= globalThis.DotNet.Methods[`${typeName}.${methodName}`];
    }
    if (typeof method !== 'function') {
        const receiverDetail = isVirtual && typeof args[1]?.t === 'string'
            ? ` for runtime receiver '${args[1].t}'`
            : '';
        throwManagedException(
            ctx,
            'System.MissingMethodException',
            `Managed method '${typeName}.${methodName}' was not emitted${receiverDetail}.`);
    }
    const previousGenericParams = ctx.genericParams;
    let result;
    try {
        const ambient = ambientGenericParams ?? previousGenericParams;
        const targetTypeParams = isVirtual && virtualTargetTypeParams != null
            ? virtualTargetTypeParams
            : resolveManagedGenericTypeArguments(
                typeName,
                genericTypeParams,
                ambient);
        if (targetTypeParams || genericMethodParams) {
            const methodResolutionContext = composeManagedGenericArguments(ambient, targetTypeParams);
            const targetMethodParams = resolveManagedGenericArguments(
                genericMethodParams,
                methodResolutionContext);
            ctx.genericParams = composeManagedGenericArguments(targetTypeParams, targetMethodParams);
        }
        result = method.apply(null, args);
    } finally { ctx.genericParams = previousGenericParams; }
    pushResult(stack, result);
}

function validateConstrainedReceiver(receiver, constrainedType, ambientGenericParams) {
    const value = deRef(receiver);
    if (value === null || unwrap(value) === null) return;
    if (value === undefined || typeof value.t !== 'string') {
        throw new Error("A 'constrained.' call requires a managed receiver or reference cell.");
    }
    const resolvedType = resolveManagedTypeReference(constrainedType, ambientGenericParams);
    const expectedType = getManagedTypeName(resolvedType);
    const expectedMetadata = globalThis.DotNet.Types[expectedType];
    const integralEnumStorage = typeof expectedMetadata?.enumUnderlyingType === 'string'
        && integralStorageTypes.has(value.t);
    if (value.t !== expectedType
        && !integralEnumStorage
        && !isTypeAssignableTo(value.t, expectedType)) {
        throw new Error(
            `The constrained receiver type '${value.t}' is not assignable to '${expectedType}'.`);
    }
    return expectedType;
}

function tryInvokeArrayInterface(ctx, methodRef, args, ambientGenericParams) {
    const receiver = args[1];
    const receiverValue = unwrap(receiver);
    if (Array.isArray(receiverValue)
        && receiver.t.endsWith('[]')
        && methodRef.typeName === 'System.Collections.Generic.IEnumerable`1[T]'
        && methodRef.methodName === 'GetEnumerator()') {
        const genericTypeParams = resolveManagedGenericTypeArguments(
            methodRef.typeName,
            methodRef.genericTypeParams,
            ambientGenericParams);
        if (genericTypeParams?.T === undefined) {
            return { handled: false };
        }
        const elementType = receiver.t.substring(0, receiver.t.length - 2);
        const requestedElementType = getManagedTypeName(genericTypeParams.T);
        if (!isTypeAssignableTo(elementType, requestedElementType)) {
            return { handled: false };
        }
        return {
            handled: true,
            result: wrap(
                { $arrayEnumerator: receiver, index: -1 },
                'System.SZGenericArrayEnumerator`1',
                { T: requestedElementType })
        };
    }
    if (receiver.t !== 'System.SZGenericArrayEnumerator`1'
        || receiverValue?.$arrayEnumerator === undefined) {
        return { handled: false };
    }

    const array = unwrap(receiverValue.$arrayEnumerator);
    switch (methodRef.methodName) {
        case 'MoveNext()':
            if (methodRef.typeName !== 'System.Collections.IEnumerator') {
                return { handled: false };
            }
            receiverValue.index++;
            return { handled: true, result: wrap(receiverValue.index < array.length, 'System.Boolean') };
        case 'get_Current()': {
            const genericTypeParams = resolveManagedGenericTypeArguments(
                methodRef.typeName,
                methodRef.genericTypeParams,
                ambientGenericParams);
            if (!isCompatibleArrayEnumeratorCurrent(
                receiver,
                methodRef,
                genericTypeParams)) {
                return { handled: false };
            }
            if (receiverValue.index < 0 || receiverValue.index >= array.length) {
                throwManagedException(ctx, 'System.InvalidOperationException');
            }
            return { handled: true, result: array[receiverValue.index] };
        }
        case 'Dispose()':
            if (methodRef.typeName !== 'System.IDisposable') {
                return { handled: false };
            }
            return { handled: true, result: undefined };
        case 'Reset()':
            if (methodRef.typeName !== 'System.Collections.IEnumerator') {
                return { handled: false };
            }
            receiverValue.index = -1;
            return { handled: true, result: undefined };
        default:
            return { handled: false };
    }
}

function isCompatibleArrayEnumeratorCurrent(receiver, methodRef, genericTypeParams) {
    if (methodRef.typeName === 'System.Collections.IEnumerator') {
        return true;
    }
    if (methodRef.typeName !== 'System.Collections.Generic.IEnumerator`1[T]'
        || genericTypeParams?.T === undefined
        || receiver.$typeArgs?.T === undefined) {
        return false;
    }
    return isTypeAssignableTo(
        getManagedTypeName(receiver.$typeArgs.T),
        getManagedTypeName(genericTypeParams.T));
}

function pushResult(stack, result) {
    if (result === null) stack.push(null);
    else if (result === undefined) { }
    else if (result.t) stack.push(result);
    else throw "not supported";
}

export function invokeDelegate(ctx, delegate, args = []) {
    const stack = [delegate, ...args];
    invokeMethod(ctx, { methodName: 'Invoke()', paramTypes: args.map(argument => argument?.t ?? 'System.Object'), hasThis: true, isDelegateInvocation: true }, stack);
    return stack.length === 0 ? undefined : stack.pop();
}
