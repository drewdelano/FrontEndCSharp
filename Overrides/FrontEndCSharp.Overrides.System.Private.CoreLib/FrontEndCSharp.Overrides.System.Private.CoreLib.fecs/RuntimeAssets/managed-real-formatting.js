import { unwrap } from '../../msil-type-helpers.js';
import { formatCustomNumber } from './managed-formatting.js';

const typePrecision = Object.freeze({
    'System.Half': { roundTrip: 5, custom: 5 },
    'System.Single': { roundTrip: 9, custom: 7 },
    'System.Double': { roundTrip: 17, custom: 15 }
});

function fieldValue(value, name) {
    const raw = unwrap(value);
    if (raw == null || typeof raw !== 'object') return undefined;
    return unwrap(raw[name] ?? raw.v?.[name]);
}

function decodeHalf(bits) {
    const sign = (bits & 0x8000) === 0 ? 1 : -1;
    const exponent = (bits >>> 10) & 0x1f;
    const fraction = bits & 0x3ff;
    if (exponent === 0x1f) return fraction === 0 ? sign * Infinity : NaN;
    if (exponent === 0) return sign * fraction * 2 ** -24;
    return sign * (1 + fraction / 1024) * 2 ** (exponent - 15);
}

function encodeHalf(value) {
    if (Number.isNaN(value)) return 0x7e00;
    if (value === Infinity) return 0x7c00;
    if (value === -Infinity) return 0xfc00;
    const sign = value < 0 || Object.is(value, -0) ? 0x8000 : 0;
    let magnitude = Math.abs(value);
    if (magnitude === 0) return sign;
    if (magnitude >= 65520) return sign | 0x7c00;
    if (magnitude < 2 ** -14) {
        const fraction = Math.round(magnitude / 2 ** -24);
        return sign | Math.min(fraction, 0x3ff);
    }
    let exponent = Math.floor(Math.log2(magnitude));
    let significand = magnitude / 2 ** exponent;
    let fraction = Math.round((significand - 1) * 1024);
    if (fraction === 1024) { exponent++; fraction = 0; }
    return sign | ((exponent + 15) << 10) | fraction;
}

function parseDecimalText(text, preserveTrailingZeros = false) {
    let negative = text.startsWith('-');
    if (negative || text.startsWith('+')) text = text.substring(1);
    const parts = text.toLowerCase().split('e');
    const exponent = parts.length === 2 ? Number(parts[1]) : 0;
    const point = parts[0].indexOf('.');
    let decimalPosition = (point < 0 ? parts[0].length : point) + exponent;
    let digits = parts[0].replace('.', '');
    let leading = 0;
    while (leading < digits.length && digits[leading] === '0') leading++;
    decimalPosition -= leading;
    digits = digits.substring(leading);
    if (!preserveTrailingZeros) digits = digits.replace(/0+$/, '');
    if (digits === '') return { negative, digits: '0', decimalPosition: 1, zero: true };
    return { negative, digits, decimalPosition, zero: false };
}

function sameFloat(value, candidate, typeName, halfBits) {
    if (typeName === 'System.Half') return encodeHalf(candidate) === halfBits;
    if (typeName === 'System.Single') return Object.is(Math.fround(candidate), Math.fround(value));
    return Object.is(candidate, value);
}

function shortestFloating(value, typeName, halfBits) {
    const maximum = typePrecision[typeName].roundTrip;
    for (let precision = 1; precision <= maximum; precision++) {
        const candidate = value.toPrecision(precision);
        if (sameFloat(value, Number(candidate), typeName, halfBits)) return parseDecimalText(candidate);
    }
    return parseDecimalText(value.toPrecision(maximum));
}

function preciseFloating(value, digits) {
    return parseDecimalText(value.toExponential(Math.min(100, Math.max(0, digits - 1))));
}

