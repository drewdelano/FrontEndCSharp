import { invokeManagedVirtualMethod } from '../../jit-engine/calls.js';
import { deRef, isTypeAssignableTo, unwrap, wrap } from '../../msil-type-helpers.js';

const integralTypes = Object.freeze({
    'System.SByte': { bits: 8, signed: true },
    'System.Byte': { bits: 8, signed: false },
    'System.Int16': { bits: 16, signed: true },
    'System.UInt16': { bits: 16, signed: false },
    'System.Int32': { bits: 32, signed: true },
    'System.UInt32': { bits: 32, signed: false },
    'System.Int64': { bits: 64, signed: true },
    'System.UInt64': { bits: 64, signed: false },
    'System.IntPtr': { bits: 32, signed: true },
    'System.UIntPtr': { bits: 32, signed: false },
    'System.Int128': { bits: 128, signed: true },
    'System.UInt128': { bits: 128, signed: false }
});

const intrinsicFormattableTypes = new Set([
    ...Object.keys(integralTypes),
    'System.Half', 'System.Single', 'System.Double', 'System.Decimal',
    'System.DateTime', 'System.DateTimeOffset', 'System.DateOnly',
    'System.TimeOnly', 'System.TimeSpan', 'System.Guid', 'System.Enum'
]);

function fieldValue(value, name) {
    const raw = unwrap(value);
    if (raw == null || typeof raw !== 'object') return undefined;
    return unwrap(raw[name] ?? raw.v?.[name]);
}

function toIntegralBigInt(value, typeName) {
    const type = integralTypes[typeName];
    if (type === undefined) return null;
    const raw = unwrap(value);
    let result;
    if (typeof raw === 'bigint') result = raw;
    else if (typeof raw === 'number' && Number.isInteger(raw)) result = BigInt(raw);
    else {
        const lowerValue = fieldValue(raw, '_lower');
        const upperValue = fieldValue(raw, '_upper');
        if (lowerValue === undefined || upperValue === undefined) return null;
        result = (BigInt(upperValue) << 64n) | (BigInt(lowerValue) & ((1n << 64n) - 1n));
        if (type.signed && (result & (1n << 127n)) !== 0n) result -= 1n << 128n;
    }
    return type.signed
        ? BigInt.asIntN(type.bits, result)
        : BigInt.asUintN(type.bits, result);
}

function parseStandardFormat(format) {
    if (format == null || format === '') return { specifier: 'G', precision: null };
    const match = /^([A-Za-z])(\d*)$/.exec(format);
    if (match === null) return null;
    const precision = match[2] === '' ? null : Number(match[2]);
    if (precision !== null && (!Number.isSafeInteger(precision) || precision > 999999999)) return null;
    return { specifier: match[1], precision };
}

