import { throwManagedException } from '../managed-exceptions.js';
import { parseManagedTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { initializeObject } from '../msil.js';
import { setPropertyValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
import { getDefaultValue, getManagedTypeArguments, getManagedTypeName, isManagedNull, resolveManagedTypeReference, unwrap, wrap } from '../msil-type-helpers.js';

const exactNumber = Symbol('Newtonsoft.Json exact number');
const sourceIndexFailure = Symbol('Newtonsoft.Json source index failure');
const sourceDepthFailure = Symbol('Newtonsoft.Json source depth failure');
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

function sourcePosition(source, index) {
    let lineNumber = 1;
    let lineStart = 0;
    for (let current = 0; current < index; current++) {
        if (source[current] === '\n') {
            lineNumber++;
            lineStart = current + 1;
        }
    }
    return { lineNumber, linePosition: index - lineStart };
}

function exceptionFields(source, path, index) {
    const position = sourcePosition(source, index);
    return {
        '<Path>k__BackingField': wrap(path, 'System.String'),
        '<LineNumber>k__BackingField': wrap(position.lineNumber, 'System.Int32'),
        '<LinePosition>k__BackingField': wrap(position.linePosition, 'System.Int32')
    };
}

function readerFail(ctx, typeName, numberSource, path, source, locations) {
    const location = locations.get(path);
    if (!location) {
        fail(ctx, 'System.NotSupportedException', `The JSON source location for '${path}' could not be resolved.`);
    }
    throwManagedException(
        ctx,
        'Newtonsoft.Json.JsonReaderException',
        `JSON integer ${numberSource} is too large or small for ${typeName}.`,
        null,
        exceptionFields(source, path, location.end));
}

function appendPath(path, name) {
    if (/^[A-Za-z_][A-Za-z0-9_]*$/.test(name)) return path ? `${path}.${name}` : name;
    return `${path}['${name.replaceAll('\\', '\\\\').replaceAll("'", "\\'")}']`;
}

function createSourceIndex(source, maxDepth) {
    const locations = new Map();
    const namedFloatingValues = new Map();
    const namedFloatingTokens = [];
    let index = 0;
    let error = null;
    let depthError = null;
    let additional = null;

    function reject(path, failureIndex = index) {
        error ??= { path, index: failureIndex };
        throw sourceIndexFailure;
    }

    function rejectDepth(path, failureIndex) {
        depthError ??= { path, index: failureIndex };
        throw sourceDepthFailure;
    }

    function skipWhitespace() {
        while (index < source.length
            && (source[index] === ' ' || source[index] === '\t'
                || source[index] === '\r' || source[index] === '\n')) index++;
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

    function parseNamedFloating(path, literal, replacement) {
        const start = index;
        parseLiteral(path, literal);
        namedFloatingValues.set(path, literal);
        namedFloatingTokens.push({ start, end: index, replacement });
    }

    function parseArray(path, depth) {
        const start = index++;
        skipWhitespace();
        if (source[index] === ']') {
            index++;
            locations.set(path, { start, end: index });
            return;
        }
        let elementIndex = 0;
        while (true) {
            parseValue(`${path}[${elementIndex++}]`, depth);
            skipWhitespace();
            if (source[index] === ']') {
                index++;
                locations.set(path, { start, end: index });
                return;
            }
            if (source[index] !== ',') reject(path, index);
            index++;
            skipWhitespace();
        }
    }

    function parseObject(path, depth) {
        const start = index++;
        skipWhitespace();
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
            parseValue(memberPath, depth);
            skipWhitespace();
            if (source[index] === '}') {
                index++;
                locations.set(path, { start, end: index });
                return;
            }
            if (source[index] !== ',') reject(memberPath, index);
            index++;
            skipWhitespace();
        }
    }

    function parseValue(path, depth) {
        namedFloatingValues.delete(path);
        skipWhitespace();
        switch (source[index]) {
            case '{':
                if (maxDepth !== null && depth >= maxDepth) {
                    rejectDepth(path, index + 1);
                }
                parseObject(path, depth + 1);
                return;
            case '[':
                if (maxDepth !== null && depth >= maxDepth) {
                    rejectDepth(path, index + 1);
                }
                parseArray(path, depth + 1);
                return;
            case '"': parseString(path, true); return;
            case 't': parseLiteral(path, 'true'); return;
            case 'f': parseLiteral(path, 'false'); return;
            case 'n': parseLiteral(path, 'null'); return;
            case 'N': parseNamedFloating(path, 'NaN', '0e0'); return;
            case 'I': parseNamedFloating(path, 'Infinity', '1e999999'); return;
            default:
                if (source.startsWith('-Infinity', index)) {
                    parseNamedFloating(path, '-Infinity', '-1e999999');
                    return;
                }
                if (source[index] === '-' || /[0-9]/.test(source[index] ?? '')) {
                    parseNumber(path);
                    return;
                }
                reject(path, index);
        }
    }

    try {
        parseValue('', 0);
        skipWhitespace();
        if (index !== source.length) additional = { path: '', index };
    } catch (failure) {
        if (failure !== sourceIndexFailure && failure !== sourceDepthFailure) throw failure;
    }
    return {
        locations,
        error,
        depthError,
        additional,
        namedFloatingValues,
        namedFloatingTokens
    };
}

function normalizeNamedFloatingTokens(source, tokens) {
    if (tokens.length === 0) return source;
    let normalized = '';
    let sourceIndex = 0;
    for (const token of tokens) {
        normalized += source.slice(sourceIndex, token.start) + token.replacement;
        sourceIndex = token.end;
    }
    return normalized + source.slice(sourceIndex);
}

function restoreNamedFloatingValues(value, path, namedFloatingValues) {
    if (namedFloatingValues.has(path)) return namedFloatingValues.get(path);
    if (Array.isArray(value)) {
        return value.map((element, index) => restoreNamedFloatingValues(
            element, `${path}[${index}]`, namedFloatingValues));
    }
    if (value === null || typeof value !== 'object' || value?.[exactNumber] !== undefined) {
        return value;
    }
    for (const [name, memberValue] of Object.entries(value)) {
        value[name] = restoreNamedFloatingValues(
            memberValue, appendPath(path, name), namedFloatingValues);
    }
    return value;
}

function getTargetType(ctx) {
    const typeReference = ctx.genericParams?.T;
    if (typeReference === null || typeReference === undefined) {
        fail(ctx, 'System.NotSupportedException', 'The JSON target type could not be resolved.');
    }
    return resolveManagedTypeReference(typeReference);
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

function getMissingMemberHandling(ctx, typeName) {
    const handling = getJsonObjectAttribute(typeName)
        ?.namedArguments?.MissingMemberHandling ?? 0;
    if (handling !== 0 && handling !== 1) {
        fail(ctx, 'System.NotSupportedException', `JsonObject MissingMemberHandling value '${handling}' is not implemented.`);
    }
    return handling;
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

function missingMemberFail(ctx, typeName, name, path, sourceText, locations) {
    const memberPath = appendPath(path, name);
    const location = locations.get(memberPath);
    if (!location) {
        fail(ctx, 'System.NotSupportedException', `The JSON source location for '${memberPath}' could not be resolved.`);
    }
    const simpleTypeName = typeName.slice(Math.max(typeName.lastIndexOf('.'), typeName.lastIndexOf('+')) + 1);
    const position = sourcePosition(sourceText, location.start);
    throwManagedException(
        ctx,
        'Newtonsoft.Json.JsonSerializationException',
        `Could not find member '${name}' on object of type '${simpleTypeName}'. Path '${memberPath}', line ${position.lineNumber}, position ${position.linePosition}.`,
        null,
        exceptionFields(sourceText, memberPath, location.start));
}

function requireSupportedJsonProperty(ctx, member) {
    const attribute = findAttribute(member, 'Newtonsoft.Json.JsonPropertyAttribute');
    if (!attribute) return null;
    const named = attribute.namedArguments ?? {};
    const unsupported = Object.keys(named).filter(
        name => name !== 'PropertyName' && name !== 'Order' && name !== 'Required'
            && name !== 'NullValueHandling' && name !== 'DefaultValueHandling'
            && name !== 'ReferenceLoopHandling'
            && name !== 'NamingStrategyType' && name !== 'NamingStrategyParameters');
    if (unsupported.length !== 0) {
        fail(ctx, 'System.NotSupportedException', `JsonProperty setting '${unsupported[0]}' is not implemented.`);
    }
    return attribute;
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
    const attribute = requireSupportedJsonProperty(ctx, member);
    const named = attribute?.namedArguments ?? {};
    const specifiedName = named.PropertyName ?? attribute?.constructorArguments?.[0];
    const hasSpecifiedName = specifiedName !== null && specifiedName !== undefined;
    const strategy = named.NamingStrategyType === null || named.NamingStrategyType === undefined
        ? containerStrategy
        : getNamingStrategy(ctx, named);
    return applyNamingStrategy(strategy, hasSpecifiedName ? specifiedName : member.name, hasSpecifiedName);
}

function getRequiredMode(ctx, member, itemRequired = 0) {
    let required;
    if (findAttribute(member, 'Newtonsoft.Json.JsonRequiredAttribute')) {
        required = 2;
    } else {
        const named = requireSupportedJsonProperty(ctx, member)?.namedArguments ?? {};
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
    const named = requireSupportedJsonProperty(ctx, member)?.namedArguments ?? {};
    const value = Object.prototype.hasOwnProperty.call(named, 'NullValueHandling')
        ? named.NullValueHandling
        : itemNullValueHandling;
    if (value !== 0 && value !== 1) {
        fail(ctx, 'System.NotSupportedException', `NullValueHandling value '${value}' is not implemented.`);
    }
    return value;
}

function getDefaultValueHandling(ctx, member) {
    const value = requireSupportedJsonProperty(ctx, member)?.namedArguments?.DefaultValueHandling ?? 0;
    if (!Number.isInteger(value) || value < 0 || value > 3) {
        fail(ctx, 'System.NotSupportedException', `DefaultValueHandling value '${value}' is not implemented.`);
    }
    return value;
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
    for (const entry of getTypeHierarchyEntries(typeName)) {
        const { metadata } = entry;
        const containerStrategy = getContainerNamingStrategy(ctx, entry.typeName);
        for (const property of metadata.properties ?? []) {
            if (property.indexParameterCount !== 0
                || findAttribute(property, 'Newtonsoft.Json.JsonIgnoreAttribute')) continue;
            const attributed = findAttribute(property, 'Newtonsoft.Json.JsonPropertyAttribute') !== null;
            const publiclyVisible = property.getterIsPublic === true || property.setterIsPublic === true;
            if (((!optIn && publiclyVisible) || attributed) && property.setter) {
                const jsonName = getJsonName(ctx, property, containerStrategy);
                add(propertyGroups, property, { kind: 'property', member: property, jsonName });
            }
        }
        for (const field of metadata.fields ?? []) {
            if (field.isStatic || findAttribute(field, 'Newtonsoft.Json.JsonIgnoreAttribute')) continue;
            const attributed = findAttribute(field, 'Newtonsoft.Json.JsonPropertyAttribute') !== null;
            if (((!optIn && field.isPublic === true) || attributed) && field.isInitOnly !== true) {
                const jsonName = getJsonName(ctx, field, containerStrategy);
                add(fieldGroups, field, { kind: 'field', member: field, jsonName });
            }
        }
    }
    return requireUniqueJsonNames(ctx, [
        ...[...fieldGroups.values()].flatMap(group => group.contracts),
        ...[...propertyGroups.values()].flatMap(group => group.contracts)
    ]);
}

function getConstructorContracts(ctx, typeName) {
    const optIn = getMemberSerialization(ctx, typeName) === 1;
    const contracts = [];
    for (const entry of getTypeHierarchyEntries(typeName)) {
        const { metadata } = entry;
        const containerStrategy = getContainerNamingStrategy(ctx, entry.typeName);
        for (const property of metadata.properties ?? []) {
            if (property.indexParameterCount !== 0
                || findAttribute(property, 'Newtonsoft.Json.JsonIgnoreAttribute')) continue;
            const attributed = findAttribute(property, 'Newtonsoft.Json.JsonPropertyAttribute') !== null;
            const publiclyVisible = property.getterIsPublic === true || property.setterIsPublic === true;
            if ((!optIn && publiclyVisible) || attributed) {
                contracts.push({ member: property, jsonName: getJsonName(ctx, property, containerStrategy) });
            }
        }
        for (const field of metadata.fields ?? []) {
            const attributed = findAttribute(field, 'Newtonsoft.Json.JsonPropertyAttribute') !== null;
            if (!field.isStatic
                && ((!optIn && field.isPublic === true) || attributed)
                && !findAttribute(field, 'Newtonsoft.Json.JsonIgnoreAttribute')) {
                contracts.push({ member: field, jsonName: getJsonName(ctx, field, containerStrategy) });
            }
        }
    }
    return requireUniqueJsonNames(ctx, contracts);
}

function selectConstructor(ctx, typeName, metadata) {
    const constructors = metadata.constructors ?? [];
    const attributed = constructors.filter(constructor =>
        findAttribute(constructor, 'Newtonsoft.Json.JsonConstructorAttribute'));
    if (attributed.length > 1) {
        fail(ctx, 'Newtonsoft.Json.JsonException', `Type '${typeName}' has more than one JsonConstructor.`);
    }
    if (attributed.length === 1) return attributed[0];

    const publicConstructors = constructors.filter(constructor => constructor.isPublic === true);
    const publicDefault = publicConstructors.find(constructor => constructor.parameters.length === 0);
    if (metadata.isValueType === true && (constructors.length === 0 || publicDefault)) return null;
    if (publicDefault) return publicDefault;
    const publicParameterized = publicConstructors.filter(constructor => constructor.parameters.length !== 0);
    if (publicParameterized.length === 1) return publicParameterized[0];
    const privateDefault = constructors.find(constructor =>
        constructor.isPublic !== true && constructor.parameters.length === 0);
    if (privateDefault) return privateDefault;
    fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Unable to find a constructor to use for type ${typeName}.`);
}

function indexConstructorInputs(value) {
    const inputs = new Map();
    for (const [name, memberValue] of Object.entries(value)) {
        const foldedName = name.toLowerCase();
        const input = inputs.get(foldedName) ?? { name, memberValue, names: [] };
        input.name = name;
        input.memberValue = memberValue;
        input.names.push(name);
        inputs.set(foldedName, input);
    }
    return inputs;
}

function bindConstructor(ctx, typeName, metadata, value, path, sourceText, locations) {
    const constructorMetadata = selectConstructor(ctx, typeName, metadata);
    const contracts = getConstructorContracts(ctx, typeName);
    const contractsByClrName = new Map();
    for (const contract of contracts) {
        const name = contract.member.name.toLowerCase();
        const matches = contractsByClrName.get(name) ?? [];
        matches.push(contract);
        contractsByClrName.set(name, matches);
    }
    const inputsByName = indexConstructorInputs(value);
    const consumedNames = new Set();
    const constructorBoundJsonNames = new Set();
    const containerStrategy = getContainerNamingStrategy(ctx, typeName);
    const argumentsToPass = (constructorMetadata?.parameters ?? []).map(parameter => {
        if (typeof parameter.name !== 'string') {
            fail(ctx, 'System.NotSupportedException', `Constructor '${constructorMetadata.methodName}' has a parameter without metadata name.`);
        }
        const parameterAttribute = requireSupportedJsonProperty(ctx, parameter);
        const matchingContracts = contractsByClrName.get(parameter.name.toLowerCase()) ?? [];
        if (matchingContracts.length > 1) {
            fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Multiple members match constructor parameter '${parameter.name}'.`);
        }
        const jsonName = parameterAttribute
            ? getJsonName(ctx, parameter, containerStrategy)
            : matchingContracts[0]?.jsonName ?? parameter.name;
        if (matchingContracts.length === 1) constructorBoundJsonNames.add(jsonName.toLowerCase());
        const input = inputsByName.get(jsonName.toLowerCase());
        if (!input) return getDefaultValue(parameter.typeName);
        for (const name of input.names) consumedNames.add(name);
        return convert(
            ctx,
            input.memberValue,
            parameter,
            appendPath(path, input.name),
            sourceText,
            locations);
    });

    const instance = initializeObject(typeName);
    if (constructorMetadata === null) {
        return { instance, consumedNames, constructorBoundJsonNames };
    }
    const constructor = globalThis.DotNet.Methods[constructorMetadata.methodName];
    if (typeof constructor !== 'function') {
        fail(ctx, 'System.NotSupportedException', `Constructor '${constructorMetadata.methodName}' is unavailable.`);
    }
    constructor(ctx, instance, ...argumentsToPass);
    return { instance, consumedNames, constructorBoundJsonNames };
}

function validateRequiredMembers(ctx, typeName, value, path, sourceText, locations) {
    const inputsByName = indexConstructorInputs(value);
    const itemRequired = getItemRequired(ctx, typeName);
    for (const contract of getConstructorContracts(ctx, typeName)) {
        const required = getRequiredMode(ctx, contract.member, itemRequired);
        if (required === 0) continue;
        const input = inputsByName.get(contract.jsonName.toLowerCase());
        const missing = input === undefined;
        const nullDisallowed = input?.memberValue === null && (required === 2 || required === 3);
        if (!(missing && (required === 1 || required === 2)) && !nullDisallowed) continue;
        const location = locations.get(path);
        if (!location) {
            fail(ctx, 'System.NotSupportedException', `The JSON source location for '${path}' could not be resolved.`);
        }
        const message = missing
            ? `Required property '${contract.jsonName}' not found in JSON.`
            : `Required property '${contract.jsonName}' expects a value but got null.`;
        throwManagedException(
            ctx,
            'Newtonsoft.Json.JsonSerializationException',
            message,
            null,
            exceptionFields(sourceText, path, location.end));
    }
}

function findContract(ctx, contracts, jsonName) {
    const exact = contracts.filter(contract => contract.jsonName === jsonName);
    if (exact.length > 1) {
        fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `A member with the name '${jsonName}' already exists.`);
    }
    if (exact.length === 1) return exact[0];
    const folded = contracts.filter(contract => contract.jsonName.toLowerCase() === jsonName.toLowerCase());
    if (folded.length > 1) {
        fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Multiple members match '${jsonName}'.`);
    }
    return folded[0] ?? null;
}

function parseInteger(ctx, node, typeName, minimum, maximum, path, sourceText, locations) {
    const isString = typeof node === 'string';
    const numberSource = isString ? node.trim() : node?.[exactNumber];
    const valid = typeof numberSource === 'string'
        && (isString ? /^[+-]?\d+$/.test(numberSource) : /^-?(?:0|[1-9]\d*)$/.test(numberSource));
    if (!valid) {
        const location = locations.get(path);
        if (!location) {
            fail(ctx, 'System.NotSupportedException', `The JSON source location for '${path}' could not be resolved.`);
        }
        throwManagedException(
            ctx,
            'Newtonsoft.Json.JsonReaderException',
            isString
                ? `Could not convert string to integer: ${node}.`
                : `Error converting value to type '${typeName}'.`,
            null,
            exceptionFields(sourceText, path, location.end));
    }
    const value = BigInt(numberSource);
    if (value < minimum || value > maximum) {
        readerFail(ctx, typeName, numberSource, path, sourceText, locations);
    }
    return value;
}

function floatingStringFail(ctx, value, path, sourceText, locations) {
    const location = locations.get(path);
    if (!location) {
        fail(ctx, 'System.NotSupportedException', `The JSON source location for '${path}' could not be resolved.`);
    }
    throwManagedException(
        ctx,
        'Newtonsoft.Json.JsonReaderException',
        `Could not convert string to double: ${value}.`,
        null,
        exceptionFields(sourceText, path, location.end));
}

function parseFloating(ctx, node, typeName, path, sourceText, locations) {
    let value;
    if (typeof node === 'string') {
        if (node === 'NaN') value = Number.NaN;
        else if (node === 'Infinity') value = Number.POSITIVE_INFINITY;
        else if (node === '-Infinity') value = Number.NEGATIVE_INFINITY;
        else {
            const source = node.trim();
            if (!/^[+-]?(?:(?:\d+(?:\.\d*)?)|(?:\.\d+))(?:[eE][+-]?\d+)?$/.test(source)) {
                floatingStringFail(ctx, node, path, sourceText, locations);
            }
            value = Number(source);
        }
    } else {
        const numberSource = node?.[exactNumber];
        if (typeof numberSource !== 'string') {
            fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error converting value to type '${typeName}'.`);
        }
        value = Number(numberSource);
    }
    return typeName === 'System.Single' ? Math.fround(value) : value;
}

