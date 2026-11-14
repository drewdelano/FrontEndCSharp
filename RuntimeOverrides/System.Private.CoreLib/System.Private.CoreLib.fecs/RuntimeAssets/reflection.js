import {
    getManagedTypeArguments,
    getManagedTypeName,
    isReferenceType,
    resolveManagedTypeReference,
    unwrap,
    wrap
} from '../../msil-type-helpers.js';

function requireTypeMetadata(typeObject) {
    const typeName = unwrap(typeObject);
    const metadata = globalThis.DotNet?.Types?.[typeName];
    if (!metadata) {
        throw new Error(`Reflection metadata is unavailable for managed type '${typeName}'.`);
    }
    return { typeName, typeArguments: typeObject.$typeArgs ?? null, metadata };
}

function getEnumMetadata(typeObject) {
    const result = requireTypeMetadata(typeObject);
    if (typeof result.metadata.enumUnderlyingType !== 'string'
        || !Array.isArray(result.metadata.enumValues)) {
        throw new TypeError(`Managed type '${result.typeName}' is not an enum.`);
    }
    return result;
}

function enumWidth(underlyingType) {
    switch (underlyingType) {
        case 'System.SByte':
        case 'System.Byte':
            return 8;
        case 'System.Int16':
        case 'System.UInt16':
            return 16;
        case 'System.Int32':
        case 'System.UInt32':
            return 32;
        case 'System.Int64':
        case 'System.UInt64':
            return 64;
        default:
            throw new TypeError(`Unsupported enum underlying type '${underlyingType}'.`);
    }
}

function enumBits(value, underlyingType) {
    return BigInt.asUintN(enumWidth(underlyingType), BigInt(value));
}

function getSortedEnumValues(metadata) {
    return metadata.enumValues
        .map((entry, index) => ({ entry, index, bits: enumBits(entry.value, metadata.enumUnderlyingType) }))
        .sort((left, right) => left.bits < right.bits
            ? -1
            : left.bits > right.bits
                ? 1
                : left.index - right.index)
        .map(value => value.entry);
}

function toRuntimeEnumValue(value, underlyingType) {
    const bits = enumBits(value, underlyingType);
    if (underlyingType === 'System.UInt32') return Number(BigInt.asIntN(32, bits));
    if (underlyingType === 'System.UInt64') return BigInt.asIntN(64, bits);
    return value;
}

function unwrapEnumArgument(typeName, metadata, value, allowString) {
    const boxed = unwrap(value);
    const candidate = boxed !== null && typeof boxed === 'object' && 't' in boxed
        ? boxed
        : value;
    const raw = unwrap(candidate);
    if (allowString && typeof raw === 'string') return raw;
    if (candidate?.t !== typeName && candidate?.t !== metadata.enumUnderlyingType) {
        throw new TypeError(
            `Enum value for '${typeName}' must have type '${typeName}' or '${metadata.enumUnderlyingType}'.`);
    }
    return raw;
}

export function isEnum(typeObject) {
    const { metadata } = requireTypeMetadata(typeObject);
    return wrap(typeof metadata.enumUnderlyingType === 'string', 'System.Boolean');
}

export function isValueType(typeObject) {
    const { metadata } = requireTypeMetadata(typeObject);
    return wrap(metadata.isValueType === true, 'System.Boolean');
}

export function getEnumUnderlyingType(typeObject) {
    const { metadata } = getEnumMetadata(typeObject);
    return wrap(metadata.enumUnderlyingType, 'System.Type');
}

export function getEnumNames(typeObject) {
    const { metadata } = getEnumMetadata(typeObject);
    return wrap(
        getSortedEnumValues(metadata).map(entry => wrap(entry.name, 'System.String')),
        'System.String[]');
}

