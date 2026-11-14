import { throwManagedException } from '../../managed-exceptions.js';
import { getDefaultValue, getManagedTypeName, unwrap, wrap } from '../../msil-type-helpers.js';
import { parseManagedTemporal, parseManagedTemporalResult } from './managed-temporal.js';

const integralTypes = Object.freeze({
    'System.SByte': { bits: 8, signed: true, storage: 'number' },
    'System.Byte': { bits: 8, signed: false, storage: 'number' },
    'System.Int16': { bits: 16, signed: true, storage: 'number' },
    'System.UInt16': { bits: 16, signed: false, storage: 'number' },
    'System.Int32': { bits: 32, signed: true, storage: 'number' },
    'System.UInt32': { bits: 32, signed: false, storage: 'number' },
    'System.Int64': { bits: 64, signed: true, storage: 'bigint' },
    'System.UInt64': { bits: 64, signed: false, storage: 'bigint' },
    'System.IntPtr': { bits: 32, signed: true, storage: 'native-int' },
    'System.UIntPtr': { bits: 32, signed: false, storage: 'native-uint' },
    'System.Int128': { bits: 128, signed: true, storage: 'int128' },
    'System.UInt128': { bits: 128, signed: false, storage: 'int128' }
});

const signedEnumTypes = new Set([
    'System.SByte', 'System.Int16', 'System.Int32', 'System.Int64'
]);

const decimalMaximum = (1n << 96n) - 1n;

function success(value) {
    return { status: 'success', value };
}

function failure(status = 'format') {
    return { status, value: null };
}

function inputText(value) {
    const text = unwrap(value);
    if (text === null) return { status: 'argument-null', text: null };
    return typeof text === 'string'
        ? { status: 'success', text: text.trim() }
        : { status: 'format', text: null };
}

function integralBounds(type) {
    const bits = BigInt(type.bits);
    return type.signed
        ? { minimum: -(1n << (bits - 1n)), maximum: (1n << (bits - 1n)) - 1n }
        : { minimum: 0n, maximum: (1n << bits) - 1n };
}

function int128Value(value, typeName) {
    const bits = BigInt.asUintN(128, value);
    return wrap({
        _lower: wrap(BigInt.asUintN(64, bits), 'System.UInt64'),
        _upper: wrap(BigInt.asUintN(64, bits >> 64n), 'System.UInt64')
    }, typeName);
}

function integralValue(value, typeName) {
    const type = integralTypes[typeName];
    switch (type.storage) {
        case 'number': return wrap(Number(value), typeName);
        case 'bigint': return wrap(value, typeName);
        case 'native-int': return wrap(Number(value), '$NativeInt32');
        case 'native-uint': return wrap(Number(value), '$NativeUInt32');
        case 'int128': return int128Value(value, typeName);
        default: throw new TypeError(`Unsupported integral storage '${type.storage}'.`);
    }
}

function parseIntegralCore(text, typeName) {
    const type = integralTypes[typeName];
    if (type === undefined) throw new TypeError(`Unsupported integral type '${typeName}'.`);
    if (!/^[+-]?\d+$/.test(text)) return failure();
    let value;
    try { value = BigInt(text); }
    catch { return failure(); }
    const bounds = integralBounds(type);
    return value < bounds.minimum || value > bounds.maximum
        ? failure('overflow')
        : success(integralValue(value, typeName));
}

export function parseIntegral(value, typeName) {
    const input = inputText(value);
    return input.status === 'success'
        ? parseIntegralCore(input.text, typeName)
        : failure(input.status);
}

function encodeHalf(value) {
    if (Number.isNaN(value)) return 0x7e00;
    if (value === Infinity) return 0x7c00;
    if (value === -Infinity) return 0xfc00;
    const sign = value < 0 || Object.is(value, -0) ? 0x8000 : 0;
    const magnitude = Math.abs(value);
    if (magnitude === 0) return sign;
    const floatBits = new Uint32Array(new Float32Array([magnitude]).buffer)[0];
    let exponent = ((floatBits >>> 23) & 0xff) - 127 + 15;
    let significand = floatBits & 0x7fffff;
    if (exponent <= 0) {
        if (exponent < -10) return sign;
        significand |= 0x800000;
        const shift = 14 - exponent;
        let rounded = significand >>> shift;
        const remainder = significand & ((1 << shift) - 1);
        const halfway = 1 << (shift - 1);
        if (remainder > halfway || (remainder === halfway && (rounded & 1) !== 0)) rounded++;
        return sign | rounded;
    }
    if (exponent >= 31) return sign | 0x7c00;
    let rounded = significand >>> 13;
    const remainder = significand & 0x1fff;
    if (remainder > 0x1000 || (remainder === 0x1000 && (rounded & 1) !== 0)) {
        rounded++;
        if (rounded === 0x400) {
            rounded = 0;
            exponent++;
            if (exponent >= 31) return sign | 0x7c00;
        }
    }
    return sign | (exponent << 10) | rounded;
}