function parseBoolean(ctx, node, path, sourceText, locations) {
    if (typeof node === 'boolean') return node;
    if (typeof node === 'string') {
        const normalized = node.trim().toLowerCase();
        if (normalized === 'true') return true;
        if (normalized === 'false') return false;
        const location = locations.get(path);
        if (!location) {
            fail(ctx, 'System.NotSupportedException', `The JSON source location for '${path}' could not be resolved.`);
        }
        throwManagedException(
            ctx,
            'Newtonsoft.Json.JsonReaderException',
            `Could not convert string to boolean: ${node}.`,
            null,
            exceptionFields(sourceText, path, location.end));
    }
    const numberSource = node?.[exactNumber];
    if (typeof numberSource === 'string') return Number(numberSource) !== 0;
    fail(ctx, 'Newtonsoft.Json.JsonSerializationException', 'Error converting value to System.Boolean.');
}

function parseCharacter(ctx, node, path, sourceText, locations) {
    if (typeof node === 'string' && node.length === 1) return node.charCodeAt(0);
    const numberSource = node?.[exactNumber];
    if (typeof numberSource === 'string' && /^-?(?:0|[1-9]\d*)$/.test(numberSource)) {
        const value = BigInt(numberSource);
        if (value >= 0n && value <= 65535n) return Number(value);
    }
    const location = locations.get(path);
    if (!location) {
        fail(ctx, 'System.NotSupportedException', `The JSON source location for '${path}' could not be resolved.`);
    }
    throwManagedException(
        ctx,
        'Newtonsoft.Json.JsonSerializationException',
        'Error converting value to System.Char.',
        null,
        exceptionFields(sourceText, path, location.end));
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

function replaceIsolatedSurrogates(value) {
    let result = '';
    for (let index = 0; index < value.length; index++) {
        const code = value.charCodeAt(index);
        if (code >= 0xd800 && code <= 0xdbff) {
            const next = index + 1 < value.length ? value.charCodeAt(index + 1) : -1;
            if (next >= 0xdc00 && next <= 0xdfff) {
                result += value[index] + value[index + 1];
                index++;
            } else {
                result += '\ufffd';
            }
        } else if (code >= 0xdc00 && code <= 0xdfff) {
            result += '\ufffd';
        } else {
            result += value[index];
        }
    }
    return result;
}

function parseEnumName(ctx, value, typeName, metadata) {
    const parts = value.trim().split(',').map(part => part.trim());
    if (parts.length === 0 || parts.some(part => part.length === 0)) {
        fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error converting value '${value}' to type '${typeName}'.`);
    }
    let combined = 0n;
    for (const part of parts) {
        const entry = (metadata.enumValues ?? []).find(candidate =>
            candidate.name.toLowerCase() === part.toLowerCase());
        if (!entry) return null;
        combined |= BigInt(entry.value);
    }
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
    const width = widths[metadata.enumUnderlyingType];
    if (width === undefined) {
        fail(ctx, 'System.NotSupportedException', `Enum underlying type '${metadata.enumUnderlyingType}' is not implemented.`);
    }
    combined = BigInt.asUintN(width, combined);
    if (metadata.enumUnderlyingType === 'System.SByte'
        || metadata.enumUnderlyingType === 'System.Int16'
        || metadata.enumUnderlyingType === 'System.Int32'
        || metadata.enumUnderlyingType === 'System.Int64') {
        combined = BigInt.asIntN(width, combined);
    }
    return metadata.enumUnderlyingType === 'System.Int64'
        || metadata.enumUnderlyingType === 'System.UInt64'
        ? combined
        : Number(combined);
}

function convert(ctx, value, declaredType, path, sourceText, locations) {
    const typeReference = resolveManagedTypeReference(declaredType);
    const typeName = getManagedTypeName(typeReference);
    if (value === null) {
        if (typeName === 'System.String' || typeName.endsWith('[]')
            || globalThis.DotNet.Types[typeName]?.isClass === true) return null;
        fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Cannot convert null value to ${typeName}.`);
    }
    switch (typeName) {
        case 'System.String':
            if (typeof value === 'string') return wrap(replaceIsolatedSurrogates(value), typeName);
            if (typeof value === 'boolean') return wrap(value ? 'true' : 'false', typeName);
            if (typeof value?.[exactNumber] === 'string') {
                return wrap(value[exactNumber], typeName);
            }
            fail(ctx, 'Newtonsoft.Json.JsonSerializationException', 'Error converting value to System.String.');
        case 'System.Char':
            return wrap(parseCharacter(ctx, value, path, sourceText, locations), typeName);
        case 'System.Boolean':
            return wrap(parseBoolean(ctx, value, path, sourceText, locations), typeName);
        case 'System.Byte': return wrap(Number(parseInteger(ctx, value, typeName, 0n, 255n, path, sourceText, locations)), typeName);
        case 'System.SByte': return wrap(Number(parseInteger(ctx, value, typeName, -128n, 127n, path, sourceText, locations)), typeName);
        case 'System.Int16': return wrap(Number(parseInteger(ctx, value, typeName, -32768n, 32767n, path, sourceText, locations)), typeName);
        case 'System.UInt16': return wrap(Number(parseInteger(ctx, value, typeName, 0n, 65535n, path, sourceText, locations)), typeName);
        case 'System.Int32': return wrap(Number(parseInteger(ctx, value, typeName, -2147483648n, 2147483647n, path, sourceText, locations)), typeName);
        case 'System.UInt32': return wrap(Number(parseInteger(ctx, value, typeName, 0n, 4294967295n, path, sourceText, locations)), typeName);
        case 'System.Int64': return wrap(parseInteger(ctx, value, typeName, -9223372036854775808n, 9223372036854775807n, path, sourceText, locations), typeName);
        case 'System.UInt64': return wrap(parseInteger(ctx, value, typeName, 0n, 18446744073709551615n, path, sourceText, locations), typeName);
        case 'System.Single':
        case 'System.Double': return wrap(
            parseFloating(ctx, value, typeName, path, sourceText, locations),
            typeName);
        case 'System.DateTime':
        case 'System.DateTimeOffset':
        case 'System.DateOnly':
        case 'System.TimeOnly':
        case 'System.TimeSpan': {
            const temporal = parseManagedTemporal(typeName, value);
            if (temporal === null) {
                fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error converting value to type '${typeName}'.`);
            }
            return temporal;
        }
    }

    const enumMetadata = globalThis.DotNet.Types[typeName];
    if (typeof enumMetadata?.enumUnderlyingType === 'string') {
        if (typeof value === 'string' && !/^[+-]?\d+$/.test(value.trim())) {
            const namedValue = parseEnumName(ctx, value, typeName, enumMetadata);
            if (namedValue === null) {
                fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error converting value '${value}' to type '${typeName}'.`);
            }
            return wrap(namedValue, typeName);
        }
        const enumNumberSource = value?.[exactNumber];
        if (enumMetadata.enumUnderlyingType === 'System.UInt64'
            && typeof enumNumberSource === 'string'
            && /^-?(?:0|[1-9]\d*)$/.test(enumNumberSource)
            && BigInt(enumNumberSource) > 9223372036854775807n) {
            fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error converting value to type '${typeName}'.`);
        }
        const converted = convert(
            ctx, value, enumMetadata.enumUnderlyingType, path, sourceText, locations);
        return wrap(unwrap(converted), typeName);
    }

    if (typeName === 'System.Byte[]') {
        if (Array.isArray(value)) {
            return wrap(
                value.map((element, index) => convert(
                    ctx, element, 'System.Byte', `${path}[${index}]`, sourceText, locations)),
                typeName);
        }
        if (typeof value !== 'string') {
            fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error converting value to type '${typeName}'.`);
        }
        const bytes = decodeBase64(value);
        if (bytes === null) {
            fail(ctx, 'System.FormatException', 'The input is not a valid Base-64 string.');
        }
        return wrap(bytes.map(byte => wrap(byte, 'System.Byte')), typeName);
    }

    if (typeName.endsWith('[]')) {
        if (!Array.isArray(value)) {
            fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error converting value to type '${typeName}'.`);
        }
        const elementType = typeName.slice(0, -2);
        return wrap(
            value.map((element, index) => convert(ctx, element, elementType, `${path}[${index}]`, sourceText, locations)),
            typeName);
    }

    const collection = findCollectionContract(typeReference);
    if (collection !== null) {
        if (collection.kind === 'list') {
            if (!Array.isArray(value)) {
                fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error converting value to type '${typeName}'.`);
            }
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
                        locations));
            }
            return instance;
        }

        if (Array.isArray(value) || typeof value !== 'object' || value?.[exactNumber] !== undefined) {
            fail(ctx, 'Newtonsoft.Json.JsonSerializationException', `Error converting value to type '${typeName}'.`);
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
                    locations));
        }
        return instance;
    }

    const metadata = globalThis.DotNet.Types[typeName];
    if (!isManagedObjectContract(metadata) || Array.isArray(value) || typeof value !== 'object' || value?.[exactNumber] !== undefined) {
        fail(ctx, 'System.NotSupportedException', `JSON conversion for managed type '${typeName}' is not implemented.`);
    }

    validateRequiredMembers(ctx, typeName, value, path, sourceText, locations);
    const { instance, consumedNames, constructorBoundJsonNames } = bindConstructor(
        ctx, typeName, metadata, value, path, sourceText, locations);
    const contracts = getContracts(ctx, typeName);
    const missingMemberHandling = getMissingMemberHandling(ctx, typeName);
    const itemNullValueHandling = getItemNullValueHandling(ctx, typeName);
    function assign(contract, converted) {
        if (contract.kind === 'property') {
            setPropertyValue(ctx, wrap({ metadata: contract.member }, 'System.Reflection.PropertyInfo'), instance, converted);
        } else {
            unwrap(instance)[contract.member.storageKey ?? contract.member.name] = converted;
        }
    }
    for (const [name, memberValue] of Object.entries(value)) {
        if (consumedNames.has(name)) continue;
        const contract = findContract(ctx, contracts, name);
        if (!contract) {
            if (missingMemberHandling === 1) {
                missingMemberFail(ctx, typeName, name, path, sourceText, locations);
            }
            continue;
        }
        if (memberValue === null
            && getNullValueHandling(ctx, contract.member, itemNullValueHandling) === 1) continue;
        const converted = convert(ctx, memberValue, contract.member, appendPath(path, name), sourceText, locations);
        const defaultHandling = getDefaultValueHandling(ctx, contract.member);
        if ((defaultHandling & 1) !== 0 && (defaultHandling & 2) === 0
            && isMemberDefaultValue(ctx, converted, contract.member)) continue;
        assign(contract, converted);
    }
    const presentNames = new Set(Object.keys(value).map(name => name.toLowerCase()));
    for (const contract of contracts) {
        const foldedName = contract.jsonName.toLowerCase();
        if (presentNames.has(foldedName) || constructorBoundJsonNames.has(foldedName)) continue;
        if ((getDefaultValueHandling(ctx, contract.member) & 2) === 0) continue;
        assign(contract, getMemberDefaultValue(ctx, contract.member));
    }
    return instance;
}

