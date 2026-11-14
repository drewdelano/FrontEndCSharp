export function wrap(value, typeName, genericTypeMap = null) {
    if (typeName !== null && typeof typeName === 'object') {
        const resolved = resolveManagedTypeReference(typeName, genericTypeMap);
        typeName = getManagedTypeName(resolved);
        genericTypeMap = getManagedTypeArguments(resolved);
    }
    const wrapped = { t: typeName, v: value };
    if (genericTypeMap) {
        wrapped.$typeArgs = genericTypeMap;
    }
    return wrapped;
}

// TODO: this should have an assert
export function unwrap(wrapped) {
    // Check if wrapped is a .NET wrapped value (has 't' and 'v' properties)
    if (wrapped && typeof wrapped === 'object' && 't' in wrapped && 'v' in wrapped) {
        return wrapped.v;
    }
    return wrapped;
}

export function getManagedTypeName(typeReference) {
    if (typeof typeReference === 'string') return typeReference;
    if (isManagedArrayTypeReference(typeReference)) {
        return formatArrayTypeReference(typeReference);
    }
    if (typeReference !== null
        && typeof typeReference === 'object'
        && typeof typeReference.typeName === 'string') {
        return typeReference.typeName;
    }
    throw new TypeError('A managed type reference requires a type name.');
}

export function getManagedTypeArguments(typeReference) {
    if (typeReference === null || typeof typeReference !== 'object') return null;
    return typeReference.genericParams
        ?? typeReference.genericTypeParams
        ?? typeReference.genericTypeArguments
        ?? null;
}

export function resolveManagedTypeReference(typeReference, genericParams = null) {
    if (typeof typeReference === 'string') {
        if (genericParams
            && Object.hasOwn(genericParams, typeReference)
            && genericParams[typeReference] !== typeReference) {
            return resolveManagedTypeReference(genericParams[typeReference], genericParams);
        }
        return typeReference;
    }
    if (typeReference === null || typeof typeReference !== 'object') {
        throw new TypeError('A managed type reference must be a string or descriptor.');
    }

    if (isManagedArrayTypeReference(typeReference)) {
        validateArrayTypeReference(typeReference);
        return {
            arrayElementType: resolveManagedTypeReference(
                typeReference.arrayElementType,
                genericParams),
            arrayKind: typeReference.arrayKind,
            arrayRank: typeReference.arrayRank,
            arraySizes: [...(typeReference.arraySizes ?? [])],
            arrayLowerBounds: [...(typeReference.arrayLowerBounds ?? [])]
        };
    }

    const typeName = getManagedTypeName(typeReference);
    const ownArguments = getManagedTypeArguments(typeReference);
    if (ownArguments === null
        && genericParams
        && Object.hasOwn(genericParams, typeName)
        && genericParams[typeName] !== typeReference) {
        return resolveManagedTypeReference(genericParams[typeName], genericParams);
    }
    if (ownArguments === null) return typeName;

    const resolvedArguments = {};
    for (const [name, argument] of Object.entries(ownArguments)) {
        resolvedArguments[name] = resolveManagedTypeReference(argument, genericParams);
    }
    return { typeName, genericParams: resolvedArguments };
}

export function composeManagedGenericArguments(baseArguments, ...argumentMaps) {
    let composed = baseArguments == null ? {} : { ...baseArguments };
    let hasArguments = baseArguments != null;
    for (const argumentMap of argumentMaps) {
        if (argumentMap == null) continue;
        const resolved = resolveManagedGenericArguments(argumentMap, composed);
        composed = { ...composed, ...resolved };
        hasArguments = true;
    }
    return hasArguments ? composed : null;
}

export function resolveManagedGenericArguments(argumentMap, ambientArguments = null) {
    if (argumentMap == null) return null;
    const resolved = {};
    for (const [name, argument] of Object.entries(argumentMap)) {
        resolved[name] = resolveManagedTypeReference(argument, ambientArguments);
    }
    return resolved;
}

export function resolveManagedGenericTypeArguments(
    typeName,
    argumentMap,
    ambientArguments = null) {
    if (argumentMap != null) {
        return resolveManagedGenericArguments(argumentMap, ambientArguments);
    }
    if (ambientArguments == null) return null;

    const openingBracket = typeName.indexOf('[');
    if (openingBracket < 0 || !typeName.endsWith(']')) return null;
    const selected = {};
    for (const name of typeName.slice(openingBracket + 1, -1).split(', ')) {
        if (Object.hasOwn(ambientArguments, name)) {
            selected[name] = resolveManagedTypeReference(ambientArguments[name], ambientArguments);
        }
    }
    return Object.keys(selected).length === 0 ? null : selected;
}