function decimalBuffer(value) {
    const flags = Number(fieldValue(value, '_flags')) | 0;
    const high = BigInt(Number(fieldValue(value, '_hi32')) >>> 0);
    const low = BigInt.asUintN(64, BigInt(fieldValue(value, '_lo64')));
    const coefficient = (high << 64n) | low;
    const scale = (flags >>> 16) & 0xff;
    const digits = coefficient.toString();
    return {
        negative: (flags & 0x80000000) !== 0 && coefficient !== 0n,
        digits,
        decimalPosition: digits.length - scale,
        zero: coefficient === 0n,
        decimalScale: scale
    };
}

function roundSignificant(buffer, precision, preserveTrailingZeros = false) {
    if (precision <= 0 || buffer.digits.length <= precision) return { ...buffer };
    let digits = buffer.digits.substring(0, precision);
    let decimalPosition = buffer.decimalPosition;
    if (buffer.digits.charCodeAt(precision) >= 53) {
        const rounded = (BigInt(digits) + 1n).toString();
        if (rounded.length > precision) {
            digits = rounded.substring(0, precision);
            decimalPosition++;
        } else digits = rounded.padStart(precision, '0');
    }
    if (!preserveTrailingZeros) digits = digits.replace(/0+$/, '') || '0';
    return { ...buffer, digits, decimalPosition };
}

function scaledInteger(buffer, decimals, decimalShift = 0, bankersRounding = false) {
    const coefficient = BigInt(buffer.digits);
    const power = buffer.decimalPosition - buffer.digits.length + decimals + decimalShift;
    if (power >= 0) return coefficient * (10n ** BigInt(power));
    const divisor = 10n ** BigInt(-power);
    let quotient = coefficient / divisor;
    const doubledRemainder = (coefficient % divisor) * 2n;
    if (doubledRemainder > divisor
        || (doubledRemainder === divisor && (!bankersRounding || (quotient & 1n) !== 0n))) quotient++;
    return quotient;
}

