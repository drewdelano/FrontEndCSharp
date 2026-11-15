import { throwManagedException } from '../managed-exceptions.js';
import { tryGetListItems } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';
import { parseManagedTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { initializeObject } from '../msil.js';
import { setPropertyValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
import { getDefaultValue, getManagedTypeArguments, getManagedTypeName, isManagedNull, resolveManagedTypeReference, unwrap, wrap } from '../msil-type-helpers.js';

const exactNumber = Symbol('System.Text.Json exact number');
const sourceIndexFailure = Symbol('System.Text.Json source index failure');

function fail(ctx, typeName, message) {
    throwManagedException(ctx, typeName, message);
}

function isManagedObjectContract(metadata) {
    return metadata?.isClass === true
        || metadata?.isValueType === true && Array.isArray(metadata.constructors);
}

function getBaseTypeName(typeName) {
    const reference = globalThis.DotNet.Types[typeName]?.baseType;
    return reference == null ? null : getManagedTypeName(reference);
}

function getTypeHierarchy(typeName) {
    const hierarchy = [];
    for (let current = typeName; typeof current === 'string'; current = getBaseTypeName(current)) {
        const metadata = globalThis.DotNet.Types[current];
        if (!metadata) break;
        hierarchy.push(metadata);
    }
    return hierarchy;
}

function getGenericArguments(typeReference) {
    const typeName = getManagedTypeName(typeReference);
    const argumentsMap = getManagedTypeArguments(typeReference);
    if (argumentsMap === null) return [];
    const openingBracket = typeName.indexOf('[');
    if (openingBracket < 0 || !typeName.endsWith(']')) return [];
    return typeName.slice(openingBracket + 1, -1).split(', ')
        .map(name => resolveManagedTypeReference(argumentsMap[name], argumentsMap));
}

function findCollectionContract(typeReference) {
    const resolved = resolveManagedTypeReference(typeReference);
    const typeName = getManagedTypeName(resolved);
    const argumentsToUse = getGenericArguments(resolved);
    if (typeName === 'System.Collections.Generic.Dictionary`2[TKey, TValue]'
        || typeName === 'System.Collections.Generic.IDictionary`2[TKey, TValue]'
        || typeName === 'System.Collections.Generic.IReadOnlyDictionary`2[TKey, TValue]') {
        if (argumentsToUse.length !== 2) return null;
        const implementationType = typeName === 'System.Collections.Generic.Dictionary`2[TKey, TValue]'
            ? resolved
            : {
                typeName: 'System.Collections.Generic.Dictionary`2[TKey, TValue]',
                genericParams: { TKey: argumentsToUse[0], TValue: argumentsToUse[1] }
            };
        return {
            kind: 'dictionary',
            keyType: argumentsToUse[0],
            valueType: argumentsToUse[1],
            implementationType
        };
    }
    if (typeName === 'System.Collections.Generic.List`1[T]'
        || typeName === 'System.Collections.Generic.IList`1[T]'
        || typeName === 'System.Collections.Generic.IReadOnlyList`1[T]') {
        if (argumentsToUse.length !== 1) return null;
        const implementationType = typeName === 'System.Collections.Generic.List`1[T]'
            ? resolved
            : {
                typeName: 'System.Collections.Generic.List`1[T]',
                genericParams: { T: argumentsToUse[0] }
            };
        return { kind: 'list', elementType: argumentsToUse[0], implementationType };
    }
    return null;
}

function findManagedVirtualMethod(instance, methodName) {
    let typeName = instance?.t;
    while (typeof typeName === 'string') {
        for (const candidate of [
            methodName,
            `System.Collections.IList.${methodName}`,
            `System.Collections.IDictionary.${methodName}`,
            `System.Collections.IDictionaryEnumerator.${methodName}`]) {
            const method = globalThis.DotNet.Methods[`${typeName}.${candidate}`];
            if (typeof method === 'function') return method;
        }
        typeName = getBaseTypeName(typeName);
    }
    return null;
}

function invokeManagedVirtual(ctx, instance, methodName, ...args) {
    const method = findManagedVirtualMethod(instance, methodName);
    if (method === null) {
        fail(ctx, 'System.NotSupportedException', `Managed collection method '${methodName}' is unavailable.`);
    }
    const previousGenericParams = ctx.genericParams;
    try {
        ctx.genericParams = instance?.$typeArgs ?? null;
        return method(ctx, instance, ...args);
    } finally {
        ctx.genericParams = previousGenericParams;
    }
}

function constructManagedCollection(ctx, typeReference) {
    const resolved = resolveManagedTypeReference(typeReference);
    const typeName = getManagedTypeName(resolved);
    const typeArguments = getManagedTypeArguments(resolved);
    const instance = initializeObject(typeName, typeArguments);
    const constructor = globalThis.DotNet.Methods[`${typeName}..ctor()`];
    if (typeof constructor !== 'function') {
        fail(ctx, 'System.NotSupportedException', `Collection type '${typeName}' does not have an available parameterless constructor.`);
    }
    const previousGenericParams = ctx.genericParams;
    try {
        ctx.genericParams = typeArguments;
        constructor(ctx, instance);
    } finally {
        ctx.genericParams = previousGenericParams;
    }
    return instance;
}

function nullableInt64(value) {
    return wrap({
        hasValue: wrap(true, 'System.Boolean'),
        value: wrap(BigInt(value), 'System.Int64')
    }, 'System.Nullable`1', { T: 'System.Int64' });
}

function sourcePosition(source, index) {
    let lineNumber = 0;
    let lineStart = 0;
    for (let current = 0; current < index; current++) {
        if (source[current] === '\n') {
            lineNumber++;
            lineStart = current + 1;
        }
    }
    return {
        lineNumber,
        bytePositionInLine: new TextEncoder().encode(source.slice(lineStart, index)).length
    };
}

function exceptionFields(source, path, index) {
    const position = sourcePosition(source, index);
    return {
        '<Path>k__BackingField': wrap(path, 'System.String'),
        '<LineNumber>k__BackingField': nullableInt64(position.lineNumber),
        '<BytePositionInLine>k__BackingField': nullableInt64(position.bytePositionInLine)
    };
}

function conversionFail(ctx, typeName, path, source, locations) {
    const location = locations.get(path);
    if (!location) {
        fail(ctx, 'System.NotSupportedException', `The JSON source location for '${path}' could not be resolved.`);
    }
    throwManagedException(
        ctx,
        'System.Text.Json.JsonException',
        `The JSON value could not be converted to ${typeName}.`,
        null,
        exceptionFields(source, path, location.end));
}

function appendPath(path, name) {
    return /^[A-Za-z_][A-Za-z0-9_]*$/.test(name)
        ? `${path}.${name}`
        : `${path}['${name.replaceAll('\\', '\\\\').replaceAll("'", "\\'")}']`;
}

function normalizeJsonSource(source, options) {
    if (options.readCommentHandling === 0 && !options.allowTrailingCommas) return source;
    const characters = source.split('');

    if (options.readCommentHandling === 1) {
        let inString = false;
        for (let index = 0; index < characters.length; index++) {
            const character = characters[index];
            if (inString) {
                if (character === '\\') {
                    index++;
                } else if (character === '"') {
                    inString = false;
                }
                continue;
            }
            if (character === '"') {
                inString = true;
                continue;
            }
            if (character !== '/' || index + 1 >= characters.length) continue;
            if (characters[index + 1] === '/') {
                let end = index;
                while (end < characters.length
                    && characters[end] !== '\r' && characters[end] !== '\n') end++;
                for (let current = index; current < end; current++) characters[current] = ' ';
                index = end - 1;
            } else if (characters[index + 1] === '*') {
                let end = index + 2;
                while (end + 1 < characters.length
                    && !(characters[end] === '*' && characters[end + 1] === '/')) end++;
                const foundEnd = end + 1 < characters.length;
                const replacementEnd = foundEnd ? end + 2 : characters.length;
                for (let current = index; current < replacementEnd; current++) {
                    if (characters[current] !== '\r' && characters[current] !== '\n') {
                        characters[current] = ' ';
                    }
                }
                index = replacementEnd - 1;
            }
        }
    }

    if (options.allowTrailingCommas) {
        let inString = false;
        for (let index = 0; index < characters.length; index++) {
            const character = characters[index];
            if (inString) {
                if (character === '\\') {
                    index++;
                } else if (character === '"') {
                    inString = false;
                }
                continue;
            }
            if (character === '"') {
                inString = true;
                continue;
            }
            if (character !== ',') continue;
            let next = index + 1;
            while (next < characters.length
                && (characters[next] === ' ' || characters[next] === '\t'
                    || characters[next] === '\r' || characters[next] === '\n')) next++;
            if (characters[next] === ']' || characters[next] === '}') characters[index] = ' ';
        }
    }

    return characters.join('');
}

function createSourceIndex(source, options) {
    const locations = new Map();
    let index = 0;
    let error = null;

    function reject(path, failureIndex = index) {
        error ??= { path, index: failureIndex };
        throw sourceIndexFailure;
    }

    function skipWhitespace() {
        while (index < source.length
            && (source[index] === ' ' || source[index] === '\t'
                || source[index] === '\r' || source[index] === '\n')) index++;
    }

    function skipComment(path) {
        if (options.readCommentHandling !== 1 || source[index] !== '/') return false;
        const start = index;
        if (source[index + 1] === '/') {
            index += 2;
            while (index < source.length && source[index] !== '\r' && source[index] !== '\n') index++;
            return true;
        }
        if (source[index + 1] === '*') {
            index += 2;
            while (index + 1 < source.length
                && !(source[index] === '*' && source[index + 1] === '/')) index++;
            if (index + 1 >= source.length) reject(path, start);
            index += 2;
            return true;
        }
        return false;
    }

    function skipTrivia(path) {
        while (true) {
            skipWhitespace();
            if (!skipComment(path)) return;
        }
    }

    function parseString(path, recordLocation) {
        const start = index;
        if (source[index] !== '"') reject(path);
        index++;
        while (index < source.length) {
            const character = source[index++];
            if (character === '"') {
                const end = index;
                let value;
                try {
                    value = JSON.parse(source.slice(start, end));
                } catch {
                    reject(path, start);
                }
                if (recordLocation) locations.set(path, { start, end });
                return value;
            }
            if (character.charCodeAt(0) < 0x20) reject(path, index - 1);
            if (character !== '\\') continue;
            if (index >= source.length) reject(path, index);
            const escape = source[index++];
            if ('"\\/bfnrt'.includes(escape)) continue;
            if (escape !== 'u') reject(path, index - 1);
            for (let digit = 0; digit < 4; digit++) {
                if (index >= source.length || !/[0-9A-Fa-f]/.test(source[index])) reject(path, index);
                index++;
            }
        }
        reject(path, index);
    }

    function parseNumber(path) {
        const start = index;
        if (source[index] === '-') index++;
        if (source[index] === '0') {
            index++;
            if (/[0-9]/.test(source[index] ?? '')) reject(path, index);
        } else if (/[1-9]/.test(source[index] ?? '')) {
            while (/[0-9]/.test(source[index] ?? '')) index++;
        } else {
            reject(path, index);
        }
        if (source[index] === '.') {
            index++;
            if (!/[0-9]/.test(source[index] ?? '')) reject(path, index);
            while (/[0-9]/.test(source[index] ?? '')) index++;
        }
        if (source[index] === 'e' || source[index] === 'E') {
            index++;
            if (source[index] === '+' || source[index] === '-') index++;
            if (!/[0-9]/.test(source[index] ?? '')) reject(path, index);
            while (/[0-9]/.test(source[index] ?? '')) index++;
        }
        locations.set(path, { start, end: index });
    }

    function parseLiteral(path, literal) {
        const start = index;
        for (let offset = 0; offset < literal.length; offset++) {
            if (source[index + offset] !== literal[offset]) reject(path, index + offset);
        }
        index += literal.length;
        locations.set(path, { start, end: index });
    }

    function parseArray(path, depth) {
        const start = index++;
        skipTrivia(path);
        if (source[index] === ']') {
            index++;
            locations.set(path, { start, end: index });
            return;
        }
        let elementIndex = 0;
        while (true) {
            const elementPath = `${path}[${elementIndex++}]`;
            parseValue(elementPath, depth + 1);
            skipTrivia(elementPath);
            if (source[index] === ']') {
                index++;
                locations.set(path, { start, end: index });
                return;
            }
            if (source[index] !== ',') reject(path, index);
            index++;
            skipTrivia(path);
            if (options.allowTrailingCommas && source[index] === ']') {
                index++;
                locations.set(path, { start, end: index });
                return;
            }
        }
    }

    function parseObject(path, depth) {
        const start = index++;
        skipTrivia(path);
        if (source[index] === '}') {
            index++;
            locations.set(path, { start, end: index });
            return;
        }
        while (true) {
            const name = parseString(path, false);
            skipWhitespace();
            if (source[index] !== ':') reject(path, index);
            index++;
            const memberPath = appendPath(path, name);
            parseValue(memberPath, depth + 1);
            skipTrivia(memberPath);
            if (source[index] === '}') {
                index++;
                locations.set(path, { start, end: index });
                return;
            }
            if (source[index] !== ',') reject(memberPath, index);
            index++;
            skipTrivia(path);
            if (options.allowTrailingCommas && source[index] === '}') {
                index++;
                locations.set(path, { start, end: index });
                return;
            }
        }
    }

    function parseValue(path, depth) {
        skipTrivia(path);
        switch (source[index]) {
            case '{':
                if (depth >= options.maxDepth) reject(path, index);
                parseObject(path, depth);
                return;
            case '[':
                if (depth >= options.maxDepth) reject(path, index);
                parseArray(path, depth);
                return;
            case '"': parseString(path, true); return;
            case 't': parseLiteral(path, 'true'); return;
            case 'f': parseLiteral(path, 'false'); return;
            case 'n': parseLiteral(path, 'null'); return;
            default:
                if (source[index] === '-' || /[0-9]/.test(source[index] ?? '')) {
                    parseNumber(path);
                    return;
                }
                reject(path, index);
        }
    }

    try {
        parseValue('$', 0);
        skipTrivia('$');
        if (index !== source.length) reject('$', index);
    } catch (failure) {
        if (failure !== sourceIndexFailure) throw failure;
    }
    return { locations, error };
}

function getTargetType(ctx) {
    const typeReference = ctx.genericParams?.TValue;
    if (typeReference === null || typeReference === undefined) {
        fail(ctx, 'System.NotSupportedException', 'The JSON target type could not be resolved.');
    }
    return resolveManagedTypeReference(typeReference);
}

function getProperties(typeName, options) {
    const groups = new Map();
    for (let current = typeName; typeof current === 'string'; current = getBaseTypeName(current)) {
        const metadata = globalThis.DotNet.Types[current];
        if (!metadata) break;
        for (const property of metadata.properties ?? []) {
            let group = groups.get(property.name);
            if (!group) {
                group = { jsonNames: new Set(), properties: [] };
                groups.set(property.name, group);
            }
            const jsonName = getJsonName(property, options);
            const jsonNameKey = getJsonNameKey(options, jsonName);
            if (group.jsonNames.has(jsonNameKey)) continue;
            group.jsonNames.add(jsonNameKey);
            group.properties.push(property);
        }
    }
    return [...groups.values()].flatMap(group => group.properties);
}

function findAttribute(property, typeName) {
    return (property.attributes ?? []).find(attribute => attribute.typeName === typeName) ?? null;
}

function getJsonName(property, options) {
    const attribute = findAttribute(property, 'System.Text.Json.Serialization.JsonPropertyNameAttribute');
    if (attribute) return attribute.constructorArguments?.[0] ?? property.name;
    return applyPropertyNamingPolicy(options.propertyNamingPolicy, property.name);
}

function readOptionField(state, name, defaultValue) {
    if (state[name] === undefined) return defaultValue;
    let value = state[name];
    while (value !== null && typeof value === 'object' && 't' in value && 'v' in value) {
        value = unwrap(value);
    }
    return value;
}

function requireDefaultOption(ctx, state, fieldName, optionName, defaultValue) {
    const value = readOptionField(state, fieldName, defaultValue);
    const matches = typeof defaultValue === 'boolean'
        ? value === defaultValue || value === (defaultValue ? 1 : 0)
        : value === defaultValue;
    if (!matches) {
        fail(ctx, 'System.NotSupportedException', `JsonSerializerOptions.${optionName} is not implemented.`);
    }
}

function getBooleanOption(ctx, state, fieldName, optionName) {
    const value = readOptionField(state, fieldName, false);
    if (value !== false && value !== true && value !== 0 && value !== 1) {
        fail(ctx, 'System.NotSupportedException', `JsonSerializerOptions.${optionName} is invalid.`);
    }
    return value === true || value === 1;
}

function getPropertyNamingPolicy(ctx, value) {
    if (value === undefined || isManagedNull(value)) return null;
    const typeName = typeof value?.t === 'string' ? value.t : null;
    const policies = new Map([
        ['System.Text.Json.JsonCamelCaseNamingPolicy', { kind: 'camel' }],
        ['System.Text.Json.JsonSnakeCaseLowerNamingPolicy', { kind: 'separated', separator: '_', lowercase: true }],
        ['System.Text.Json.JsonSnakeCaseUpperNamingPolicy', { kind: 'separated', separator: '_', lowercase: false }],
        ['System.Text.Json.JsonKebabCaseLowerNamingPolicy', { kind: 'separated', separator: '-', lowercase: true }],
        ['System.Text.Json.JsonKebabCaseUpperNamingPolicy', { kind: 'separated', separator: '-', lowercase: false }]
    ]);
    const policy = policies.get(typeName);
    if (!policy) {
        fail(ctx, 'System.NotSupportedException', `JsonNamingPolicy '${typeName ?? '<unknown>'}' is not implemented.`);
    }
    return policy;
}

function getReferenceHandling(ctx, value) {
    if (value === undefined || isManagedNull(value)) return 0;
    const typeName = typeof value?.t === 'string' ? value.t : null;
    if (typeName === 'System.Text.Json.Serialization.IgnoreReferenceHandler') return 1;
    if (typeName === 'System.Text.Json.Serialization.PreserveReferenceHandler') {
        fail(ctx, 'System.NotSupportedException', 'JsonSerializerOptions.ReferenceHandler.Preserve is not implemented.');
    }
    fail(ctx, 'System.NotSupportedException', `ReferenceHandler '${typeName ?? '<unknown>'}' is not implemented.`);
}

function getEnumConverters(ctx, value) {
    if (value === undefined || isManagedNull(value)) return [];
    const converterList = unwrap(value);
    const items = tryGetListItems(converterList?._list ?? value);
    if (items === null) {
        fail(ctx, 'System.NotSupportedException', 'JsonSerializerOptions.Converters has an unsupported representation.');
    }
    return items.map(converter => {
        const typeName = converter?.t;
        const generic = typeName === 'System.Text.Json.Serialization.JsonStringEnumConverter`1[TEnum]';
        if (typeName !== 'System.Text.Json.Serialization.JsonStringEnumConverter' && !generic) {
            fail(ctx, 'System.NotSupportedException', `JsonConverter '${typeName ?? '<unknown>'}' is not implemented.`);
        }
        const state = unwrap(converter);
        const converterOptions = Number(readOptionField(state, '_converterOptions', 3));
        if (!Number.isInteger(converterOptions) || (converterOptions & ~3) !== 0) {
            fail(ctx, 'System.NotSupportedException', `JsonConverter '${typeName}' has invalid options.`);
        }
        const targetType = generic
            ? converter?.$typeArgs?.TEnum ?? null
            : null;
        return {
            targetType: targetType === null ? null : getManagedTypeName(targetType),
            namingPolicy: getPropertyNamingPolicy(ctx, state._namingPolicy),
            allowStrings: (converterOptions & 1) !== 0,
            allowNumbers: (converterOptions & 2) !== 0
        };
    });
}

function getSerializerOptions(ctx, options) {
    if (isManagedNull(options)) {
        return Object.freeze({
            allowTrailingCommas: false,
            dictionaryKeyPolicy: null,
            propertyNameCaseInsensitive: false,
            propertyNamingPolicy: null,
            readCommentHandling: 0,
            maxDepth: 64,
            writeIndented: false,
            ignoreNullValues: false,
            defaultIgnoreCondition: 0,
            ignoreReadOnlyFields: false,
            ignoreReadOnlyProperties: false,
            includeFields: false,
            numberHandling: 0,
            unmappedMemberHandling: 0,
            referenceHandling: 0,
            enumConverters: []
        });
    }
    const state = unwrap(options);
    if (state === null || typeof state !== 'object') {
        fail(ctx, 'System.NotSupportedException', 'The JsonSerializerOptions representation is invalid.');
    }
    requireDefaultOption(ctx, state, '_preferredObjectCreationHandling', 'PreferredObjectCreationHandling', 0);
    requireDefaultOption(ctx, state, '_unknownTypeHandling', 'UnknownTypeHandling', 0);
    for (const [fieldName, optionName] of [
        ['_encoder', 'Encoder'],
        ['_typeInfoResolver', 'TypeInfoResolver'],
        ['_typeInfoResolverChain', 'TypeInfoResolverChain']
    ]) {
        const value = state[fieldName];
        if (value !== undefined && !isManagedNull(value)) {
            fail(ctx, 'System.NotSupportedException', `JsonSerializerOptions.${optionName} is not implemented.`);
        }
    }

    const propertyNameCaseInsensitiveValue = readOptionField(
        state, '_propertyNameCaseInsensitive', false);
    if (propertyNameCaseInsensitiveValue !== false && propertyNameCaseInsensitiveValue !== true
        && propertyNameCaseInsensitiveValue !== 0 && propertyNameCaseInsensitiveValue !== 1) {
        fail(ctx, 'System.NotSupportedException', 'JsonSerializerOptions.PropertyNameCaseInsensitive is invalid.');
    }
    const propertyNameCaseInsensitive = propertyNameCaseInsensitiveValue === true
        || propertyNameCaseInsensitiveValue === 1;
    const allowTrailingCommas = getBooleanOption(
        ctx, state, '_allowTrailingCommas', 'AllowTrailingCommas');
    const readCommentHandling = readOptionField(state, '_readCommentHandling', 0);
    if (readCommentHandling !== 0 && readCommentHandling !== 1) {
        fail(ctx, 'System.InvalidOperationException', 'The specified JsonCommentHandling value is not valid for the serializer.');
    }
    const configuredMaxDepth = readOptionField(state, '_maxDepth', 0);
    if (!Number.isInteger(configuredMaxDepth) || configuredMaxDepth < 0) {
        fail(ctx, 'System.InvalidOperationException', 'The specified MaxDepth value is not valid.');
    }
    const maxDepth = configuredMaxDepth === 0 ? 64 : configuredMaxDepth;
    const writeIndented = getBooleanOption(ctx, state, '_writeIndented', 'WriteIndented');
    const ignoreNullValues = getBooleanOption(
        ctx, state, '_ignoreNullValues', 'IgnoreNullValues');
    const defaultIgnoreCondition = readOptionField(state, '_defaultIgnoreCondition', 0);
    if (defaultIgnoreCondition !== 0 && defaultIgnoreCondition !== 2
        && defaultIgnoreCondition !== 3) {
        fail(ctx, 'System.InvalidOperationException', 'The specified JsonIgnoreCondition value is not valid as a default.');
    }
    if (ignoreNullValues && defaultIgnoreCondition !== 0) {
        fail(ctx, 'System.InvalidOperationException', 'DefaultIgnoreCondition and IgnoreNullValues cannot both be set.');
    }
    const ignoreReadOnlyFields = getBooleanOption(
        ctx, state, '_ignoreReadonlyFields', 'IgnoreReadOnlyFields');
    const ignoreReadOnlyProperties = getBooleanOption(
        ctx, state, '_ignoreReadOnlyProperties', 'IgnoreReadOnlyProperties');
    const includeFields = getBooleanOption(ctx, state, '_includeFields', 'IncludeFields');
    const numberHandling = readOptionField(state, '_numberHandling', 0);
    if (!Number.isInteger(numberHandling) || numberHandling < 0 || (numberHandling & ~7) !== 0) {
        fail(ctx, 'System.InvalidOperationException', `The specified JsonNumberHandling value '${JSON.stringify(numberHandling)}' is not valid.`);
    }
    const unmappedMemberHandling = readOptionField(state, '_unmappedMemberHandling', 0);
    if (unmappedMemberHandling !== 0 && unmappedMemberHandling !== 1) {
        fail(ctx, 'System.InvalidOperationException', 'The specified JsonUnmappedMemberHandling value is not valid.');
    }
    state._isReadOnly = wrap(true, 'System.Boolean');
    return Object.freeze({
        allowTrailingCommas,
        dictionaryKeyPolicy: getPropertyNamingPolicy(ctx, state._dictionaryKeyPolicy),
        propertyNameCaseInsensitive,
        propertyNamingPolicy: getPropertyNamingPolicy(ctx, state._jsonPropertyNamingPolicy),
        readCommentHandling,
        maxDepth,
        writeIndented,
        ignoreNullValues,
        defaultIgnoreCondition,
        ignoreReadOnlyFields,
        ignoreReadOnlyProperties,
        includeFields,
        numberHandling,
        unmappedMemberHandling,
        referenceHandling: getReferenceHandling(ctx, state._referenceHandler),
        enumConverters: getEnumConverters(ctx, state._converters)
    });
}

function isUpperCharacter(character) {
    return /^\p{Lu}$/u.test(character);
}

function isLowerCharacter(character) {
    return /^\p{Ll}$/u.test(character);
}

function isDecimalDigit(character) {
    return /^\p{Nd}$/u.test(character);
}

function isSpaceSeparator(character) {
    return /^\p{Zs}$/u.test(character);
}

function convertInvariantCharacter(character, lowercase) {
    const converted = lowercase ? character.toLowerCase() : character.toUpperCase();
    return converted.length === 1 ? converted : character;
}

function toCamelCase(value) {
    if (value.length === 0 || !isUpperCharacter(value[0])) return value;
    const characters = value.split('');
    for (let index = 0; index < characters.length; index++) {
        if (index === 1 && !isUpperCharacter(characters[index])) break;
        const hasNext = index + 1 < characters.length;
        if (index > 0 && hasNext && !isUpperCharacter(characters[index + 1])) {
            if (characters[index + 1] === ' ') {
                characters[index] = convertInvariantCharacter(characters[index], true);
            }
            break;
        }
        characters[index] = convertInvariantCharacter(characters[index], true);
    }
    return characters.join('');
}

function toSeparatedCase(value, separator, lowercase) {
    let result = '';
    let state = 'notStarted';
    for (let index = 0; index < value.length; index++) {
        let character = value[index];
        if (isUpperCharacter(character)) {
            if (state === 'lowerOrDigit' || state === 'space') {
                result += separator;
            } else if (state === 'upper' && index + 1 < value.length
                && isLowerCharacter(value[index + 1])) {
                result += separator;
            }
            if (lowercase) character = convertInvariantCharacter(character, true);
            result += character;
            state = 'upper';
        } else if (isLowerCharacter(character) || isDecimalDigit(character)) {
            if (state === 'space') result += separator;
            if (!lowercase && isLowerCharacter(character)) {
                character = convertInvariantCharacter(character, false);
            }
            result += character;
            state = 'lowerOrDigit';
        } else if (isSpaceSeparator(character)) {
            if (state !== 'notStarted') state = 'space';
        } else {
            result += character;
            state = 'notStarted';
        }
    }
    return result;
}

function applyPropertyNamingPolicy(policy, name) {
    if (!policy) return name;
    return policy.kind === 'camel'
        ? toCamelCase(name)
        : toSeparatedCase(name, policy.separator, policy.lowercase);
}

function foldOrdinalIgnoreCase(value) {
    return value.split('').map(character =>
        convertInvariantCharacter(character, false)).join('');
}

function getJsonNameKey(options, name) {
    return options.propertyNameCaseInsensitive ? foldOrdinalIgnoreCase(name) : name;
}

function findInput(value, jsonName, options) {
    if (!options.propertyNameCaseInsensitive) {
        return Object.prototype.hasOwnProperty.call(value, jsonName)
            ? { name: jsonName, value: value[jsonName] }
            : null;
    }
    const key = getJsonNameKey(options, jsonName);
    let result = null;
    for (const [name, inputValue] of Object.entries(value)) {
        if (getJsonNameKey(options, name) === key) result = { name, value: inputValue };
    }
    return result;
}

function getIgnoreCondition(member) {
    const attribute = findAttribute(member, 'System.Text.Json.Serialization.JsonIgnoreAttribute');
    if (!attribute) return null;
    return attribute.namedArguments?.Condition ?? 1;
}

function isRequired(member) {
    return findAttribute(
        member,
        'System.Text.Json.Serialization.JsonRequiredAttribute') !== null
        || findAttribute(
            member,
            'System.Runtime.CompilerServices.RequiredMemberAttribute') !== null;
}

function getNumberHandling(ctx, member, fallback = 0) {
    const attribute = findAttribute(
        member,
        'System.Text.Json.Serialization.JsonNumberHandlingAttribute');
    if (!attribute) return fallback;
    const handling = attribute.constructorArguments?.[0];
    if (!Number.isInteger(handling) || handling < 0 || (handling & ~7) !== 0) {
        fail(ctx, 'System.InvalidOperationException', 'The specified JsonNumberHandling value is not valid.');
    }
    return handling;
}

function getUnmappedMemberHandling(ctx, metadata, fallback) {
    const attribute = findAttribute(
        metadata,
        'System.Text.Json.Serialization.JsonUnmappedMemberHandlingAttribute');
    if (!attribute) return fallback;
    const handling = attribute.constructorArguments?.[0];
    if (handling !== 0 && handling !== 1) {
        fail(ctx, 'System.InvalidOperationException', 'The specified JsonUnmappedMemberHandling value is not valid.');
    }
    return handling;
}

function unmappedMemberFail(ctx, typeName, name, path, sourceText, locations) {
    const memberPath = appendPath(path, name);
    const location = locations.get(memberPath);
    if (!location) {
        fail(ctx, 'System.NotSupportedException', `The JSON source location for '${memberPath}' could not be resolved.`);
    }
    throwManagedException(
        ctx,
        'System.Text.Json.JsonException',
        `The JSON property '${name}' could not be mapped to any .NET member contained in type '${typeName}'.`,
        null,
        exceptionFields(sourceText, memberPath, location.end));
}

function requiredFail(ctx, contract, path, sourceText, locations) {
    const location = locations.get(path);
    if (!location) {
        fail(ctx, 'System.NotSupportedException', `The JSON source location for '${path}' could not be resolved.`);
    }
    throwManagedException(
        ctx,
        'System.Text.Json.JsonException',
        `JSON deserialization for type was missing required properties, including '${contract.jsonName}'.`,
        null,
        exceptionFields(sourceText, path, location.end));
}

function canTypeBeNull(typeName) {
    return typeName === 'System.String'
        || typeName.endsWith('[]')
        || globalThis.DotNet.Types[typeName]?.isClass === true;
}

function getMemberContracts(ctx, typeName, options) {
    const contracts = [];
    const jsonNames = new Set();
    const typeNumberHandling = getNumberHandling(
        ctx, globalThis.DotNet.Types[typeName], options.numberHandling);
    for (const property of getProperties(typeName, options)) {
        const ignoreCondition = getIgnoreCondition(property);
        if (property.indexParameterCount !== 0 || ignoreCondition === 1) continue;
        const included = findAttribute(property, 'System.Text.Json.Serialization.JsonIncludeAttribute') !== null;
        const publiclyVisible = property.getterIsPublic === true || property.setterIsPublic === true;
        if (!publiclyVisible && !included) continue;
        const required = isRequired(property);
        const jsonName = getJsonName(property, options);
        const jsonNameKey = getJsonNameKey(options, jsonName);
        if (jsonNames.has(jsonNameKey)) {
            fail(ctx, 'System.InvalidOperationException', `Multiple properties bind to JSON name '${jsonName}'.`);
        }
        jsonNames.add(jsonNameKey);
        contracts.push({
            kind: 'property',
            member: property,
            jsonName,
            included,
            required,
            ignoreNullOnRead: options.ignoreNullValues
                && ignoreCondition === null
                && !required
                && canTypeBeNull(property.typeName),
            numberHandling: getNumberHandling(ctx, property, typeNumberHandling)
        });
    }
    for (const metadata of getTypeHierarchy(typeName)) {
        for (const field of metadata.fields ?? []) {
            const ignoreCondition = getIgnoreCondition(field);
            if (field.isStatic || ignoreCondition === 1) continue;
            const included = findAttribute(field, 'System.Text.Json.Serialization.JsonIncludeAttribute') !== null;
            if (!included && !(options.includeFields && field.isPublic === true)) continue;
            const required = isRequired(field);
            const jsonName = getJsonName(field, options);
            const jsonNameKey = getJsonNameKey(options, jsonName);
            if (jsonNames.has(jsonNameKey)) {
                fail(ctx, 'System.InvalidOperationException', `Multiple members bind to JSON name '${jsonName}'.`);
            }
            jsonNames.add(jsonNameKey);
            contracts.push({
                kind: 'field',
                member: field,
                jsonName,
                included,
                required,
                ignoreNullOnRead: options.ignoreNullValues
                    && ignoreCondition === null
                    && !required
                    && canTypeBeNull(field.typeName),
                numberHandling: getNumberHandling(ctx, field, typeNumberHandling)
            });
        }
    }
    return contracts;
}

function selectConstructor(ctx, typeName, metadata) {
    const constructors = metadata.constructors ?? [];
    const attributed = constructors.filter(constructor =>
        findAttribute(constructor, 'System.Text.Json.Serialization.JsonConstructorAttribute'));
    if (attributed.length > 1) {
        fail(ctx, 'System.InvalidOperationException', `Type '${typeName}' has more than one JsonConstructor.`);
    }
    if (attributed.length === 1) return attributed[0];

    const publicConstructors = constructors.filter(constructor => constructor.isPublic === true);
    const parameterless = publicConstructors.find(constructor => constructor.parameters.length === 0);
    if (parameterless) return parameterless;
    if (metadata.isValueType === true) return null;
    if (publicConstructors.length === 1) return publicConstructors[0];
    fail(ctx, 'System.NotSupportedException', `Type '${typeName}' does not have a supported constructor.`);
}

function getConstructorParameterDefault(ctx, parameter) {
    if (parameter.hasDefaultValue !== true) return getDefaultValue(parameter.typeName);
    if (parameter.defaultValue === null) return null;
    switch (parameter.typeName) {
        case 'System.String':
            if (typeof parameter.defaultValue !== 'string') break;
            return wrap(parameter.defaultValue, parameter.typeName);
        case 'System.Boolean':
            if (typeof parameter.defaultValue !== 'boolean') break;
            return wrap(parameter.defaultValue, parameter.typeName);
        case 'System.Char':
            if (typeof parameter.defaultValue !== 'string'
                || parameter.defaultValue.length !== 1) break;
            return wrap(parameter.defaultValue.charCodeAt(0), parameter.typeName);
        case 'System.Single':
            if (typeof parameter.defaultValue !== 'number') break;
            return wrap(Math.fround(parameter.defaultValue), parameter.typeName);
        case 'System.Double':
            if (typeof parameter.defaultValue !== 'number') break;
            return wrap(parameter.defaultValue, parameter.typeName);
        case 'System.Byte':
        case 'System.SByte':
        case 'System.Int16':
        case 'System.UInt16':
        case 'System.Int32':
        case 'System.UInt32':
            if (typeof parameter.defaultValue !== 'number') break;
            return wrap(parameter.defaultValue, parameter.typeName);
        case 'System.Int64':
        case 'System.UInt64':
            if (typeof parameter.defaultValue !== 'number'
                && typeof parameter.defaultValue !== 'bigint') break;
            return wrap(BigInt(parameter.defaultValue), parameter.typeName);
    }
    fail(
        ctx,
        'System.NotSupportedException',
        `Constructor parameter '${parameter.name}' has an unsupported default value.`);
}

function bindConstructor(ctx, typeName, metadata, contracts, value, path, sourceText, locations, options) {
    const constructorMetadata = selectConstructor(ctx, typeName, metadata);
    const consumedMembers = new Set();
    const contractsByClrName = new Map();
    for (const contract of contracts) {
        const name = contract.member.name.toLowerCase();
        const matches = contractsByClrName.get(name) ?? [];
        matches.push(contract);
        contractsByClrName.set(name, matches);
    }
    const argumentsToPass = (constructorMetadata?.parameters ?? []).map(parameter => {
        if (typeof parameter.name !== 'string') {
            fail(ctx, 'System.NotSupportedException', `Constructor '${constructorMetadata.methodName}' has a parameter without metadata name.`);
        }
        const matches = contractsByClrName.get(parameter.name.toLowerCase()) ?? [];
        if (matches.length !== 1 || matches[0].member.typeName !== parameter.typeName) {
            fail(
                ctx,
                'System.InvalidOperationException',
                `Constructor parameter '${parameter.name}' on '${typeName}' does not bind to exactly one property.`);
        }
        const contract = matches[0];
        consumedMembers.add(contract.member);
        const input = findInput(value, contract.jsonName, options);
        if (!input || (input.value === null && contract.ignoreNullOnRead)) {
            return getConstructorParameterDefault(ctx, parameter);
        }
        return convert(
            ctx,
            input.value,
            parameter,
            appendPath(path, input.name),
            sourceText,
            locations,
            options,
            contract.numberHandling);
    });

    const instance = initializeObject(typeName);
    if (constructorMetadata === null) {
        return { instance, consumedMembers };
    }
    const constructor = globalThis.DotNet.Methods[constructorMetadata.methodName];
    if (typeof constructor !== 'function') {
        fail(ctx, 'System.NotSupportedException', `Constructor '${constructorMetadata.methodName}' is unavailable.`);
    }
    constructor(ctx, instance, ...argumentsToPass);
    return { instance, consumedMembers };
}

function parseInteger(ctx, node, typeName, minimum, maximum, path, sourceText, locations, numberHandling) {
    const numberSource = typeof node === 'string' && (numberHandling & 1) !== 0
        ? node
        : node?.[exactNumber];
    if (typeof numberSource !== 'string' || !/^-?(?:0|[1-9]\d*)$/.test(numberSource)) {
        conversionFail(ctx, typeName, path, sourceText, locations);
    }
    const value = BigInt(numberSource);
    if (value < minimum || value > maximum) {
        conversionFail(ctx, typeName, path, sourceText, locations);
    }
    return value;
}

function parseFloating(ctx, node, typeName, path, sourceText, locations, numberHandling) {
    if (typeof node === 'string') {
        if (node === 'NaN' || node === 'Infinity' || node === '-Infinity') {
            if ((numberHandling & 5) === 0) {
                conversionFail(ctx, typeName, path, sourceText, locations);
            }
            const namedValue = node === 'NaN' ? Number.NaN : node === 'Infinity' ? Infinity : -Infinity;
            return typeName === 'System.Single' ? Math.fround(namedValue) : namedValue;
        }
        if ((numberHandling & 1) === 0
            || !/^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$/.test(node)) {
            conversionFail(ctx, typeName, path, sourceText, locations);
        }
        const quotedValue = Number(node);
        return typeName === 'System.Single' ? Math.fround(quotedValue) : quotedValue;
    }
    const numberSource = node?.[exactNumber];
    if (typeof numberSource !== 'string') {
        conversionFail(ctx, typeName, path, sourceText, locations);
    }
    const value = Number(numberSource);
    return typeName === 'System.Single' ? Math.fround(value) : value;
}

function decodeBase64(value) {
    const source = value.replace(/[ \t\r\n]/g, '');
    if (!/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(source)) {
        return null;
    }
    const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
    const bytes = [];
    for (let index = 0; index < source.length; index += 4) {
        const first = alphabet.indexOf(source[index]);
        const second = alphabet.indexOf(source[index + 1]);
        const third = source[index + 2] === '=' ? 0 : alphabet.indexOf(source[index + 2]);
        const fourth = source[index + 3] === '=' ? 0 : alphabet.indexOf(source[index + 3]);
        const block = (first << 18) | (second << 12) | (third << 6) | fourth;
        bytes.push((block >>> 16) & 255);
        if (source[index + 2] !== '=') bytes.push((block >>> 8) & 255);
        if (source[index + 3] !== '=') bytes.push(block & 255);
    }
    return bytes;
}

function hasIsolatedSurrogate(value) {
    for (let index = 0; index < value.length; index++) {
        const code = value.charCodeAt(index);
        if (code >= 0xd800 && code <= 0xdbff) {
            const next = index + 1 < value.length ? value.charCodeAt(index + 1) : -1;
            if (next < 0xdc00 || next > 0xdfff) return true;
            index++;
        } else if (code >= 0xdc00 && code <= 0xdfff) {
            return true;
        }
    }
    return false;
}

function getEnumConverter(options, typeName) {
    return options.enumConverters.find(converter =>
        converter.targetType === null || converter.targetType === typeName) ?? null;
}

function getEnumWidth(ctx, underlyingType) {
    const widths = {
        'System.SByte': 8, 'System.Byte': 8,
        'System.Int16': 16, 'System.UInt16': 16,
        'System.Int32': 32, 'System.UInt32': 32,
        'System.Int64': 64, 'System.UInt64': 64
    };
    const width = widths[underlyingType];
    if (width === undefined) {
        fail(ctx, 'System.NotSupportedException', 'Enum underlying type ' + underlyingType + ' is not implemented.');
    }
    return width;
}

function enumBits(ctx, value, underlyingType) {
    return BigInt.asUintN(getEnumWidth(ctx, underlyingType), BigInt(value));
}

function enumValueFromBits(ctx, value, underlyingType) {
    const width = getEnumWidth(ctx, underlyingType);
    let normalized = BigInt.asUintN(width, value);
    if (underlyingType === 'System.SByte'
        || underlyingType === 'System.Int16'
        || underlyingType === 'System.Int32'
        || underlyingType === 'System.Int64') {
        normalized = BigInt.asIntN(width, normalized);
    }
    return underlyingType === 'System.Int64' || underlyingType === 'System.UInt64'
        ? normalized
        : Number(normalized);
}

function parseEnumString(ctx, value, metadata, converter) {
    if (!converter.allowStrings || value.trim().length === 0) return null;
    let combined = 0n;
    const parts = value.split(',');
    if (parts.length > 1 && !metadata.enumIsFlags) return null;
    for (const part of parts) {
        const name = part.trim();
        const entry = (metadata.enumValues ?? []).find(candidate => {
            const converted = applyPropertyNamingPolicy(converter.namingPolicy, candidate.name);
            return candidate.name.toLowerCase() === name.toLowerCase()
                || converted === name;
        });
        if (!entry) return null;
        combined |= enumBits(ctx, entry.value, metadata.enumUnderlyingType);
    }
    return enumValueFromBits(ctx, combined, metadata.enumUnderlyingType);
}

function convert(ctx, value, declaredType, path, sourceText, locations, options, numberHandling = options.numberHandling) {
    const typeReference = resolveManagedTypeReference(declaredType);
    const typeName = getManagedTypeName(typeReference);
    if (value === null) {
        if (typeName === 'System.String' || typeName.endsWith('[]')
            || globalThis.DotNet.Types[typeName]?.isClass === true) return null;
        conversionFail(ctx, typeName, path, sourceText, locations);
    }
    switch (typeName) {
        case 'System.String':
            if (typeof value !== 'string' || hasIsolatedSurrogate(value)) {
                conversionFail(ctx, typeName, path, sourceText, locations);
            }
            return wrap(value, typeName);
        case 'System.Char':
            if (typeof value !== 'string' || value.length !== 1) {
                conversionFail(ctx, typeName, path, sourceText, locations);
            }
            return wrap(value.charCodeAt(0), typeName);
        case 'System.Boolean':
            if (typeof value !== 'boolean') conversionFail(ctx, typeName, path, sourceText, locations);
            return wrap(value, typeName);
        case 'System.Byte': return wrap(Number(parseInteger(ctx, value, typeName, 0n, 255n, path, sourceText, locations, numberHandling)), typeName);
        case 'System.SByte': return wrap(Number(parseInteger(ctx, value, typeName, -128n, 127n, path, sourceText, locations, numberHandling)), typeName);
        case 'System.Int16': return wrap(Number(parseInteger(ctx, value, typeName, -32768n, 32767n, path, sourceText, locations, numberHandling)), typeName);
        case 'System.UInt16': return wrap(Number(parseInteger(ctx, value, typeName, 0n, 65535n, path, sourceText, locations, numberHandling)), typeName);
        case 'System.Int32': return wrap(Number(parseInteger(ctx, value, typeName, -2147483648n, 2147483647n, path, sourceText, locations, numberHandling)), typeName);
        case 'System.UInt32': return wrap(Number(parseInteger(ctx, value, typeName, 0n, 4294967295n, path, sourceText, locations, numberHandling)), typeName);
        case 'System.Int64': return wrap(parseInteger(ctx, value, typeName, -9223372036854775808n, 9223372036854775807n, path, sourceText, locations, numberHandling), typeName);
        case 'System.UInt64': return wrap(parseInteger(ctx, value, typeName, 0n, 18446744073709551615n, path, sourceText, locations, numberHandling), typeName);
        case 'System.Single':
        case 'System.Double': return wrap(parseFloating(ctx, value, typeName, path, sourceText, locations, numberHandling), typeName);
        case 'System.DateTime':
        case 'System.DateTimeOffset':
        case 'System.DateOnly':
        case 'System.TimeOnly':
        case 'System.TimeSpan': {
            const temporal = parseManagedTemporal(typeName, value);
            if (temporal === null) conversionFail(ctx, typeName, path, sourceText, locations);
            return temporal;
        }
    }

    const enumMetadata = globalThis.DotNet.Types[typeName];
    if (typeof enumMetadata?.enumUnderlyingType === 'string') {
        const converter = getEnumConverter(options, typeName);
        if (typeof value === 'string') {
            const named = converter === null
                ? null
                : parseEnumString(ctx, value, enumMetadata, converter);
            if (named === null) conversionFail(ctx, typeName, path, sourceText, locations);
            return wrap(named, typeName);
        }
        if (converter !== null && !converter.allowNumbers) {
            conversionFail(ctx, typeName, path, sourceText, locations);
        }
        const converted = convert(
            ctx,
            value,
            enumMetadata.enumUnderlyingType,
            path,
            sourceText,
            locations,
            options,
            0);
        return wrap(unwrap(converted), typeName);
    }

    if (typeName === 'System.Byte[]') {
        if (typeof value !== 'string') conversionFail(ctx, typeName, path, sourceText, locations);
        const bytes = decodeBase64(value);
        if (bytes === null) conversionFail(ctx, typeName, path, sourceText, locations);
        return wrap(bytes.map(byte => wrap(byte, 'System.Byte')), typeName);
    }

    if (typeName.endsWith('[]')) {
        if (!Array.isArray(value)) conversionFail(ctx, typeName, path, sourceText, locations);
        const elementType = typeName.slice(0, -2);
        return wrap(
            value.map((element, index) => convert(
                ctx, element, elementType, `${path}[${index}]`, sourceText, locations, options, numberHandling)),
            typeName);
    }

    const collection = findCollectionContract(typeReference);
    if (collection !== null) {
        if (collection.kind === 'list') {
            if (!Array.isArray(value)) conversionFail(ctx, typeName, path, sourceText, locations);
            const instance = constructManagedCollection(ctx, collection.implementationType);
            for (let index = 0; index < value.length; index++) {
                invokeManagedVirtual(
                    ctx,
                    instance,
                    'Add(T)',
                    convert(
                        ctx,
                        value[index],
                        collection.elementType,
                        `${path}[${index}]`,
                        sourceText,
                        locations,
                        options,
                        numberHandling));
            }
            return instance;
        }

        if (Array.isArray(value) || typeof value !== 'object' || value?.[exactNumber] !== undefined) {
            conversionFail(ctx, typeName, path, sourceText, locations);
        }
        if (getManagedTypeName(collection.keyType) !== 'System.String') {
            fail(ctx, 'System.NotSupportedException', 'Only string dictionary keys are currently implemented.');
        }
        const instance = constructManagedCollection(ctx, collection.implementationType);
        for (const [name, dictionaryValue] of Object.entries(value)) {
            invokeManagedVirtual(
                ctx,
                instance,
                'Add(TKey, TValue)',
                wrap(name, 'System.String'),
                convert(
                    ctx,
                    dictionaryValue,
                    collection.valueType,
                    appendPath(path, name),
                    sourceText,
                    locations,
                    options,
                    numberHandling));
        }
        return instance;
    }

    const metadata = globalThis.DotNet.Types[typeName];
    if (!isManagedObjectContract(metadata) || Array.isArray(value) || typeof value !== 'object' || value?.[exactNumber] !== undefined) {
        fail(ctx, 'System.NotSupportedException', `JSON conversion for managed type '${typeName}' is not implemented.`);
    }

    const contracts = getMemberContracts(ctx, typeName, options);
    if (getUnmappedMemberHandling(ctx, metadata, options.unmappedMemberHandling) === 1) {
        const contractNames = new Set(contracts.map(contract =>
            getJsonNameKey(options, contract.jsonName)));
        for (const name of Object.keys(value)) {
            if (!contractNames.has(getJsonNameKey(options, name))) {
                unmappedMemberFail(ctx, typeName, name, path, sourceText, locations);
            }
        }
    }
    for (const contract of contracts) {
        if (contract.required
            && !findInput(value, contract.jsonName, options)) {
            requiredFail(ctx, contract, path, sourceText, locations);
        }
    }
    const { instance, consumedMembers } = bindConstructor(
        ctx, typeName, metadata, contracts, value, path, sourceText, locations, options);
    const candidates = new Map();
    for (const contract of contracts) {
        const member = contract.member;
        if (consumedMembers.has(member)) continue;
        if (contract.kind === 'property'
            && member.setter
            && (member.setterIsPublic === true || contract.included)) {
            candidates.set(getJsonNameKey(options, contract.jsonName), contract);
        } else if (contract.kind === 'field' && member.isInitOnly !== true) {
            candidates.set(getJsonNameKey(options, contract.jsonName), contract);
        }
    }
    for (const [name, propertyValue] of Object.entries(value)) {
        const contract = candidates.get(getJsonNameKey(options, name));
        if (!contract) continue;
        if (propertyValue === null && contract.ignoreNullOnRead) continue;
        const converted = convert(
            ctx,
            propertyValue,
            contract.member,
            appendPath(path, name),
            sourceText,
            locations,
            options,
            contract.numberHandling);
        if (contract.kind === 'property') {
            setPropertyValue(
                ctx,
                wrap({ metadata: contract.member }, 'System.Reflection.PropertyInfo'),
                instance,
                converted);
        } else {
            unwrap(instance)[contract.member.storageKey ?? contract.member.name] = converted;
        }
    }
    return instance;
}

globalThis.DotNet.Methods['System.Text.Json.JsonSerializer.Deserialize<TValue>(System.String, System.Text.Json.JsonSerializerOptions)'] = function (ctx, json, options) {
    if (isManagedNull(json)) fail(ctx, 'System.ArgumentNullException', 'Value cannot be null. (Parameter \'json\')');
    const serializerOptions = getSerializerOptions(ctx, options);

    const source = unwrap(json);
    const sourceIndex = createSourceIndex(source, serializerOptions);
    const normalizedSource = normalizeJsonSource(source, serializerOptions);
    let parsed;
    try {
        parsed = JSON.parse(normalizedSource, (key, value, context) => {
            if (typeof value !== 'number') return value;
            if (typeof context?.source !== 'string') {
                fail(ctx, 'System.NotSupportedException', 'This JavaScript runtime does not expose exact JSON number tokens.');
            }
            return Object.freeze({ [exactNumber]: context.source });
        });
    } catch (error) {
        if (error instanceof SyntaxError) {
            const syntax = sourceIndex.error;
            if (!syntax) {
                fail(ctx, 'System.NotSupportedException', 'The JSON syntax error location could not be resolved.');
            }
            throwManagedException(
                ctx,
                'System.Text.Json.JsonException',
                error.message,
                null,
                exceptionFields(source, syntax.path, syntax.index));
        }
        throw error;
    }
    if (sourceIndex.error) {
        throwManagedException(
            ctx,
            'System.Text.Json.JsonException',
            'The JSON value could not be read.',
            null,
            exceptionFields(
                source,
                sourceIndex.error.path,
                sourceIndex.error.index));
    }
    return convert(ctx, parsed, getTargetType(ctx), '$', source, sourceIndex.locations, serializerOptions);
};