export function getEnumValues(typeObject) {
    const { typeName, metadata } = getEnumMetadata(typeObject);
    return wrap(
        getSortedEnumValues(metadata).map(entry => wrap(
            toRuntimeEnumValue(entry.value, metadata.enumUnderlyingType),
            typeName)),
        `${typeName}[]`);
}

export function getEnumValuesAsUnderlyingType(typeObject) {
    const { metadata } = getEnumMetadata(typeObject);
    return wrap(
        getSortedEnumValues(metadata).map(entry => wrap(
            toRuntimeEnumValue(entry.value, metadata.enumUnderlyingType),
            metadata.enumUnderlyingType)),
        `${metadata.enumUnderlyingType}[]`);
}

export function getEnumName(typeObject, value) {
    const { typeName, metadata } = getEnumMetadata(typeObject);
    const raw = unwrapEnumArgument(typeName, metadata, value, false);
    const bits = enumBits(raw, metadata.enumUnderlyingType);
    const entry = getSortedEnumValues(metadata).find(candidate =>
        enumBits(candidate.value, metadata.enumUnderlyingType) === bits);
    return entry === undefined ? null : wrap(entry.name, 'System.String');
}

export function isEnumDefined(typeObject, value) {
    const { typeName, metadata } = getEnumMetadata(typeObject);
    const raw = unwrapEnumArgument(typeName, metadata, value, true);
    const defined = typeof raw === 'string'
        ? metadata.enumValues.some(entry => entry.name === raw)
        : metadata.enumValues.some(entry =>
            enumBits(entry.value, metadata.enumUnderlyingType)
                === enumBits(raw, metadata.enumUnderlyingType));
    return wrap(defined, 'System.Boolean');
}

function wrapMember(typeName, declaringType, declaringTypeArguments, metadata) {
    return wrap({ declaringType, declaringTypeArguments, metadata }, typeName);
}

export function getMethodFromHandle(handle) {
    const handleValue = unwrap(handle);
    const token = handleValue?.methodName ?? handleValue;
    const declaringType = token?.typeName;
    const signature = token?.methodName;
    const typeMetadata = globalThis.DotNet?.Types?.[declaringType];
    const method = typeMetadata?.methods?.find(candidate =>
        candidate.methodName === `${declaringType}.${signature}`);
    if (!method) {
        throw new Error(`Reflection metadata is unavailable for managed method '${declaringType}.${signature}'.`);
    }
    return wrapMember(
        'System.Reflection.MethodInfo',
        declaringType,
        handleValue?.genericTypeArguments ?? null,
        method);
}

export function getProperties(typeObject) {
    const { typeName, typeArguments, metadata } = requireTypeMetadata(typeObject);
    const properties = (metadata.properties ?? []).map(property =>
        wrapMember('System.Reflection.PropertyInfo', typeName, typeArguments, property));
    return wrap(properties, 'System.Reflection.PropertyInfo[]');
}

export function getMethods(typeObject) {
    const { typeName, typeArguments, metadata } = requireTypeMetadata(typeObject);
    const methods = (metadata.methods ?? []).map(method =>
        wrapMember('System.Reflection.MethodInfo', typeName, typeArguments, method));
    return wrap(methods, 'System.Reflection.MethodInfo[]');
}

export function getFields(typeObject) {
    const { typeName, typeArguments, metadata } = requireTypeMetadata(typeObject);
    const fields = [];
    let currentTypeName = typeName;
    let currentMetadata = metadata;
    while (currentMetadata !== undefined) {
        for (const field of currentMetadata.fields ?? []) {
            if (field.isPublic !== true) continue;
            fields.push(wrapMember(
                'System.Reflection.FieldInfo',
                field.declaringType ?? currentTypeName,
                typeArguments,
                field));
        }
        const baseType = currentMetadata.baseType;
        currentTypeName = typeof baseType === 'string'
            ? baseType
            : baseType?.typeName;
        currentMetadata = typeof currentTypeName === 'string'
            ? globalThis.DotNet?.Types?.[currentTypeName]
            : undefined;
    }
    return wrap(fields, 'System.Reflection.FieldInfo[]');
}