function groupDigits(digits) {
    return digits.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

function fixed(buffer, decimals, grouped = false, shift = 0, bankersRounding = false) {
    const scaled = scaledInteger(buffer, decimals, shift, bankersRounding).toString().padStart(decimals + 1, '0');
    let integer = decimals === 0 ? scaled : scaled.substring(0, scaled.length - decimals);
    if (grouped) integer = groupDigits(integer);
    return decimals === 0 ? integer : `${integer}.${scaled.substring(scaled.length - decimals)}`;
}

function exponential(buffer, decimals, upper) {
    const rounded = roundSignificant(buffer, decimals + 1, true);
    const digits = rounded.digits.padEnd(decimals + 1, '0');
    const mantissa = decimals === 0 ? digits[0] : `${digits[0]}.${digits.substring(1)}`;
    const exponent = rounded.decimalPosition - 1;
    return `${mantissa}${upper ? 'E' : 'e'}${exponent < 0 ? '-' : '+'}${String(Math.abs(exponent)).padStart(3, '0')}`;
}

function general(buffer, precision, upper, preserveDecimalScale = false, defaultPrecision = 1) {
    const rounded = precision == null ? { ...buffer }
        : roundSignificant(buffer, precision, preserveDecimalScale);
    const limit = precision ?? defaultPrecision;
    const exponent = rounded.decimalPosition - 1;
    if (exponent < -4 || exponent >= limit) {
        const digits = preserveDecimalScale ? rounded.digits : rounded.digits.replace(/0+$/, '') || '0';
        const tail = digits.substring(1);
        return digits[0] + (tail === '' ? '' : `.${tail}`)
            + `${upper ? 'E' : 'e'}${exponent < 0 ? '-' : '+'}${String(Math.abs(exponent)).padStart(2, '0')}`;
    }
    const digits = rounded.digits;
    if (rounded.decimalPosition <= 0) return `0.${'0'.repeat(-rounded.decimalPosition)}${digits}`;
    if (rounded.decimalPosition >= digits.length) return digits + '0'.repeat(rounded.decimalPosition - digits.length);
    return `${digits.substring(0, rounded.decimalPosition)}.${digits.substring(rounded.decimalPosition)}`;
}

function parseFormat(format) {
    if (format == null || format === '') return { code: 'G', original: 'G', precision: null };
    const match = /^([A-Za-z])(\d*)$/.exec(format);
    if (match === null) return null;
    const precision = match[2] === '' ? null : Number(match[2]);
    if (precision !== null && (!Number.isSafeInteger(precision) || precision > 999999999)) return { invalid: true };
    return { code: match[1].toUpperCase(), original: match[1], precision };
}

function floatingValue(value, typeName) {
    if (typeName === 'System.Half') {
        const bits = Number(fieldValue(value, '_value')) & 0xffff;
        return { value: decodeHalf(bits), halfBits: bits };
    }
    const raw = Number(unwrap(value));
    return { value: typeName === 'System.Single' ? Math.fround(raw) : raw, halfBits: null };
}

function formatStandard(buffer, parsed, typeName, isDecimal) {
    if (parsed.invalid) return null;
    const precision = parsed.precision;
    const upper = parsed.original === parsed.original.toUpperCase();
    const sign = buffer.negative ? '-' : '';
    switch (parsed.code) {
        case 'C': {
            const body = fixed(buffer, precision ?? 2, true, 0, !isDecimal);
            return buffer.negative ? `(¤${body})` : `¤${body}`;
        }
        case 'E': return sign + exponential(buffer, precision ?? 6, upper);
        case 'F': return sign + fixed(buffer, precision ?? 2, false, 0, !isDecimal);
        case 'N': return sign + fixed(buffer, precision ?? 2, true, 0, !isDecimal);
        case 'P': return sign + fixed(buffer, precision ?? 2, true, 2, !isDecimal) + ' %';
        case 'G':
        case 'R': {
            const defaultPrecision = isDecimal ? 29 : typePrecision[typeName].roundTrip;
            const requested = precision == null || precision === 0 ? null : precision;
            return sign + general(buffer, requested, upper, isDecimal && requested == null, defaultPrecision);
        }
        default: return null;
    }
}

export function formatReal(value, format, typeName) {
    const isDecimal = typeName === 'System.Decimal';
    let buffer;
    let numericValue = null;
    let halfBits = null;
    if (isDecimal) buffer = decimalBuffer(value);
    else {
        ({ value: numericValue, halfBits } = floatingValue(value, typeName));
        if (Number.isNaN(numericValue)) return 'NaN';
        if (numericValue === Infinity) return 'Infinity';
        if (numericValue === -Infinity) return '-Infinity';
    }
    const parsed = parseFormat(format);
    if (parsed !== null) {
        if (!isDecimal) {
            const requested = parsed.precision == null || parsed.precision === 0
                ? typePrecision[typeName].roundTrip
                : parsed.precision;
            const needsShortest = (parsed.code === 'G' || parsed.code === 'R')
                && (parsed.precision == null || parsed.precision === 0);
            buffer = needsShortest
                ? shortestFloating(numericValue, typeName, halfBits)
                : preciseFloating(Math.abs(numericValue), Math.max(requested + 2, typePrecision[typeName].roundTrip + 2));
            buffer.negative = numericValue < 0 || Object.is(numericValue, -0);
        }
        return formatStandard(buffer, parsed, typeName, isDecimal);
    }

    if (!isDecimal) {
        const precision = typePrecision[typeName].custom;
        buffer = preciseFloating(Math.abs(numericValue), precision);
        buffer.negative = numericValue < 0 || Object.is(numericValue, -0);
    }
    const coefficient = BigInt(buffer.digits);
    const scale = buffer.digits.length - buffer.decimalPosition;
    return formatCustomNumber(coefficient, scale, buffer.negative, format);
}