function groupDigits(digits) {
    return digits.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

function roundDigits(digits, significantDigits) {
    if (digits.length <= significantDigits) return { digits, carry: false };
    let kept = digits.substring(0, significantDigits);
    if (digits.charCodeAt(significantDigits) >= 53) {
        const rounded = (BigInt(kept) + 1n).toString();
        if (rounded.length > significantDigits) return { digits: rounded.substring(0, significantDigits), carry: true };
        kept = rounded.padStart(significantDigits, '0');
    }
    return { digits: kept, carry: false };
}

function scientific(digits, decimals, upper, exponentDigits) {
    const rounded = roundDigits(digits, decimals + 1);
    const exponent = digits.length - 1 + (rounded.carry ? 1 : 0);
    const coefficient = rounded.digits.padEnd(decimals + 1, '0');
    const mantissa = decimals === 0 ? coefficient[0] : `${coefficient[0]}.${coefficient.substring(1)}`;
    return `${mantissa}${upper ? 'E' : 'e'}+${String(exponent).padStart(exponentDigits, '0')}`;
}

function fixed(absValue, decimals, grouped) {
    const integer = grouped ? groupDigits(absValue.toString()) : absValue.toString();
    return decimals === 0 ? integer : `${integer}.${'0'.repeat(decimals)}`;
}

function formatStandard(value, type, parsed) {
    const code = parsed.specifier.toUpperCase();
    const upper = parsed.specifier === code;
    const precision = parsed.precision;
    const negative = value < 0n;
    const abs = negative ? -value : value;
    const sign = negative ? '-' : '';
    switch (code) {
        case 'C': {
            const body = fixed(abs, precision ?? 2, true);
            return negative ? `(¤${body})` : `¤${body}`;
        }
        case 'D': return sign + abs.toString().padStart(precision ?? 0, '0');
        case 'E': return sign + scientific(abs.toString(), precision ?? 6, upper, 3);
        case 'F': return sign + fixed(abs, precision ?? 2, false);
        case 'G':
        case 'R': {
            if (precision == null || precision === 0 || abs === 0n) return value.toString();
            const digits = abs.toString();
            return digits.length <= precision ? value.toString() : sign + scientific(digits, precision - 1, upper, 2);
        }
        case 'N': return sign + fixed(abs, precision ?? 2, true);
        case 'P': return sign + fixed(abs * 100n, precision ?? 2, true) + ' %';
        case 'X':
        case 'B': {
            const radix = code === 'X' ? 16 : 2;
            let encoded = value;
            if (encoded < 0n) encoded += 1n << BigInt(type.bits);
            let digits = encoded.toString(radix).padStart(precision ?? 0, '0');
            if (code === 'X' && upper) digits = digits.toUpperCase();
            return digits;
        }
        default: return null;
    }
}

function splitSections(format) {
    const sections = [''];
    let quote = null;
    for (let index = 0; index < format.length; index++) {
        const ch = format[index];
        if (ch === '\\' && index + 1 < format.length) sections[sections.length - 1] += ch + format[++index];
        else if (quote !== null) {
            sections[sections.length - 1] += ch;
            if (ch === quote) quote = null;
        } else if (ch === '"' || ch === "'") {
            quote = ch;
            sections[sections.length - 1] += ch;
        } else if (ch === ';' && sections.length < 3) sections.push('');
        else sections[sections.length - 1] += ch;
    }
    return sections;
}

function tokenizeCustom(section) {
    const tokens = [];
    for (let index = 0; index < section.length; index++) {
        const ch = section[index];
        if (ch === '\\') {
            if (++index >= section.length) return null;
            tokens.push({ kind: 'literal', text: section[index] });
        } else if (ch === '"' || ch === "'") {
            const quote = ch;
            let text = '';
            while (++index < section.length && section[index] !== quote) text += section[index];
            if (index >= section.length) return null;
            if (text !== '') tokens.push({ kind: 'literal', text });
        } else if (ch === '0' || ch === '#') tokens.push({ kind: 'digit', required: ch === '0' });
        else if (ch === '.') tokens.push({ kind: 'decimal' });
        else if (ch === ',') tokens.push({ kind: 'comma' });
        else if (ch === '%') tokens.push({ kind: 'percent' });
        else if (ch === '‰') tokens.push({ kind: 'permille' });
        else if (ch === 'E' || ch === 'e') {
            const match = /^([Ee])([+-]?)(0+)/.exec(section.substring(index));
            if (match === null) tokens.push({ kind: 'literal', text: ch });
            else {
                tokens.push({ kind: 'exponent', upper: match[1] === 'E', sign: match[2], digits: match[3].length });
                index += match[0].length - 1;
            }
        } else tokens.push({ kind: 'literal', text: ch });
    }
    return tokens;
}

function renderLiteral(token) {
    if (token?.kind === 'literal') return token.text;
    if (token?.kind === 'percent') return '%';
    if (token?.kind === 'permille') return '‰';
    return '';
}

function scaledInteger(abs, decimalShift, fractionalDigits) {
    const power = fractionalDigits + decimalShift;
    if (power >= 0) return abs * (10n ** BigInt(power));
    const divisor = 10n ** BigInt(-power);
    let quotient = abs / divisor;
    if ((abs % divisor) * 2n >= divisor) quotient++;
    return quotient;
}

function renderFixedCustom(tokens, abs, negative, automaticSign, intrinsicScale = 0) {
    const digitIndexes = [];
    for (let index = 0; index < tokens.length; index++) if (tokens[index].kind === 'digit') digitIndexes.push(index);
    if (digitIndexes.length === 0) return (negative && automaticSign ? '-' : '') + tokens.map(renderLiteral).join('');
    const firstDigit = digitIndexes[0];
    const lastDigit = digitIndexes[digitIndexes.length - 1];
    const decimalIndex = tokens.findIndex(token => token.kind === 'decimal');
    const integerEnd = decimalIndex < 0 ? lastDigit + 1 : decimalIndex;
    const integerIndexes = digitIndexes.filter(index => index < integerEnd);
    const fractionIndexes = digitIndexes.filter(index => index > decimalIndex && decimalIndex >= 0);

    let scaleCommas = 0;
    const lastIntegerDigit = integerIndexes[integerIndexes.length - 1];
    for (let index = lastIntegerDigit + 1; index < tokens.length && tokens[index].kind === 'comma'; index++) scaleCommas++;
    const percentCount = tokens.filter(token => token.kind === 'percent').length;
    const permilleCount = tokens.filter(token => token.kind === 'permille').length;
    const decimalShift = percentCount * 2 + permilleCount * 3 - scaleCommas * 3 - intrinsicScale;
    let digits = scaledInteger(abs, decimalShift, fractionIndexes.length).toString()
        .padStart(fractionIndexes.length + 1, '0');
    let integerDigits = digits.substring(0, digits.length - fractionIndexes.length);
    let fractionDigits = fractionIndexes.length === 0 ? '' : digits.substring(digits.length - fractionIndexes.length);
    const minimumInteger = integerIndexes.reduce((count, index) => count + (tokens[index].required ? 1 : 0), 0);
    if (integerDigits === '0' && minimumInteger === 0) integerDigits = '';
    integerDigits = integerDigits.padStart(minimumInteger, '0');
    let fractionLength = fractionDigits.length;
    while (fractionLength > 0 && fractionDigits[fractionLength - 1] === '0'
        && !tokens[fractionIndexes[fractionLength - 1]].required) fractionLength--;
    fractionDigits = fractionDigits.substring(0, fractionLength);

    const grouping = scaleCommas === 0 && tokens.slice(firstDigit, integerEnd).some(token => token.kind === 'comma');
    const output = new Array(tokens.length).fill('');
    for (let index = 0; index < firstDigit; index++) output[index] = renderLiteral(tokens[index]);
    for (let index = lastDigit + 1; index < tokens.length; index++) output[index] = renderLiteral(tokens[index]);
    if (grouping) {
        output[firstDigit] = groupDigits(integerDigits);
    } else {
        let cursor = integerDigits.length;
        for (let index = integerEnd - 1; index >= firstDigit; index--) {
            const token = tokens[index];
            if (token.kind === 'digit') {
                if (cursor > 0) output[index] = integerDigits[--cursor];
                else if (token.required) output[index] = '0';
            } else if (token.kind !== 'comma') output[index] = renderLiteral(token);
        }
        if (cursor > 0) output[firstDigit] = integerDigits.substring(0, cursor) + output[firstDigit];
    }
    if (decimalIndex >= 0 && fractionDigits.length > 0) output[decimalIndex] = '.';
    let cursor = 0;
    for (let index = decimalIndex + 1; decimalIndex >= 0 && index <= lastDigit; index++) {
        const token = tokens[index];
        if (token.kind === 'digit') {
            if (cursor < fractionDigits.length) output[index] = fractionDigits[cursor++];
            else if (token.required) output[index] = '0';
        } else output[index] = renderLiteral(token);
    }
    return (negative && automaticSign ? '-' : '') + output.join('');
}

function renderScientificCustom(tokens, abs, negative, automaticSign, intrinsicScale = 0) {
    const exponentIndex = tokens.findIndex(token => token.kind === 'exponent');
    const exponentToken = tokens[exponentIndex];
    const coefficientTokens = tokens.slice(0, exponentIndex);
    const firstDigit = coefficientTokens.findIndex(token => token.kind === 'digit');
    const decimalIndex = coefficientTokens.findIndex(token => token.kind === 'decimal');
    const integerCount = coefficientTokens.filter((token, index) => token.kind === 'digit' && (decimalIndex < 0 || index < decimalIndex)).length;
    const fractionTokens = coefficientTokens.filter((token, index) => token.kind === 'digit' && decimalIndex >= 0 && index > decimalIndex);
    const significant = Math.max(1, integerCount + fractionTokens.length);
    const sourceDigits = abs.toString();
    const rounded = roundDigits(sourceDigits, significant);
    const exponent = abs === 0n ? 0 : sourceDigits.length - intrinsicScale
        - Math.max(1, integerCount) + (rounded.carry ? 1 : 0);
    const coefficient = rounded.digits.padEnd(significant, '0');
    let body = coefficient.substring(0, Math.max(1, integerCount));
    let fraction = coefficient.substring(Math.max(1, integerCount));
    while (fraction.length > 0 && fraction.endsWith('0') && !fractionTokens[fraction.length - 1]?.required) fraction = fraction.slice(0, -1);
    if (fraction.length > 0) body += '.' + fraction;
    const prefix = coefficientTokens.slice(0, firstDigit).map(renderLiteral).join('');
    let lastCoefficientDigit = -1;
    for (let index = coefficientTokens.length - 1; index >= 0; index--) if (coefficientTokens[index].kind === 'digit') { lastCoefficientDigit = index; break; }
    const suffix = coefficientTokens.slice(lastCoefficientDigit + 1).map(renderLiteral).join('');
    const exponentSign = exponent < 0 ? '-' : (exponentToken.sign === '+' ? '+' : '');
    return (negative && automaticSign ? '-' : '') + prefix + body + suffix
        + (exponentToken.upper ? 'E' : 'e') + exponentSign
        + String(Math.abs(exponent)).padStart(exponentToken.digits, '0')
        + tokens.slice(exponentIndex + 1).map(renderLiteral).join('');
}

function formatCustom(value, format, intrinsicScale = 0, negativeZero = false) {
    const sections = splitSections(format);
    const negative = value < 0n || negativeZero;
    const sectionIndex = value === 0n && sections.length === 3 ? 2 : (negative && sections.length >= 2 ? 1 : 0);
    const tokens = tokenizeCustom(sections[sectionIndex]);
    if (tokens === null) return null;
    const abs = negative ? -value : value;
    return tokens.some(token => token.kind === 'exponent')
        ? renderScientificCustom(tokens, abs, negative, negative && sectionIndex === 0, intrinsicScale)
        : renderFixedCustom(tokens, abs, negative, negative && sectionIndex === 0, intrinsicScale);
}

export function formatCustomNumber(coefficient, scale, negative, format) {
    const magnitude = BigInt(coefficient);
    return formatCustom(negative && magnitude !== 0n ? -magnitude : magnitude, format, scale, negative);
}

export function formatIntegral(value, format, typeName) {
    const type = integralTypes[typeName];
    const integral = toIntegralBigInt(value, typeName);
    if (type === undefined || integral === null) return null;
    const parsed = parseStandardFormat(format);
    return parsed === null ? formatCustom(integral, format) : formatStandard(integral, type, parsed);
}

export function formatInt32(value, format) {
    return formatIntegral(value, format, 'System.Int32');
}

export function formatGuid(value, format) {
    const text = String(unwrap(value)).toLowerCase();
    const match = /^([0-9a-f]{8})-([0-9a-f]{4})-([0-9a-f]{4})-([0-9a-f]{4})-([0-9a-f]{12})$/.exec(text);
    if (match === null) return null;
    const specifier = format == null || format === '' ? 'D' : format.toUpperCase();
    const compact = match.slice(1).join('');
    switch (specifier) {
        case 'D': return text;
        case 'N': return compact;
        case 'B': return `{${text}}`;
        case 'P': return `(${text})`;
        case 'X': {
            const bytes = compact.substring(16).match(/../g);
            return `{0x${match[1]},0x${match[2]},0x${match[3]},{${bytes.map(value => `0x${value}`).join(',')}}}`;
        }
        default: return null;
    }
}

export function formatInterpolatedValue(ctx, value, format = undefined) {
    let receiver = deRef(value);
    const raw = unwrap(receiver);
    if (raw == null) return '';
    if (receiver?.t === 'System.Object' && typeof raw === 'object'
        && typeof raw?.t === 'string' && Object.prototype.hasOwnProperty.call(raw, 'v')) receiver = raw;
    const genericType = typeof ctx.genericParams?.T === 'string'
        ? ctx.genericParams.T
        : ctx.genericParams?.T?.typeName;
    if (typeof globalThis.DotNet?.Types?.[genericType]?.enumUnderlyingType === 'string'
        && receiver?.t !== genericType) {
        receiver = wrap(raw, genericType);
    }
    let receiverType = genericType
        ?? (receiver?.t === '$NativeInt32' ? 'System.IntPtr'
            : receiver?.t === '$NativeUInt32' ? 'System.UIntPtr' : receiver?.t);
    if (typeof globalThis.DotNet?.Types?.[receiverType]?.enumUnderlyingType === 'string') {
        receiverType = 'System.Enum';
    }
    const isFormattable = isTypeAssignableTo(receiverType, 'System.IFormattable');
    const formattedSignature = `${receiverType}.ToString(System.String, System.IFormatProvider)`;
    const intrinsicFormatter = intrinsicFormattableTypes.has(receiverType)
        ? globalThis.DotNet.Methods[formattedSignature]
        : undefined;
    if (format !== undefined && typeof intrinsicFormatter === 'function') {
        return unwrap(intrinsicFormatter(ctx, receiver, format, null)) ?? '';
    }
    if (format !== undefined && isFormattable) {
        return unwrap(invokeManagedVirtualMethod(
            ctx,
            receiver,
            'System.IFormattable',
            'ToString(System.String, System.IFormatProvider)',
            ['System.String', 'System.IFormatProvider'],
            [format, null])) ?? '';
    }
    return unwrap(invokeManagedVirtualMethod(
        ctx,
        receiver,
        'System.Object',
        'ToString()',
        [],
        [])) ?? '';
}
