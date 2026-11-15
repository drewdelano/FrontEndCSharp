import { throwManagedException } from '../managed-exceptions.js';
import { tryGetListItems } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';
import { formatManagedTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { getPropertyValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
import { getManagedTypeArguments, getManagedTypeName, isManagedNull, resolveManagedTypeReference, unwrap, wrap } from '../msil-type-helpers.js';

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

function getValueTypeReference(value, declaredType) {
    const resolved = resolveManagedTypeReference(declaredType);
    if (value?.$typeArgs && typeof value?.t === 'string') {
        return { typeName: value.t, genericParams: value.$typeArgs };
    }
    return resolved;
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
        if (argumentsToUse.length === 2) {
            return { kind: 'dictionary', keyType: argumentsToUse[0], valueType: argumentsToUse[1] };
        }
    }
    if (typeName === 'System.Collections.Generic.List`1[T]'
        || typeName === 'System.Collections.Generic.IList`1[T]'
        || typeName === 'System.Collections.Generic.IReadOnlyList`1[T]') {
        if (argumentsToUse.length === 1) return { kind: 'list', elementType: argumentsToUse[0] };
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

function getIgnoreCondition(property) {
    const attribute = findAttribute(property, 'System.Text.Json.Serialization.JsonIgnoreAttribute');
    if (!attribute) return null;
    return attribute.namedArguments?.Condition ?? 1;
}

function getPropertyOrder(member) {
    const attribute = findAttribute(
        member,
        'System.Text.Json.Serialization.JsonPropertyOrderAttribute');
    return attribute?.constructorArguments?.[0] ?? 0;
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

function normalizeEnumValue(ctx, value, underlyingType) {
    const widths = {
        'System.SByte': 8,
        'System.Byte': 8,
        'System.Int16': 16,
        'System.UInt16': 16,
        'System.Int32': 32,
        'System.UInt32': 32,
        'System.Int64': 64,
        'System.UInt64': 64
    };
    const width = widths[underlyingType];
    if (width === undefined) {
        fail(ctx, 'System.NotSupportedException', `Enum underlying type '${underlyingType}' is not implemented.`);
    }
    let normalized = BigInt.asUintN(width, BigInt(value));
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

function formatEnumName(ctx, value, metadata, converter) {
    const bits = enumBits(ctx, value, metadata.enumUnderlyingType);
    const entries = (metadata.enumValues ?? []).map(entry => ({
        ...entry,
        bits: enumBits(ctx, entry.value, metadata.enumUnderlyingType)
    }));
    const exact = entries.find(entry => entry.bits === bits);
    if (exact) return applyPropertyNamingPolicy(converter.namingPolicy, exact.name);
    if (!metadata.enumIsFlags || bits === 0n) return null;

    let remaining = bits;
    const names = [];
    const candidates = entries
        .filter(entry => entry.bits !== 0n)
        .sort((left, right) => left.bits < right.bits ? -1 : left.bits > right.bits ? 1 : 0);
    for (let index = candidates.length - 1; index >= 0; index--) {
        const entry = candidates[index];
        if ((remaining & entry.bits) !== entry.bits) continue;
        remaining &= ~entry.bits;
        names.unshift(applyPropertyNamingPolicy(converter.namingPolicy, entry.name));
        if (remaining === 0n) break;
    }
    return remaining === 0n && names.length !== 0 ? names.join(', ') : null;
}

function escapeString(value) {
    let result = '"';
    for (let index = 0; index < value.length; index++) {
        const character = value[index];
        const code = character.charCodeAt(0);
        if (code >= 0xd800 && code <= 0xdbff) {
            const nextCode = index + 1 < value.length ? value.charCodeAt(index + 1) : -1;
            if (nextCode >= 0xdc00 && nextCode <= 0xdfff) {
                result += `\\u${code.toString(16).toUpperCase().padStart(4, '0')}`;
                result += `\\u${nextCode.toString(16).toUpperCase().padStart(4, '0')}`;
                index++;
            } else {
                result += '\\uFFFD';
            }
            continue;
        }
        if (code >= 0xdc00 && code <= 0xdfff) {
            result += '\\uFFFD';
            continue;
        }
        switch (character) {
            case '"': result += '\\u0022'; break;
            case '\\': result += '\\\\'; break;
            case '\b': result += '\\b'; break;
            case '\f': result += '\\f'; break;
            case '\n': result += '\\n'; break;
            case '\r': result += '\\r'; break;
            case '\t': result += '\\t'; break;
            default:
                if (code < 0x20 || code > 0x7e || character === '<' || character === '>' || character === '&' || character === '\'' || character === '+') {
                    result += `\\u${code.toString(16).toUpperCase().padStart(4, '0')}`;
                } else {
                    result += character;
                }
                break;
        }
    }
    return result + '"';
}

function encodeBase64(bytes) {
    const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
    let result = '';
    for (let index = 0; index < bytes.length; index += 3) {
        const first = Number(unwrap(bytes[index]));
        const hasSecond = index + 1 < bytes.length;
        const hasThird = index + 2 < bytes.length;
        const second = hasSecond ? Number(unwrap(bytes[index + 1])) : 0;
        const third = hasThird ? Number(unwrap(bytes[index + 2])) : 0;
        const block = (first << 16) | (second << 8) | third;
        result += alphabet[(block >>> 18) & 63];
        result += alphabet[(block >>> 12) & 63];
        result += hasSecond ? alphabet[(block >>> 6) & 63] : '=';
        result += hasThird ? alphabet[block & 63] : '=';
    }
    return result;
}

function getCharacter(ctx, value) {
    if (typeof value === 'string' && value.length === 1) return value;
    if (Number.isInteger(value) && value >= 0 && value <= 0xffff) {
        return String.fromCharCode(value);
    }
    fail(ctx, 'System.NotSupportedException', 'The managed character representation is invalid.');
}

function shortestSingle(value) {
    const rounded = Math.fround(value);
    for (let precision = 1; precision <= 9; precision++) {
        const candidate = rounded.toPrecision(precision);
        if (Object.is(Math.fround(Number(candidate)), rounded)) return candidate;
    }
    return rounded.toPrecision(9);
}

function formatFiniteFloating(value, typeName, ensureDecimalPlace) {
    const rounded = typeName === 'System.Single' ? Math.fround(value) : value;
    if (Object.is(rounded, -0)) return ensureDecimalPlace ? '-0.0' : '-0';
    if (rounded === 0) return ensureDecimalPlace ? '0.0' : '0';

    let source = typeName === 'System.Single' ? shortestSingle(rounded) : rounded.toString();
    let sign = '';
    if (source.startsWith('-')) {
        sign = '-';
        source = source.slice(1);
    }
    const [mantissa, exponentText] = source.toLowerCase().split('e');
    const explicitExponent = exponentText === undefined ? 0 : Number(exponentText);
    const decimalIndex = mantissa.indexOf('.');
    const integerDigits = decimalIndex < 0 ? mantissa.length : decimalIndex;
    const rawDigits = mantissa.replace('.', '');
    const firstNonZero = rawDigits.search(/[1-9]/);
    const digits = rawDigits.slice(firstNonZero).replace(/0+$/, '') || '0';
    const exponent = explicitExponent + integerDigits - firstNonZero - 1;
    const exponentThreshold = typeName === 'System.Single' ? 9 : 17;

    let result;
    if (exponent < -4 || exponent >= exponentThreshold) {
        const coefficient = digits.length === 1 ? digits : `${digits[0]}.${digits.slice(1)}`;
        const exponentDigits = String(Math.abs(exponent)).padStart(2, '0');
        result = `${coefficient}E${exponent < 0 ? '-' : '+'}${exponentDigits}`;
    } else {
        const decimalPosition = exponent + 1;
        if (decimalPosition <= 0) {
            result = `0.${'0'.repeat(-decimalPosition)}${digits}`;
        } else if (decimalPosition >= digits.length) {
            result = digits + '0'.repeat(decimalPosition - digits.length);
        } else {
            result = `${digits.slice(0, decimalPosition)}.${digits.slice(decimalPosition)}`;
        }
        if (ensureDecimalPlace && !result.includes('.')) result += '.0';
    }
    return sign + result;
}

function isDefaultValue(value, typeName) {
    if (isManagedNull(value)) return true;
    const unwrapped = unwrap(value);
    if (typeName === 'System.Boolean') return unwrapped === false;
    return typeof unwrapped === 'number' ? unwrapped === 0 : typeof unwrapped === 'bigint' && unwrapped === 0n;
}

function isIgnoredReferenceCycle(value, active, options) {
    if (options.referenceHandling !== 1 || isManagedNull(value)) return false;
    const unwrapped = unwrap(value);
    return unwrapped !== null
        && typeof unwrapped === 'object'
        && active.has(unwrapped);
}

function formatContainer(open, close, entries, options, indentationDepth) {
    if (!options.writeIndented || entries.length === 0) {
        return `${open}${entries.join(',')}${close}`;
    }
    const entryIndentation = '  '.repeat(indentationDepth + 1);
    const closingIndentation = '  '.repeat(indentationDepth);
    return `${open}\r\n${entryIndentation}${entries.join(`,\r\n${entryIndentation}`)}\r\n${closingIndentation}${close}`;
}

function serializeValue(
    ctx,
    value,
    declaredType,
    active,
    options,
    numberHandling = options.numberHandling,
    depth = 0,
    indentationDepth = 0) {
    if (depth >= options.maxDepth) {
        fail(ctx, 'System.Text.Json.JsonException',
            `A possible object cycle was detected or the object depth is larger than the maximum allowed depth of ${options.maxDepth}.`);
    }
    if (isManagedNull(value)) return 'null';
    const typeReference = getValueTypeReference(value, declaredType);
    const typeName = getManagedTypeName(typeReference);
    const unwrapped = unwrap(value);
    switch (typeName) {
        case 'System.String': return escapeString(unwrapped);
        case 'System.Char': return escapeString(getCharacter(ctx, unwrapped));
        case 'System.Boolean': return unwrapped ? 'true' : 'false';
        case 'System.Byte':
        case 'System.SByte':
        case 'System.Int16':
        case 'System.UInt16':
        case 'System.Int32':
        case 'System.UInt32':
        case 'System.Int64':
        case 'System.UInt64': {
            const formatted = String(unwrapped);
            return (numberHandling & 2) !== 0 ? escapeString(formatted) : formatted;
        }
        case 'System.Single':
        case 'System.Double': {
            if (!Number.isFinite(unwrapped)) {
                if ((numberHandling & 4) !== 0) {
                    return escapeString(Number.isNaN(unwrapped)
                        ? 'NaN'
                        : unwrapped < 0 ? '-Infinity' : 'Infinity');
                }
                fail(ctx, 'System.ArgumentException', '.NET number values such as positive and negative infinity cannot be written as valid JSON.');
            }
            const formatted = formatFiniteFloating(unwrapped, typeName, false);
            return (numberHandling & 2) !== 0 ? escapeString(formatted) : formatted;
        }
        case 'System.DateTime':
        case 'System.DateTimeOffset':
        case 'System.DateOnly':
        case 'System.TimeOnly':
        case 'System.TimeSpan': {
            const formatted = formatManagedTemporal(typeName, value);
            if (formatted === null) {
                fail(ctx, 'System.ArgumentOutOfRangeException', `The managed ${typeName} value is outside its valid range.`);
            }
            return `"${formatted}"`;
        }
    }
    const enumMetadata = globalThis.DotNet.Types[typeName];
    if (typeof enumMetadata?.enumUnderlyingType === 'string') {
        const converter = getEnumConverter(options, typeName);
        if (converter !== null) {
            const name = formatEnumName(ctx, unwrapped, enumMetadata, converter);
            if (name !== null) return escapeString(name);
            if (!converter.allowNumbers) {
                fail(ctx, 'System.Text.Json.JsonException', 'The JSON value could not be converted to ' + typeName + '.');
            }
        }
        return serializeValue(
            ctx,
            wrap(
                normalizeEnumValue(ctx, unwrapped, enumMetadata.enumUnderlyingType),
                enumMetadata.enumUnderlyingType),
            enumMetadata.enumUnderlyingType,
            active,
            options,
            0,
            depth,
            indentationDepth);
    }
    if (typeName === 'System.Byte[]') {
        if (!Array.isArray(unwrapped)) {
            fail(ctx, 'System.NotSupportedException', `JSON serialization for managed type '${typeName}' is not implemented.`);
        }
        return escapeString(encodeBase64(unwrapped));
    }
    if (typeName.endsWith('[]')) {
        if (!Array.isArray(unwrapped)) {
            fail(ctx, 'System.NotSupportedException', `JSON serialization for managed type '${typeName}' is not implemented.`);
        }
        if (active.has(unwrapped)) {
            if (options.referenceHandling === 1) return 'null';
            fail(ctx, 'System.Text.Json.JsonException', 'A possible object cycle was detected.');
        }
        active.add(unwrapped);
        const elementType = typeName.slice(0, -2);
        const entries = unwrapped.map(element => serializeValue(
            ctx,
            element,
            elementType,
            active,
            options,
            numberHandling,
            depth + 1,
            indentationDepth + 1));
        const result = formatContainer('[', ']', entries, options, indentationDepth);
        active.delete(unwrapped);
        return result;
    }
    const collection = findCollectionContract(typeReference);
    if (collection !== null) {
        if (active.has(unwrapped)) {
            if (options.referenceHandling === 1) return 'null';
            fail(ctx, 'System.Text.Json.JsonException', 'A possible object cycle was detected.');
        }
        active.add(unwrapped);
        let entries;
        if (collection.kind === 'list') {
            const count = unwrap(invokeManagedVirtual(ctx, value, 'get_Count()'));
            entries = [];
            for (let index = 0; index < count; index++) {
                entries.push(serializeValue(
                    ctx,
                    invokeManagedVirtual(ctx, value, 'get_Item(System.Int32)', wrap(index, 'System.Int32')),
                    collection.elementType,
                    active,
                    options,
                    numberHandling,
                    depth + 1,
                    indentationDepth + 1));
            }
            active.delete(unwrapped);
            return formatContainer('[', ']', entries, options, indentationDepth);
        }

        if (getManagedTypeName(collection.keyType) !== 'System.String') {
            fail(ctx, 'System.NotSupportedException', 'Only string dictionary keys are currently implemented.');
        }
        entries = [];
        const enumerator = invokeManagedVirtual(ctx, value, 'GetEnumerator()');
        try {
            while (unwrap(invokeManagedVirtual(ctx, enumerator, 'MoveNext()'))) {
                const current = invokeManagedVirtual(ctx, enumerator, 'get_Current()');
                const key = invokeManagedVirtual(ctx, current, 'get_Key()');
                const dictionaryValue = invokeManagedVirtual(ctx, current, 'get_Value()');
                const keyName = applyPropertyNamingPolicy(options.dictionaryKeyPolicy, unwrap(key));
                entries.push(`${escapeString(keyName)}${options.writeIndented ? ': ' : ':'}${serializeValue(
                    ctx,
                    dictionaryValue,
                    collection.valueType,
                    active,
                    options,
                    numberHandling,
                    depth + 1,
                    indentationDepth + 1)}`);
            }
        } finally {
            const dispose = findManagedVirtualMethod(enumerator, 'Dispose()');
            if (dispose !== null) invokeManagedVirtual(ctx, enumerator, 'Dispose()');
        }
        active.delete(unwrapped);
        return formatContainer('{', '}', entries, options, indentationDepth);
    }
    if (active.has(unwrapped)) {
        if (options.referenceHandling === 1) return 'null';
        fail(ctx, 'System.Text.Json.JsonException', 'A possible object cycle was detected.');
    }
    if (!isManagedObjectContract(globalThis.DotNet.Types[typeName])) {
        fail(ctx, 'System.NotSupportedException', `JSON serialization for managed type '${typeName}' is not implemented.`);
    }

    active.add(unwrapped);
    const contracts = [];
    const jsonNames = new Set();
    const typeNumberHandling = getNumberHandling(
        ctx, globalThis.DotNet.Types[typeName], options.numberHandling);
    for (const property of getProperties(typeName, options)) {
        if (property.indexParameterCount !== 0) continue;
        const included = findAttribute(property, 'System.Text.Json.Serialization.JsonIncludeAttribute') !== null;
        const publiclyVisible = property.getterIsPublic === true || property.setterIsPublic === true;
        if (!publiclyVisible && !included) continue;
        if (!property.getter || property.getterIsPublic !== true && !included) continue;
        const memberCondition = getIgnoreCondition(property);
        const hasSetter = property.setter
            && (property.setterIsPublic === true || included);
        if (options.ignoreReadOnlyProperties && memberCondition === null && !hasSetter) continue;
        const condition = memberCondition
            ?? (options.ignoreNullValues ? 3 : options.defaultIgnoreCondition);
        if (condition === 1) continue;
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
            condition,
            order: getPropertyOrder(property),
            numberHandling: getNumberHandling(ctx, property, typeNumberHandling),
            index: contracts.length
        });
    }
    for (const metadata of getTypeHierarchy(typeName)) {
        for (const field of metadata.fields ?? []) {
            if (field.isStatic) continue;
            const included = findAttribute(field, 'System.Text.Json.Serialization.JsonIncludeAttribute') !== null;
            if (!included && !(options.includeFields && field.isPublic === true)) continue;
            const memberCondition = getIgnoreCondition(field);
            if (options.ignoreReadOnlyFields && memberCondition === null
                && field.isInitOnly === true) continue;
            const condition = memberCondition
                ?? (options.ignoreNullValues ? 3 : options.defaultIgnoreCondition);
            if (condition === 1) continue;
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
                condition,
                order: getPropertyOrder(field),
                numberHandling: getNumberHandling(ctx, field, typeNumberHandling),
                index: contracts.length
            });
        }
    }
    contracts.sort((left, right) => left.order - right.order || left.index - right.index);
    const entries = [];
    const propertySeparator = options.writeIndented ? ': ' : ':';
    for (const contract of contracts) {
        const memberValue = contract.kind === 'property'
            ? getPropertyValue(
                ctx,
                wrap({ metadata: contract.member }, 'System.Reflection.PropertyInfo'),
                value)
            : unwrapped[contract.member.storageKey ?? contract.member.name];
        const ignoredReferenceCycle = isIgnoredReferenceCycle(
            memberValue,
            active,
            options);
        if (contract.condition === 3
            && (isManagedNull(memberValue) || ignoredReferenceCycle)) continue;
        if (contract.condition === 2
            && (isDefaultValue(memberValue, contract.member.typeName)
                || ignoredReferenceCycle)) continue;
        entries.push(`${escapeString(contract.jsonName)}${propertySeparator}${serializeValue(
            ctx,
            memberValue,
            contract.member,
            active,
            options,
            contract.numberHandling,
            depth + 1,
            indentationDepth + 1)}`);
    }
    active.delete(unwrapped);
    return formatContainer('{', '}', entries, options, indentationDepth);
}

globalThis.DotNet.Methods['System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, System.Text.Json.JsonSerializerOptions)'] = function (ctx, value, options) {
    const serializerOptions = getSerializerOptions(ctx, options);
    const typeReference = ctx.genericParams?.TValue ?? value?.t;
    if (typeReference === null || typeReference === undefined) fail(ctx, 'System.NotSupportedException', 'The JSON source type could not be resolved.');
    return wrap(serializeValue(ctx, value, typeReference, new Set(), serializerOptions), 'System.String');
};
