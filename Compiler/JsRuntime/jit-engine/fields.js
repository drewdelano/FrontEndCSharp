import { createTypeInitializationException, throwManagedException } from "../managed-exceptions.js";
import { defineManagedFieldAlias, resolveManagedGenericTypeArguments, unwrap, getDefaultValue, deRef, isManagedNull, throwDotNetException, wrap } from "../msil-type-helpers.js";

function loadField(opts) {
    const { stack, ctx, genericParams, instruction: { opr, oc } } = opts;
    const instance = requireInstance(ctx, stack.pop(), oc);
    const definition = requireFieldDefinition(ctx, opr, false).definition;
    const fieldGenericParams = resolveManagedGenericTypeArguments(
        opr.typeName,
        opr.genericTypeParams,
        genericParams);
    const storageKey = getStorageKey(opr);
    defineManagedFieldAlias(instance, storageKey, definition);
    let field = instance[storageKey];
    if (field === undefined) field = instance[storageKey] = getDefaultValue(definition, fieldGenericParams);
    stack.push(field);
}
function loadFieldAddress({ stack, ctx, genericParams, instruction: { opr, oc } }) {
    const instance = requireInstance(ctx, stack.pop(), oc);
    const definition = requireFieldDefinition(ctx, opr, false).definition;
    const storageKey = getStorageKey(opr);
    defineManagedFieldAlias(instance, storageKey, definition);
    if (instance[storageKey] === undefined) {
        const fieldGenericParams = resolveManagedGenericTypeArguments(
            opr.typeName,
            opr.genericTypeParams,
            genericParams);
        instance[storageKey] = getDefaultValue(definition, fieldGenericParams);
    }
    const reference = {};
    Object.defineProperty(reference, 'value', {
        enumerable: true,
        get: () => instance[storageKey],
        set: value => { instance[storageKey] = value; }
    });
    stack.push(reference);
}
function storeField({ stack, ctx, instruction: { opr, oc } }) {
    const value = stack.pop();
    const instance = requireInstance(ctx, stack.pop(), oc);
    const definition = requireFieldDefinition(ctx, opr, false).definition;
    const storageKey = getStorageKey(opr);
    defineManagedFieldAlias(instance, storageKey, definition);
    instance[storageKey] = value;
}
function loadStaticField(opts) {
    const { stack, ctx, genericParams, instruction: { opr } } = opts;
    const { fieldName, typeName, genericTypeParams } = opr;
    const combined = resolveManagedGenericTypeArguments(typeName, genericTypeParams, genericParams);
    const { typeObj, definition } = requireFieldDefinition(ctx, opr, true);
    const key = JSON.stringify(combined ?? {});
    ensureTypeInitialized(ctx, typeObj, typeName, key, combined);
    typeObj.statics ??= {};
    typeObj.statics[key] ??= {};
    let field = typeObj.statics[key][fieldName];
    if (field === undefined) {
        field = typeName === 'System.String' && fieldName === 'Empty'
            ? wrap('', 'System.String')
            : getDefaultValue(definition, combined);
        typeObj.statics[key][fieldName] = field;
    }
    stack.push(field);
}
function storeStaticField(opts) {
    const { stack, ctx, genericParams, instruction: { opr } } = opts;
    const value = stack.pop();
    const { fieldName, typeName, genericTypeParams } = opr;
    const combined = resolveManagedGenericTypeArguments(typeName, genericTypeParams, genericParams);
    const { typeObj } = requireFieldDefinition(ctx, opr, true);
    const key = JSON.stringify(combined ?? {});
    ensureTypeInitialized(ctx, typeObj, typeName, key, combined);
    typeObj.statics ??= {};
    typeObj.statics[key] ??= {};
    typeObj.statics[key][fieldName] = value;
}

export const instructionHandlers = Object.freeze({
    ldfld: loadField, ldflda: loadFieldAddress, stfld: storeField,
    ldsfld: loadStaticField, stsfld: storeStaticField
});

export function handleFieldsInstruction(opts) {
    const handler = instructionHandlers[opts.instruction.oc];
    if (handler === undefined) throw new Error(`Unhandled fields opcode ${opts.instruction.oc}`);
    return handler(opts);
}

function requireInstance(ctx, receiver, opcode) {
    if (receiver === undefined) throw new Error(`Evaluation stack underflow executing ${opcode}.`);
    if (isManagedNull(receiver)) throwManagedException(ctx, 'System.NullReferenceException');
    const instance = unwrap(deRef(receiver));
    if (instance === null || typeof instance !== 'object') throw new TypeError(`${opcode} requires an object reference.`);
    return instance;
}
function requireFieldDefinition(ctx, fieldRef, isStatic) {
    const typeObj = globalThis.DotNet?.Types?.[fieldRef.typeName];
    const definition = (isStatic ? typeObj?.staticsDefinition : typeObj?.instances)?.[getStorageKey(fieldRef)];
    if (definition === undefined) throwManagedException(ctx, 'System.MissingFieldException');
    return { typeObj, definition };
}
function getStorageKey(fieldRef) {
    return fieldRef.storageKey ?? fieldRef.fieldName;
}
function ensureTypeInitialized(ctx, typeObj, typeName, key, genericParams) {
    typeObj.staticInitialization ??= {};
    const current = typeObj.staticInitialization[key];
    if (current?.status === 'complete' || current?.status === 'initializing') return;
    if (current?.status === 'failed') {
        if (current.exception !== undefined) throwDotNetException(ctx, current.exception);
        throw current.error;
    }
    const initializer = globalThis.DotNet.Methods[`${typeName}..cctor()`];
    if (typeof initializer !== 'function') {
        typeObj.staticInitialization[key] = { status: 'complete' };
        return;
    }
    typeObj.staticInitialization[key] = { status: 'initializing' };
    try {
        initializer({ ...ctx, genericParams });
        typeObj.staticInitialization[key] = { status: 'complete' };
    } catch (error) {
        const innerException = error?.dotNetException;
        if (innerException === undefined) {
            typeObj.staticInitialization[key] = { status: 'failed', error };
            throw error;
        }
        const exception = createTypeInitializationException(typeName, innerException);
        typeObj.staticInitialization[key] = { status: 'failed', exception };
        throwDotNetException(ctx, exception);
    }
}