export function formatManagedTypeReference(typeReference, genericParams = null) {
    const resolved = resolveManagedTypeReference(typeReference, genericParams);
    if (isManagedArrayTypeReference(resolved)) {
        return formatArrayTypeReference(resolved);
    }
    const typeName = getManagedTypeName(resolved);
    const argumentsMap = getManagedTypeArguments(resolved);
    if (argumentsMap === null) return typeName;

    const openingBracket = typeName.indexOf('[');
    if (openingBracket < 0 || !typeName.endsWith(']')) return typeName;
    const placeholders = typeName
        .slice(openingBracket + 1, -1)
        .split(', ');
    const argumentsText = placeholders.map(placeholder =>
        Object.hasOwn(argumentsMap, placeholder)
            ? formatManagedTypeReference(argumentsMap[placeholder])
            : placeholder);
    return `${typeName.slice(0, openingBracket)}[${argumentsText.join(', ')}]`;
}

function isManagedArrayTypeReference(typeReference) {
    return typeReference !== null
        && typeof typeReference === 'object'
        && Object.hasOwn(typeReference, 'arrayElementType');
}

function validateArrayTypeReference(typeReference) {
    const { arrayKind, arrayRank } = typeReference;
    const sizes = typeReference.arraySizes ?? [];
    const lowerBounds = typeReference.arrayLowerBounds ?? [];
    if ((arrayKind !== 'szarray' && arrayKind !== 'array')
        || !Number.isInteger(arrayRank)
        || arrayRank < 1
        || (arrayKind === 'szarray' && arrayRank !== 1)
        || !Array.isArray(sizes)
        || !Array.isArray(lowerBounds)
        || sizes.length > arrayRank
        || lowerBounds.length > arrayRank
        || sizes.some(size => !Number.isInteger(size) || size < 0)
        || lowerBounds.some(bound => !Number.isInteger(bound))) {
        throw new TypeError('A managed array type reference has an invalid kind, rank, or shape.');
    }
    if (arrayKind === 'szarray' && (sizes.length !== 0 || lowerBounds.length !== 0)) {
        throw new TypeError('An SZARRAY type reference cannot declare an array shape.');
    }
}

function formatArrayTypeReference(typeReference) {
    validateArrayTypeReference(typeReference);
    const element = formatManagedTypeReference(typeReference.arrayElementType);
    if (typeReference.arrayKind === 'szarray') return `${element}[]`;

    const sizes = typeReference.arraySizes ?? [];
    const lowerBounds = typeReference.arrayLowerBounds ?? [];
    if (sizes.length === 0 && lowerBounds.length === 0) {
        return typeReference.arrayRank === 1
            ? `${element}[*]`
            : `${element}[${','.repeat(typeReference.arrayRank - 1)}]`;
    }

    const dimensions = [];
    for (let index = 0; index < typeReference.arrayRank; index++) {
        const hasSize = index < sizes.length;
        const hasLowerBound = index < lowerBounds.length;
        if (!hasSize && !hasLowerBound) {
            dimensions.push('');
            continue;
        }
        const lowerBound = hasLowerBound ? lowerBounds[index] : 0;
        dimensions.push(hasSize
            ? `${lowerBound}...${lowerBound + sizes[index] - 1}`
            : `${lowerBound}...`);
    }
    return `${element}[${dimensions.join(',')}]`;
}

export function deRef(wrapped) {
    if (wrapped !== null
        && typeof wrapped === 'object'
        && Object.prototype.hasOwnProperty.call(wrapped, 'value')) {
        return wrapped.value;
    }
    return wrapped;
}

export function isManagedNull(value) {
    const dereferenced = deRef(value);
    return dereferenced === null || unwrap(dereferenced) === null;
}