export function getMemberName(member) {
    const value = unwrap(member);
    if (typeof value === 'string') {
        const separator = Math.max(value.lastIndexOf('.'), value.lastIndexOf('+'));
        return wrap(value.substring(separator + 1), 'System.String');
    }
    return wrap(value.metadata.name, 'System.String');
}

export function getPropertyType(property) {
    const value = unwrap(property);
    const reference = resolveManagedTypeReference(
        value.metadata,
        value.declaringTypeArguments);
    return wrap(
        getManagedTypeName(reference),
        'System.Type',
        getManagedTypeArguments(reference));
}

export function getFieldType(field) {
    const value = unwrap(field);
    const reference = resolveManagedTypeReference(
        value.metadata,
        value.declaringTypeArguments);
    return wrap(
        getManagedTypeName(reference),
        'System.Type',
        getManagedTypeArguments(reference));
}

export function getMethodReturnType(method) {
    const value = unwrap(method);
    const reference = resolveManagedTypeReference(
        value.metadata.returnType,
        value.declaringTypeArguments);
    return wrap(
        getManagedTypeName(reference),
        'System.Type',
        getManagedTypeArguments(reference));
}

export function getCustomAttributes(member) {
    const value = unwrap(member);
    const attributes = typeof value === 'string'
        ? (requireTypeMetadata(member).metadata.attributes ?? [])
        : (value.metadata.attributes ?? []);
    return wrap(attributes.map(attribute => wrap({
        constructorArguments: attribute.constructorArguments ?? []
    }, attribute.typeName)), 'System.Object[]');
}

function invokeDescriptor(ctx, descriptor, target, args) {
    const method = globalThis.DotNet?.Methods?.[descriptor.methodName];
    if (typeof method !== 'function') {
        throw new Error(`Reflected managed method '${descriptor.methodName}' was not emitted.`);
    }
    return descriptor.isStatic
        ? method(ctx, ...args)
        : method(ctx, target, ...args);
}

export function getPropertyValue(ctx, property, target) {
    const descriptor = unwrap(property).metadata;
    if (!descriptor.getter) {
        throw new Error(`Reflected property '${descriptor.name}' does not have a getter.`);
    }
    return invokeDescriptor(ctx, {
        methodName: descriptor.getter,
        isStatic: descriptor.getterIsStatic
    }, target, []);
}

export function setPropertyValue(ctx, property, target, value) {
    const descriptor = unwrap(property).metadata;
    if (!descriptor.setter) {
        throw new Error(`Reflected property '${descriptor.name}' does not have a setter.`);
    }
    return invokeDescriptor(ctx, {
        methodName: descriptor.setter,
        isStatic: descriptor.setterIsStatic
    }, target, [value]);
}

export function getFieldValue(field, target) {
    const descriptor = unwrap(field).metadata;
    if (descriptor.isStatic) {
        throw new Error(`Static reflected field '${descriptor.name}' is not supported.`);
    }
    const value = unwrap(target)[descriptor.storageKey ?? descriptor.name];
    return value !== null && !isReferenceType(value.t)
        ? wrap(value, 'System.Object')
        : value;
}

export function setFieldValue(field, target, value) {
    const descriptor = unwrap(field).metadata;
    if (descriptor.isStatic) {
        throw new Error(`Static reflected field '${descriptor.name}' is not supported.`);
    }
    const storedValue = value?.t === 'System.Object' && unwrap(value)?.t
        ? unwrap(value)
        : value;
    unwrap(target)[descriptor.storageKey ?? descriptor.name] = storedValue;
}

export function invokeReflectedMethod(ctx, method, target, argumentsArray) {
    const descriptor = unwrap(method).metadata;
    return invokeDescriptor(ctx, descriptor, target, unwrap(argumentsArray) ?? []);
}
