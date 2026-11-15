import { throwManagedException } from '../managed-exceptions.js';
import { formatManagedTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { getPropertyValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
import { getDefaultValue, getManagedTypeArguments, getManagedTypeName, isManagedNull, resolveManagedTypeReference, unwrap, wrap } from '../msil-type-helpers.js';

const attributedDefaultIntegralBounds = Object.freeze({
    'System.SByte': [-128n, 127n],
    'System.Byte': [0n, 255n],
    'System.Int16': [-32768n, 32767n],
    'System.UInt16': [0n, 65535n],
    'System.Int32': [-2147483648n, 2147483647n],
    'System.UInt32': [0n, 4294967295n],
    'System.Int64': [-9223372036854775808n, 9223372036854775807n],
    'System.UInt64': [0n, 18446744073709551615n]
});
const ignoredReferenceLoop = Symbol('Newtonsoft.Json ignored reference loop');

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

function getTypeHierarchyEntries(typeName) {
    const hierarchy = [];
    for (let current = typeName; typeof current === 'string'; current = getBaseTypeName(current)) {
        const metadata = globalThis.DotNet.Types[current];
        if (!metadata) break;
        hierarchy.push({ typeName: current, metadata });
    }
    return hierarchy;
}

function getTypeHierarchy(typeName) {
    return getTypeHierarchyEntries(typeName).map(entry => entry.metadata);
}

function findAttribute(member, typeName) {
    return (member.attributes ?? []).find(attribute => attribute.typeName === typeName) ?? null;
}

function getJsonObjectAttribute(typeName) {
    const metadata = getTypeHierarchy(typeName).find(candidate =>
        findAttribute(candidate, 'Newtonsoft.Json.JsonObjectAttribute'));
    return metadata === undefined
        ? null
        : findAttribute(metadata, 'Newtonsoft.Json.JsonObjectAttribute');
}

function getMemberSerialization(ctx, typeName) {
    const attribute = getJsonObjectAttribute(typeName);
    if (!attribute) return 0;

    const named = attribute.namedArguments ?? {};
    const unsupported = Object.keys(named).filter(
        name => name !== 'MissingMemberHandling'
            && name !== 'ItemRequired'
            && name !== 'ItemNullValueHandling'
            && name !== 'Id'
            && name !== 'Title'
            && name !== 'Description'
            && name !== 'IsReference'
            && name !== 'ItemIsReference'
            && name !== 'ItemReferenceLoopHandling'
            && name !== 'ItemTypeNameHandling'
            && name !== 'NamingStrategyType'
            && name !== 'NamingStrategyParameters');
    if (unsupported.length !== 0) {
        fail(ctx, 'System.NotSupportedException', `JsonObject setting '${unsupported[0]}' is not implemented.`);
    }
    validateDefaultJsonObjectContainerSettings(ctx, named);
    getNamingStrategy(ctx, named);
    const missingMemberHandling = named.MissingMemberHandling ?? 0;
    if (missingMemberHandling !== 0 && missingMemberHandling !== 1) {
        fail(ctx, 'System.NotSupportedException', `JsonObject MissingMemberHandling value '${missingMemberHandling}' is not implemented.`);
    }

    const mode = (attribute.constructorArguments ?? []).find(argument =>
        typeof argument === 'number') ?? 0;
    if (mode === 2) {
        fail(ctx, 'System.NotSupportedException', 'JsonObject MemberSerialization.Fields is not implemented.');
    }
    if (mode !== 0 && mode !== 1) {
        fail(ctx, 'System.NotSupportedException', `JsonObject MemberSerialization value '${mode}' is not implemented.`);
    }
    return mode;
}

function validateDefaultJsonObjectContainerSettings(ctx, named) {
    if (Object.prototype.hasOwnProperty.call(named, 'IsReference')
        && named.IsReference !== false && named.IsReference !== 0) {
        fail(ctx, 'System.NotSupportedException', 'JsonObject IsReference=true is not implemented.');
    }
    if (Object.prototype.hasOwnProperty.call(named, 'ItemIsReference')
        && named.ItemIsReference !== false && named.ItemIsReference !== 0) {
        fail(ctx, 'System.NotSupportedException', 'JsonObject ItemIsReference=true is not implemented.');
    }
    if ((named.ItemReferenceLoopHandling ?? 0) !== 0) {
        fail(ctx, 'System.NotSupportedException', `JsonObject ItemReferenceLoopHandling value '${named.ItemReferenceLoopHandling}' is not implemented.`);
    }
    if ((named.ItemTypeNameHandling ?? 0) !== 0) {
        fail(ctx, 'System.NotSupportedException', `JsonObject ItemTypeNameHandling value '${named.ItemTypeNameHandling}' is not implemented.`);
    }
}

function getItemRequired(ctx, typeName) {
    const required = getJsonObjectAttribute(typeName)?.namedArguments?.ItemRequired ?? 0;
    if (!Number.isInteger(required) || required < 0 || required > 3) {
        fail(ctx, 'System.NotSupportedException', `JsonObject ItemRequired value '${required}' is not implemented.`);
    }
    return required;
}

function getItemNullValueHandling(ctx, typeName) {
    const handling = getJsonObjectAttribute(typeName)
        ?.namedArguments?.ItemNullValueHandling ?? 0;
    if (handling !== 0 && handling !== 1) {
        fail(ctx, 'System.NotSupportedException', `JsonObject ItemNullValueHandling value '${handling}' is not implemented.`);
    }
    return handling;
}

function getNamingStrategyTypeName(ctx, value) {
    if (typeof value !== 'string' || value.length === 0) {
        fail(ctx, 'System.NotSupportedException', 'The naming strategy type metadata is invalid.');
    }
    return value.split(',')[0].trim();
}

function getNamingStrategyParameters(ctx, value) {
    if (value === null || value === undefined) return [];
    if (!Array.isArray(value)) {
        fail(ctx, 'System.NotSupportedException', 'The naming strategy parameters metadata is invalid.');
    }
    return value.map(parameter => {
        if (typeof parameter === 'boolean') return parameter;
        if (parameter !== null && typeof parameter === 'object'
            && typeof parameter.ArgumentType === 'string'
            && parameter.ArgumentType.split(',')[0].trim() === 'System.Boolean'
            && typeof parameter.Value === 'boolean') {
            return parameter.Value;
        }
        fail(ctx, 'System.NotSupportedException', 'Only Boolean naming strategy parameters are implemented.');
    });
}

function getNamingStrategy(ctx, named) {
    const strategyType = named?.NamingStrategyType;
    if (strategyType === null || strategyType === undefined) return null;

    const typeName = getNamingStrategyTypeName(ctx, strategyType);
    const parameters = getNamingStrategyParameters(ctx, named.NamingStrategyParameters);
    const defaultType = 'Newtonsoft.Json.Serialization.DefaultNamingStrategy';
    const builtInTypes = new Set([
        'Newtonsoft.Json.Serialization.CamelCaseNamingStrategy',
        'Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy',
        'Newtonsoft.Json.Serialization.KebabCaseNamingStrategy'
    ]);
    if (typeName === defaultType) {
        if (parameters.length !== 0) {
            fail(ctx, 'System.NotSupportedException', `Naming strategy '${typeName}' does not support constructor parameters.`);
        }
        return { kind: 'default', overrideSpecifiedNames: false };
    }
    if (!builtInTypes.has(typeName)) {
        fail(ctx, 'System.NotSupportedException', `Naming strategy '${typeName}' is not implemented.`);
    }
    if (parameters.length !== 0 && parameters.length !== 2 && parameters.length !== 3) {
        fail(ctx, 'System.NotSupportedException', `Naming strategy '${typeName}' constructor parameters are not implemented.`);
    }
    return {
        kind: typeName.slice(typeName.lastIndexOf('.') + 1),
        overrideSpecifiedNames: parameters[1] ?? false
    };
}

function getContainerNamingStrategy(ctx, typeName) {
    return getNamingStrategy(ctx, getJsonObjectAttribute(typeName)?.namedArguments);
}

function isUpperCharacter(character) {
    return /^\p{Lu}$/u.test(character);
}

function isSeparatorCharacter(character) {
    return /^\p{Z}$/u.test(character);
}

function toLowerInvariantCharacter(character) {
    const lowered = character.toLowerCase();
    return lowered.length === 1 ? lowered : character;
}

function toCamelCase(value) {
    if (value.length === 0 || !isUpperCharacter(value[0])) return value;
    const characters = value.split('');
    for (let index = 0; index < characters.length; index++) {
        if (index === 1 && !isUpperCharacter(characters[index])) break;
        const hasNext = index + 1 < characters.length;
        if (index > 0 && hasNext && !isUpperCharacter(characters[index + 1])) {
            if (isSeparatorCharacter(characters[index + 1])) {
                characters[index] = toLowerInvariantCharacter(characters[index]);
            }
            break;
        }
        characters[index] = toLowerInvariantCharacter(characters[index]);
    }
    return characters.join('');
}

function toSeparatedCase(value, separator) {
    if (value.length === 0) return value;
    let result = '';
    let state = 'start';
    for (let index = 0; index < value.length; index++) {
        const character = value[index];
        if (character === ' ') {
            if (state !== 'start') state = 'newWord';
        } else if (isUpperCharacter(character)) {
            if (state === 'upper') {
                const hasNext = index + 1 < value.length;
                if (index > 0 && hasNext
                    && !isUpperCharacter(value[index + 1])
                    && value[index + 1] !== separator) {
                    result += separator;
                }
            } else if (state === 'lower' || state === 'newWord') {
                result += separator;
            }
            result += toLowerInvariantCharacter(character);
            state = 'upper';
        } else if (character === separator) {
            result += separator;
            state = 'start';
        } else {
            if (state === 'newWord') result += separator;
            result += character;
            state = 'lower';
        }
    }
    return result;
}

function applyNamingStrategy(strategy, name, hasSpecifiedName) {
    if (!strategy || (hasSpecifiedName && !strategy.overrideSpecifiedNames)) return name;
    switch (strategy.kind) {
        case 'default': return name;
        case 'CamelCaseNamingStrategy': return toCamelCase(name);
        case 'SnakeCaseNamingStrategy': return toSeparatedCase(name, '_');
        case 'KebabCaseNamingStrategy': return toSeparatedCase(name, '-');
        default: return name;
    }
}

function getJsonName(ctx, member, containerStrategy = null) {
    const attribute = findAttribute(member, 'Newtonsoft.Json.JsonPropertyAttribute');
    const named = attribute?.namedArguments ?? {};
    const unsupported = Object.keys(named).filter(
        name => name !== 'PropertyName' && name !== 'Order' && name !== 'Required'
            && name !== 'NullValueHandling' && name !== 'DefaultValueHandling'
            && name !== 'ReferenceLoopHandling'
            && name !== 'NamingStrategyType' && name !== 'NamingStrategyParameters');
    if (unsupported.length !== 0) {
        fail(ctx, 'System.NotSupportedException', `JsonProperty setting '${unsupported[0]}' is not implemented.`);
    }
    const specifiedName = named.PropertyName ?? attribute?.constructorArguments?.[0];
    const hasSpecifiedName = specifiedName !== null && specifiedName !== undefined;
    const strategy = named.NamingStrategyType === null || named.NamingStrategyType === undefined
        ? containerStrategy
        : getNamingStrategy(ctx, named);
    return applyNamingStrategy(strategy, hasSpecifiedName ? specifiedName : member.name, hasSpecifiedName);
}

function getJsonOrder(member) {
    const attribute = findAttribute(member, 'Newtonsoft.Json.JsonPropertyAttribute');
    return attribute?.namedArguments?.Order ?? -1;
}

function getRequiredMode(ctx, member, itemRequired = 0) {
    let required;
    if (findAttribute(member, 'Newtonsoft.Json.JsonRequiredAttribute')) {
        required = 2;
    } else {
        const named = findAttribute(member, 'Newtonsoft.Json.JsonPropertyAttribute')
            ?.namedArguments ?? {};
        required = Object.prototype.hasOwnProperty.call(named, 'Required')
            ? named.Required
            : itemRequired;
    }
    if (!Number.isInteger(required) || required < 0 || required > 3) {
        fail(ctx, 'System.NotSupportedException', `Required value '${required}' is not implemented.`);
    }
    return required;
}

function getNullValueHandling(ctx, member, itemNullValueHandling = 0) {
    const named = findAttribute(member, 'Newtonsoft.Json.JsonPropertyAttribute')
        ?.namedArguments ?? {};
    const value = Object.prototype.hasOwnProperty.call(named, 'NullValueHandling')
        ? named.NullValueHandling
        : itemNullValueHandling;
    if (value !== 0 && value !== 1) {
        fail(ctx, 'System.NotSupportedException', `NullValueHandling value '${value}' is not implemented.`);
    }
    return value;
}

function getDefaultValueHandling(ctx, member) {
    const value = findAttribute(member, 'Newtonsoft.Json.JsonPropertyAttribute')
        ?.namedArguments?.DefaultValueHandling ?? 0;
    if (!Number.isInteger(value) || value < 0 || value > 3) {
        fail(ctx, 'System.NotSupportedException', `DefaultValueHandling value '${value}' is not implemented.`);
    }
    return value;
}

function getReferenceLoopHandling(ctx, member) {
    const value = findAttribute(member, 'Newtonsoft.Json.JsonPropertyAttribute')
        ?.namedArguments?.ReferenceLoopHandling ?? 0;
    if (value !== 0 && value !== 1 && value !== 2) {
        fail(ctx, 'System.NotSupportedException', `ReferenceLoopHandling value '${value}' is not implemented.`);
    }
    return value;
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

function createAttributedDefaultValue(ctx, member, value) {
    const typeName = member.typeName;
    if (value === null) {
        if (typeName === 'System.String' || typeName.endsWith('[]')
            || globalThis.DotNet.Types[typeName]?.isClass === true) return null;
        fail(ctx, 'System.NotSupportedException', `A null DefaultValueAttribute is not valid for '${typeName}'.`);
    }
    if (typeName === 'System.String' && typeof value === 'string') return wrap(value, typeName);
    if (typeName === 'System.Boolean' && typeof value === 'boolean') return wrap(value, typeName);
    if (typeName === 'System.Char' && typeof value === 'string' && value.length === 1) {
        return wrap(value.charCodeAt(0), typeName);
    }
    if (typeName === 'System.Single' && typeof value === 'number') return wrap(Math.fround(value), typeName);
    if (typeName === 'System.Double' && typeof value === 'number') return wrap(value, typeName);

    const bounds = attributedDefaultIntegralBounds[typeName];
    if (bounds && (typeof value === 'bigint' || (typeof value === 'number' && Number.isInteger(value)))) {
        const integer = BigInt(value);
        if (integer >= bounds[0] && integer <= bounds[1]) {
            return wrap(typeName === 'System.Int64' || typeName === 'System.UInt64'
                ? integer
                : Number(integer), typeName);
        }
    }
    fail(ctx, 'System.NotSupportedException', `DefaultValueAttribute value for '${typeName}' is not implemented.`);
}

function getMemberDefaultValue(ctx, member) {
    const attribute = findAttribute(member, 'System.ComponentModel.DefaultValueAttribute');
    if (!attribute) return getDefaultValue(member.typeName);
    const arguments_ = attribute.constructorArguments ?? [];
    if (arguments_.length !== 1) {
        fail(ctx, 'System.NotSupportedException', 'This DefaultValueAttribute constructor is not implemented.');
    }
    return createAttributedDefaultValue(ctx, member, arguments_[0]);
}

function isMemberDefaultValue(ctx, value, member) {
    const expected = getMemberDefaultValue(ctx, member);
    if (isManagedNull(value) || isManagedNull(expected)) {
        return isManagedNull(value) && isManagedNull(expected);
    }
    const actualValue = unwrap(value);
    const expectedValue = unwrap(expected);
    if (member.typeName === 'System.Boolean') {
        return actualValue === expectedValue || actualValue === (expectedValue ? 1 : 0);
    }
    return actualValue === expectedValue
        || (typeof actualValue === 'number' && typeof expectedValue === 'number'
            && Number.isNaN(actualValue) && Number.isNaN(expectedValue));
}

function requireUniqueJsonNames(ctx, contracts) {
    const names = new Set();
    for (const contract of contracts) {
        if (names.has(contract.jsonName)) {
            fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `A member with the name '${contract.jsonName}' already exists.`);
        }
        names.add(contract.jsonName);
    }
    return contracts;
}

function getContracts(ctx, typeName) {
    const optIn = getMemberSerialization(ctx, typeName) === 1;
    const propertyGroups = new Map();
    const fieldGroups = new Map();
    function add(groups, member, contract) {
        let group = groups.get(member.name);
        if (!group) {
            group = { jsonNames: new Set(), contracts: [] };
            groups.set(member.name, group);
        }
        if (group.jsonNames.has(contract.jsonName)) return;
        group.jsonNames.add(contract.jsonName);
        group.contracts.push(contract);
    }
    const hierarchy = getTypeHierarchyEntries(typeName);
    for (const entry of hierarchy) {
        const { metadata } = entry;
        const containerStrategy = getContainerNamingStrategy(ctx, entry.typeName);
        for (const property of metadata.properties ?? []) {
            if (property.indexParameterCount !== 0
                || findAttribute(property, 'Newtonsoft.Json.JsonIgnoreAttribute')) continue;
            const attributed = findAttribute(property, 'Newtonsoft.Json.JsonPropertyAttribute') !== null;
            const publiclyVisible = property.getterIsPublic === true || property.setterIsPublic === true;
            if (((!optIn && publiclyVisible) || attributed) && property.getter) {
                const jsonName = getJsonName(ctx, property, containerStrategy);
                add(propertyGroups, property, {
                    kind: 'property',
                    member: property,
                    jsonName,
                    order: getJsonOrder(property)
                });
            }
        }
        for (const field of metadata.fields ?? []) {
            if (field.isStatic || findAttribute(field, 'Newtonsoft.Json.JsonIgnoreAttribute')) continue;
            const attributed = findAttribute(field, 'Newtonsoft.Json.JsonPropertyAttribute') !== null;
            if ((!optIn && field.isPublic === true) || attributed) {
                const jsonName = getJsonName(ctx, field, containerStrategy);
                add(fieldGroups, field, {
                    kind: 'field',
                    member: field,
                    jsonName,
                    order: getJsonOrder(field)
                });
            }
        }
    }
    const contracts = [
        ...[...fieldGroups.values()].flatMap(group => group.contracts),
        ...[...propertyGroups.values()].flatMap(group => group.contracts)
    ];
    return requireUniqueJsonNames(ctx, contracts)
        .map((contract, index) => ({ ...contract, index }))
        .sort((left, right) => left.order - right.order || left.index - right.index);
}

function escapeString(value) {
    let result = '"';
    for (let index = 0; index < value.length; index++) {
        const character = value[index];
        const code = character.charCodeAt(0);
        switch (character) {
            case '"': result += '\\"'; break;
            case '\\': result += '\\\\'; break;
            case '\b': result += '\\b'; break;
            case '\f': result += '\\f'; break;
            case '\n': result += '\\n'; break;
            case '\r': result += '\\r'; break;
            case '\t': result += '\\t'; break;
            default:
                result += code < 0x20 || code === 0x85 || code === 0x2028 || code === 0x2029
                    ? `\\u${code.toString(16).padStart(4, '0')}`
                    : character;
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

function handleReferenceLoop(ctx, handling) {
    if (handling === 1) return ignoredReferenceLoop;
    if (handling === 2) {
        fail(ctx, 'System.NotSupportedException', 'ReferenceLoopHandling.Serialize is not implemented.');
    }
    fail(ctx, 'Newtonsoft.Json.JsonSerializationException', 'Self referencing loop detected.');
}

function serializeValue(ctx, value, declaredType, active, referenceLoopHandling = 0) {
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
        case 'System.UInt64': return String(unwrapped);
        case 'System.Single':
        case 'System.Double':
            if (Number.isNaN(unwrapped)) return escapeString('NaN');
            if (unwrapped === Number.POSITIVE_INFINITY) return escapeString('Infinity');
            if (unwrapped === Number.NEGATIVE_INFINITY) return escapeString('-Infinity');
            return formatFiniteFloating(unwrapped, typeName, true);
        case 'System.DateTime':
        case 'System.DateTimeOffset':
        case 'System.DateOnly':
        case 'System.TimeOnly':
        case 'System.TimeSpan': {
            const formatted = formatManagedTemporal(typeName, value);
            if (formatted === null) {
                fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error getting value from '${typeName}'.`);
            }
            return escapeString(formatted);
        }
    }
    const enumMetadata = globalThis.DotNet.Types[typeName];
    if (typeof enumMetadata?.enumUnderlyingType === 'string') {
        return serializeValue(
            ctx,
            wrap(
                normalizeEnumValue(ctx, unwrapped, enumMetadata.enumUnderlyingType),
                enumMetadata.enumUnderlyingType),
            enumMetadata.enumUnderlyingType,
            active,
            referenceLoopHandling);
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
            return handleReferenceLoop(ctx, referenceLoopHandling);
        }
        active.add(unwrapped);
        const elementType = typeName.slice(0, -2);
        const entries = [];
        for (const element of unwrapped) {
            const serialized = serializeValue(
                ctx,
                element,
                elementType,
                active,
                referenceLoopHandling);
            if (serialized !== ignoredReferenceLoop) entries.push(serialized);
        }
        const result = `[${entries.join(',')}]`;
        active.delete(unwrapped);
        return result;
    }
    const collection = findCollectionContract(typeReference);
    if (collection !== null) {
        if (active.has(unwrapped)) {
            return handleReferenceLoop(ctx, referenceLoopHandling);
        }
        active.add(unwrapped);
        const entries = [];
        if (collection.kind === 'list') {
            const count = unwrap(invokeManagedVirtual(ctx, value, 'get_Count()'));
            for (let index = 0; index < count; index++) {
                const serialized = serializeValue(
                    ctx,
                    invokeManagedVirtual(ctx, value, 'get_Item(System.Int32)', wrap(index, 'System.Int32')),
                    collection.elementType,
                    active,
                    referenceLoopHandling);
                if (serialized !== ignoredReferenceLoop) entries.push(serialized);
            }
            active.delete(unwrapped);
            return `[${entries.join(',')}]`;
        }

        if (getManagedTypeName(collection.keyType) !== 'System.String') {
            fail(ctx, 'System.NotSupportedException', 'Only string dictionary keys are currently implemented.');
        }
        const enumerator = invokeManagedVirtual(ctx, value, 'GetEnumerator()');
        try {
            while (unwrap(invokeManagedVirtual(ctx, enumerator, 'MoveNext()'))) {
                const current = invokeManagedVirtual(ctx, enumerator, 'get_Current()');
                const key = invokeManagedVirtual(ctx, current, 'get_Key()');
                const dictionaryValue = invokeManagedVirtual(ctx, current, 'get_Value()');
                const serialized = serializeValue(
                    ctx,
                    dictionaryValue,
                    collection.valueType,
                    active,
                    referenceLoopHandling);
                if (serialized !== ignoredReferenceLoop) {
                    entries.push(`${escapeString(unwrap(key))}:${serialized}`);
                }
            }
        } finally {
            const dispose = findManagedVirtualMethod(enumerator, 'Dispose()');
            if (dispose !== null) invokeManagedVirtual(ctx, enumerator, 'Dispose()');
        }
        active.delete(unwrapped);
        return `{${entries.join(',')}}`;
    }
    if (active.has(unwrapped)) {
        return handleReferenceLoop(ctx, referenceLoopHandling);
    }
    if (!isManagedObjectContract(globalThis.DotNet.Types[typeName])) {
        fail(ctx, 'System.NotSupportedException', `JSON serialization for managed type '${typeName}' is not implemented.`);
    }

    active.add(unwrapped);
    const entries = [];
    const itemRequired = getItemRequired(ctx, typeName);
    const itemNullValueHandling = getItemNullValueHandling(ctx, typeName);
    for (const contract of getContracts(ctx, typeName)) {
        const memberValue = contract.kind === 'property'
            ? getPropertyValue(ctx, wrap({ metadata: contract.member }, 'System.Reflection.PropertyInfo'), value)
            : unwrapped[contract.member.storageKey ?? contract.member.name];
        if (isManagedNull(memberValue)
            && getNullValueHandling(ctx, contract.member, itemNullValueHandling) === 1) continue;
        if ((getDefaultValueHandling(ctx, contract.member) & 1) !== 0
            && isMemberDefaultValue(ctx, memberValue, contract.member)) continue;
        const required = getRequiredMode(ctx, contract.member, itemRequired);
        if (isManagedNull(memberValue) && (required === 2 || required === 3)) {
            fail(
                ctx,
                'Newtonsoft.Json.JsonSerializationException',
                `Cannot write a null value for property '${contract.jsonName}'. Property requires a non-null value.`);
        }
        const serialized = serializeValue(
            ctx,
            memberValue,
            contract.member,
            active,
            getReferenceLoopHandling(ctx, contract.member));
        if (serialized === ignoredReferenceLoop) continue;
        entries.push(`${escapeString(contract.jsonName)}:${serialized}`);
    }
    active.delete(unwrapped);
    return `{${entries.join(',')}}`;
}

globalThis.DotNet.Methods['Newtonsoft.Json.JsonConvert.SerializeObject(System.Object)'] = function (ctx, value) {
    if (isManagedNull(value)) return wrap('null', 'System.String');
    if (value?.t === 'System.Object'
        && value.v !== null
        && typeof value.v === 'object'
        && Object.prototype.hasOwnProperty.call(value.v, 'v')) {
        const boxedTypeName = typeof value.v.t === 'string'
            ? value.v.t
            : value.v.t?.typeName;
        if (typeof boxedTypeName === 'string') {
            value = wrap(value.v.v, boxedTypeName, value.v.$typeArgs);
        }
    }
    const typeName = value?.t;
    if (typeof typeName !== 'string') fail(ctx, 'System.NotSupportedException', 'The JSON source type could not be resolved.');
    const typeReference = value?.$typeArgs
        ? { typeName, genericParams: value.$typeArgs }
        : typeName;
    return wrap(serializeValue(ctx, value, typeReference, new Set()), 'System.String');
};