export function defineManagedFieldAlias(instance, storageKey, definition) {
    const name = definition?.name;
    if (typeof name !== 'string'
        || !(name.startsWith('_') || name.startsWith('<'))
        || name === storageKey) {
        return;
    }
    if (Object.prototype.hasOwnProperty.call(instance, name)) {
        if (Object.prototype.hasOwnProperty.call(instance, storageKey)) return;

        // Runtime companions construct a few managed objects using their readable
        // private field names. Adopt those values the first time managed IL touches
        // the field, then leave the readable name as a compatibility alias.
        const descriptor = Object.getOwnPropertyDescriptor(instance, name);
        if (descriptor?.configurable !== true) return;
        const value = instance[name];
        delete instance[name];
        instance[storageKey] = value;
    }
    Object.defineProperty(instance, name, {
        configurable: false,
        enumerable: false,
        get: () => instance[storageKey],
        set: value => { instance[storageKey] = value; }
    });
}

export function getManagedFieldName(storageKey) {
    for (const type of Object.values(globalThis.DotNet?.Types ?? {})) {
        const name = type?.instances?.[storageKey]?.name;
        if (typeof name === 'string') return name;
    }
    return storageKey;
}

export function getManagedFieldValue(instance, name) {
    if (Object.prototype.hasOwnProperty.call(instance, name)) return instance[name];
    const storageKey = Object.keys(instance).find(key => getManagedFieldName(key) === name);
    return storageKey === undefined ? undefined : instance[storageKey];
}

export function areRuntimeTypeObjectsEqual(left, right) {
    if (left === right) return true;
    if (left === null || right === null) return false;

    // The CLR interns RuntimeType instances. FrontEndCSharp reconstructs the
    // System.Type wrapper at each ldtoken/GetTypeFromHandle boundary, so compare
    // those wrappers by the CLR identity they represent. Other Type subclasses
    // retain the operator's reference-identity behavior.
    if (left?.t !== 'System.Type' || right?.t !== 'System.Type') return false;
    if (typeof left.v !== 'string' || typeof right.v !== 'string' || left.v !== right.v) return false;

    return haveSameGenericTypeArguments(left.$typeArgs, right.$typeArgs);
}

function haveSameGenericTypeArguments(left, right) {
    if (left === right) return true;
    if (left === null || left === undefined || right === null || right === undefined) return false;

    const leftNames = Object.keys(left);
    const rightNames = Object.keys(right);
    if (leftNames.length !== rightNames.length) return false;

    return leftNames.every(name => Object.hasOwn(right, name)
        && haveSameManagedTypeReference(left[name], right[name]));
}

function haveSameManagedTypeReference(left, right) {
    if (left === right) return true;
    if (left === null || right === null
        || typeof left !== 'object' || typeof right !== 'object') return false;
    if (getManagedTypeName(left) !== getManagedTypeName(right)) return false;
    return haveSameGenericTypeArguments(
        getManagedTypeArguments(left),
        getManagedTypeArguments(right));
}

export function isTypeAssignableTo(sourceType, targetType) {
    sourceType = getManagedTypeName(sourceType);
    targetType = getManagedTypeName(targetType);
    if (sourceType === targetType) {
        return true;
    }

    if (sourceType.endsWith('[]')) {
        if (targetType === 'System.Object' || targetType === 'System.Array') {
            return true;
        }
        if (!targetType.endsWith('[]')) {
            return false;
        }

        const sourceElementType = sourceType.slice(0, -2);
        const targetElementType = targetType.slice(0, -2);
        return isReferenceType(sourceElementType)
            && isReferenceType(targetElementType)
            && isTypeAssignableTo(sourceElementType, targetElementType);
    }

    const visitedTypes = new Set();
    const pendingTypes = [sourceType];
    while (pendingTypes.length > 0) {
        const currentType = pendingTypes.pop();
        if (currentType === targetType) {
            return true;
        }
        if (visitedTypes.has(currentType)) {
            continue;
        }
        visitedTypes.add(currentType);

        const metadata = globalThis.DotNet.Types[currentType];
        if (!metadata) {
            continue;
        }
        if (metadata.baseType !== null && metadata.baseType !== undefined) {
            pendingTypes.push(getManagedTypeName(metadata.baseType));
        }
        for (const interfaceType of metadata.interfaces ?? []) {
            pendingTypes.push(getManagedTypeName(interfaceType));
        }
    }

    return false;
}

export function isReferenceType(typeName) {
    typeName = getManagedTypeName(typeName);
    if (typeName === 'System.String' || typeName.endsWith('[]')) {
        return true;
    }
    return findTypeMetadata(typeName)?.isClass === true;
}