function emptyResult(ctx, typeName) {
    if (typeName === 'System.String' || typeName.endsWith('[]')
        || globalThis.DotNet.Types[typeName]?.isClass === true) return null;
    fail(
        ctx,
        'Newtonsoft.Json.JsonSerializationException',
        `No JSON content found and type '${typeName}' is not nullable.`);
}

function isIncompleteContainer(source) {
    const containers = [];
    let inString = false;
    let escaped = false;
    for (const character of source) {
        if (inString) {
            if (escaped) escaped = false;
            else if (character === '\\') escaped = true;
            else if (character === '"') inString = false;
            continue;
        }
        if (character === '"') inString = true;
        else if (character === '{' || character === '[') containers.push(character);
        else if (character === '}' || character === ']') {
            const expected = character === '}' ? '{' : '[';
            if (containers.pop() !== expected) return false;
        }
    }
    return !inString && containers.length !== 0;
}

globalThis.DotNet.Methods['Newtonsoft.Json.JsonConvert.DeserializeObject<T>(System.String)'] = function (ctx, value) {
    if (isManagedNull(value)) fail(ctx, 'System.ArgumentNullException', 'Value cannot be null. (Parameter \'value\')');

    const source = unwrap(value);
    const targetType = getTargetType(ctx);
    const typeName = getManagedTypeName(targetType);
    if (/^\s*$/.test(source)) return emptyResult(ctx, typeName);
    const trimmed = source.trim();
    if ((typeName === 'System.Single' || typeName === 'System.Double')
        && (trimmed === 'NaN' || trimmed === 'Infinity' || trimmed === '-Infinity')) {
        return wrap(parseFloating(ctx, trimmed, typeName, '', source, null), typeName);
    }
    const sourceIndex = createSourceIndex(source, 64);
    if (sourceIndex.depthError) {
        throwManagedException(
            ctx,
            'Newtonsoft.Json.JsonReaderException',
            "The reader's MaxDepth of 64 has been exceeded.",
            null,
            exceptionFields(
                source,
                sourceIndex.depthError.path,
                sourceIndex.depthError.index));
    }
    if (sourceIndex.additional) {
        throwManagedException(
            ctx,
            'Newtonsoft.Json.JsonReaderException',
            `Additional text encountered after finished reading JSON content: ${source[sourceIndex.additional.index]}.`,
            null,
            exceptionFields(source, '', sourceIndex.additional.index));
    }
    const parseSource = normalizeNamedFloatingTokens(source, sourceIndex.namedFloatingTokens);

    let parsed;
    try {
        parsed = JSON.parse(parseSource, (key, item, context) => {
            if (typeof item !== 'number') return item;
            if (typeof context?.source !== 'string') {
                fail(ctx, 'System.NotSupportedException', 'This JavaScript runtime does not expose exact JSON number tokens.');
            }
            return Object.freeze({ [exactNumber]: context.source });
        });
    } catch (error) {
        if (error instanceof SyntaxError) {
            const exceptionType = isIncompleteContainer(source)
                ? 'Newtonsoft.Json.JsonSerializationException'
                : 'Newtonsoft.Json.JsonReaderException';
            if (exceptionType === 'Newtonsoft.Json.JsonSerializationException') {
                fail(ctx, exceptionType, error.message);
            }
            const syntax = sourceIndex.error;
            if (!syntax) {
                fail(ctx, 'System.NotSupportedException', 'The JSON syntax error location could not be resolved.');
            }
            throwManagedException(
                ctx,
                exceptionType,
                error.message,
                null,
                exceptionFields(source, syntax.path, syntax.index));
        }
        throw error;
    }
    parsed = restoreNamedFloatingValues(parsed, '', sourceIndex.namedFloatingValues);
    return convert(ctx, parsed, targetType, '', source, sourceIndex.locations);
};