function parseFloatingCore(text, typeName) {
    const folded = text.toLowerCase();
    let value;
    if (folded === 'nan') value = NaN;
    else if (folded === 'infinity' || text === '∞' || folded === '+infinity' || text === '+∞') value = Infinity;
    else if (folded === '-infinity' || text === '-∞') value = -Infinity;
    else {
        if (!/^[+-]?(?:(?:\d[\d,]*)(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/.test(text)) return failure();
        value = Number(text.replace(/,/g, ''));
        if (Number.isNaN(value)) return failure();
    }
    if (typeName === 'System.Half') {
        return success(wrap({ _value: wrap(encodeHalf(value), 'System.UInt16') }, typeName));
    }
    if (typeName === 'System.Single') value = Math.fround(value);
    return success(wrap(value, typeName));
}

function roundDecimal(coefficient, removedDigits) {
    if (removedDigits <= 0) return coefficient;
    const divisor = 10n ** BigInt(removedDigits);
    let quotient = coefficient / divisor;
    const remainder = coefficient % divisor;
    const doubled = remainder * 2n;
    if (doubled > divisor || (doubled === divisor && (quotient & 1n) !== 0n)) quotient++;
    return quotient;
}

function decimalValue(coefficient, scale, negative) {
    const flags = (scale << 16) | (negative ? 0x80000000 : 0);
    return wrap({
        _flags: wrap(flags | 0, 'System.Int32'),
        _hi32: wrap(Number((coefficient >> 64n) & 0xffffffffn), 'System.UInt32'),
        _lo64: wrap(coefficient & 0xffffffffffffffffn, 'System.UInt64')
    }, 'System.Decimal');
}

function parseDecimalCore(text) {
    let negative = false;
    let leadingSign = '';
    if (text.startsWith('+') || text.startsWith('-')) {
        leadingSign = text[0];
        negative = leadingSign === '-';
        text = text.substring(1).trimStart();
    }
    let trailingSign = '';
    if (text.endsWith('+') || text.endsWith('-')) {
        trailingSign = text[text.length - 1];
        text = text.substring(0, text.length - 1).trimEnd();
    }
    if (leadingSign !== '' && trailingSign !== '') return failure();
    if (trailingSign !== '') negative = trailingSign === '-';
    if (!/^(?:(?:\d[\d,]*)(?:\.\d*)?|\.\d+)$/.test(text)) return failure();
    const normalized = text.replace(/,/g, '');
    const point = normalized.indexOf('.');
    const scale = point < 0 ? 0 : normalized.length - point - 1;
    const digits = normalized.replace('.', '').replace(/^0+/, '') || '0';
    const source = BigInt(digits);
    for (let targetScale = Math.min(scale, 28); targetScale >= 0; targetScale--) {
        const coefficient = roundDecimal(source, scale - targetScale);
        if (coefficient <= decimalMaximum) {
            return success(decimalValue(coefficient, targetScale, negative));
        }
    }
    return failure('overflow');
}

export function parseReal(value, typeName) {
    const input = inputText(value);
    if (input.status !== 'success') return failure(input.status);
    return typeName === 'System.Decimal'
        ? parseDecimalCore(input.text)
        : parseFloatingCore(input.text, typeName);
}

function canonicalGuidFromCompact(compact) {
    compact = compact.toLowerCase();
    return `${compact.substring(0, 8)}-${compact.substring(8, 12)}-${compact.substring(12, 16)}`
        + `-${compact.substring(16, 20)}-${compact.substring(20)}`;
}

function parseGuidCore(text) {
    if (text.startsWith('{(')) return failure();
    let match = /^\{?\(?([0-9a-f]{8})-([0-9a-f]{4})-([0-9a-f]{4})-([0-9a-f]{4})-([0-9a-f]{12})\)?\}?$/i.exec(text);
    if (match !== null) {
        const opening = text[0];
        const closing = text[text.length - 1];
        if ((opening === '{' && closing !== '}') || (opening === '(' && closing !== ')')
            || ((closing === '}' || closing === ')') && !((opening === '{' && closing === '}') || (opening === '(' && closing === ')')))) return failure();
        return success(wrap(match.slice(1).join('-').toLowerCase(), 'System.Guid'));
    }
    match = /^([0-9a-f]{32})$/i.exec(text);
    if (match !== null) return success(wrap(canonicalGuidFromCompact(match[1]), 'System.Guid'));
    match = /^\{\s*0x([0-9a-f]{8})\s*,\s*0x([0-9a-f]{4})\s*,\s*0x([0-9a-f]{4})\s*,\s*\{\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*\}\s*\}$/i.exec(text);
    return match === null
        ? failure()
        : success(wrap(`${match[1]}-${match[2]}-${match[3]}-${match[4]}${match[5]}-${match.slice(6).join('')}`.toLowerCase(), 'System.Guid'));
}

export function parseGuid(value) {
    const input = inputText(value);
    return input.status === 'success' ? parseGuidCore(input.text) : failure(input.status);
}

function enumWidth(underlyingType) {
    switch (underlyingType) {
        case 'System.SByte': case 'System.Byte': return 8;
        case 'System.Int16': case 'System.UInt16': return 16;
        case 'System.Int32': case 'System.UInt32': return 32;
        case 'System.Int64': case 'System.UInt64': return 64;
        default: throw new TypeError(`Unsupported enum underlying type '${underlyingType}'.`);
    }
}

function enumTypeName(ctx) {
    const reference = ctx.genericParams?.TEnum;
    return reference === undefined ? null : getManagedTypeName(reference);
}

function enumRawValue(bits, underlyingType) {
    const width = enumWidth(underlyingType);
    const value = signedEnumTypes.has(underlyingType)
        ? BigInt.asIntN(width, bits)
        : BigInt.asUintN(width, bits);
    return width <= 32 ? Number(value) : value;
}

export function parseEnum(ctx, value) {
    const input = inputText(value);
    if (input.status !== 'success') return failure(input.status);
    if (input.text === '') return failure('argument');
    const typeName = enumTypeName(ctx);
    const metadata = globalThis.DotNet?.Types?.[typeName];
    if (typeName === null || typeof metadata?.enumUnderlyingType !== 'string'
        || !Array.isArray(metadata.enumValues)) {
        throw new Error(`Enum metadata is unavailable for managed type '${typeName}'.`);
    }
    if (/^[+-]?\d+$/.test(input.text)) {
        const parsed = parseIntegralCore(input.text, metadata.enumUnderlyingType);
        return parsed.status === 'success'
            ? success(wrap(unwrap(parsed.value), typeName))
            : parsed;
    }
    let bits = 0n;
    for (const part of input.text.split(',')) {
        const name = part.trim();
        const entry = metadata.enumValues.find(candidate => candidate.name === name);
        if (name === '' || entry === undefined) return failure('argument');
        bits |= BigInt.asUintN(enumWidth(metadata.enumUnderlyingType), BigInt(entry.value));
    }
    return success(wrap(enumRawValue(bits, metadata.enumUnderlyingType), typeName));
}

export function parseTemporal(value, typeName) {
    const input = inputText(value);
    if (input.status !== 'success') return failure(input.status);
    return parseManagedTemporalResult(typeName, input.text);
}

function defaultValue(typeName, ctx) {
    if (typeName === 'System.Guid') {
        return wrap('00000000-0000-0000-0000-000000000000', typeName);
    }
    if (typeName === 'System.Int128' || typeName === 'System.UInt128') {
        return integralValue(0n, typeName);
    }
    if (typeName === 'System.Half') {
        return wrap({ _value: wrap(0, 'System.UInt16') }, typeName);
    }
    if (typeName === 'System.Decimal') {
        return decimalValue(0n, 0, false);
    }
    const temporalDefaults = {
        'System.DateTime': '0001-01-01T00:00:00',
        'System.DateTimeOffset': '0001-01-01T00:00:00Z',
        'System.DateOnly': '0001-01-01',
        'System.TimeOnly': '00:00:00',
        'System.TimeSpan': '00:00:00'
    };
    if (Object.hasOwn(temporalDefaults, typeName)) {
        return parseManagedTemporal(typeName, temporalDefaults[typeName]);
    }
    if (typeName === 'TEnum') {
        const resolved = enumTypeName(ctx);
        const underlyingType = globalThis.DotNet?.Types?.[resolved]?.enumUnderlyingType;
        return typeof underlyingType === 'string'
            ? wrap(enumRawValue(0n, underlyingType), resolved)
            : wrap(0, resolved);
    }
    return getDefaultValue(typeName, ctx.genericParams);
}

export function completeParse(ctx, result, invalidException = 'System.FormatException') {
    if (result.status === 'success') return result.value;
    if (result.status === 'argument-null') {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Value cannot be null. (Parameter \'s\')');
    }
    if (result.status === 'overflow') {
        throwManagedException(ctx, 'System.OverflowException');
    }
    throwManagedException(ctx, invalidException);
}

export function completeTryParse(ctx, result, destination, typeName) {
    destination.value = result.status === 'success'
        ? result.value
        : defaultValue(typeName, ctx);
    return wrap(result.status === 'success', 'System.Boolean');
}