export function getDefaultValue(typeReference, genericParams, isFunctionPointer = false) {
    if (isFunctionPointer) {
        return null;
    }

    const resolvedReference = resolveManagedTypeReference(typeReference, genericParams);
    const typeName = getManagedTypeName(resolvedReference);
    const referenceArguments = getManagedTypeArguments(resolvedReference);
    const effectiveGenericParams = referenceArguments
        ?? (typeof typeReference === 'string' && typeReference === typeName
            ? genericParams
            : null);

    // Value types get their zero value
    switch (typeName) {
        case 'System.Boolean':
            return wrap(false, typeName);
        case 'System.SByte':
        case 'System.Byte':
        case 'System.Int16':
        case 'System.UInt16':
        case 'System.Int32':
        case 'System.UInt32':
            return wrap(0, typeName);
        case 'System.Int64':
        case 'System.UInt64':
            return wrap(0n, typeName);
        case 'System.IntPtr':
            return wrap(0, '$NativeInt32');
        case 'System.UIntPtr':
            return wrap(0, '$NativeUInt32');
        case 'System.Single':
        case 'System.Double':
            return wrap(0.0, typeName);
        case 'System.Char':
            return wrap('\0', typeName);
        case 'System.String':
            return null;
    }


    // Handle structural arrays before the legacy string representation so their
    // recursively constructed element reference is not flattened and reparsed.
    if (isManagedArrayTypeReference(resolvedReference)) {
        getDefaultValue(resolvedReference.arrayElementType);
        return null;
    }

    // handle legacy array names
    if (typeName.endsWith("[]")) {
        // just call it, if it exists it won't throw
        getDefaultValue(typeName.substring(0, typeName.length - 2), effectiveGenericParams);
        return null; // it should return null here!
    }

    // handle defined types
    const type = globalThis.DotNet.Types[typeName];
    if (!type) {
        throw new Error(`can't find type '${typeName}'`);
    }

    if (type.isClass) {
        // classes are references that default to null
        return null;
    } else {
        // structs are pre-initialized
        const ret = {};
        const fields = Object.keys(type.instances);
        for (let i = 0; i < fields.length; i++) {
            try {
                defineManagedFieldAlias(ret, fields[i], type.instances[fields[i]]);
                ret[fields[i]] = getDefaultValue(type.instances[fields[i]], effectiveGenericParams);
            } catch (error) {
                if (error instanceof Error) {
                    throw new Error(
                        `${error.message} while initializing '${typeName}.${fields[i]}'`,
                        { cause: error });
                }
                throw error;
            }
        }
        return wrap(ret, typeName, effectiveGenericParams);
    }
}

function findTypeMetadata(typeName) {
    const types = globalThis.DotNet?.Types ?? {};
    if (types[typeName]) return types[typeName];
    const openingBracket = typeName.indexOf('[');
    if (openingBracket < 0) return undefined;
    const definitionPrefix = `${typeName.slice(0, openingBracket)}[`;
    const definition = Object.keys(types).find(name => name.startsWith(definitionPrefix));
    return definition === undefined ? undefined : types[definition];
}

export class DotNetError extends Error {
    constructor(dotNetException) {
        const exception = requireWrappedDotNetException(dotNetException);
        const message = getDotNetExceptionMessage(dotNetException.t, exception._message);

        super(message, { cause: dotNetException });
        this.name = dotNetException.t;
        this.dotNetException = dotNetException;
    }
}

export function throwDotNetException(ctx, wrappedException) {
    const error = new DotNetError(wrappedException);
    error.dotNetException.v._stackTraceString = wrap(ctx.stackTrace, "System.String");
    throw error;
}

function requireWrappedDotNetException(dotNetException) {
    if (dotNetException === null
        || typeof dotNetException !== 'object'
        || typeof dotNetException.t !== 'string'
        || dotNetException.v === null
        || typeof dotNetException.v !== 'object') {
        throw new TypeError('A wrapped .NET exception is required.');
    }

    return dotNetException.v;
}

function getDotNetExceptionMessage(typeName, wrappedMessage) {
    if (wrappedMessage === null) {
        return `.NET exception of type '${typeName}'.`;
    }
    if (typeof wrappedMessage !== 'object' || wrappedMessage.t !== 'System.String') {
        throw new TypeError('A .NET exception message must be null or a wrapped System.String.');
    }

    return unwrap(wrappedMessage);
}
