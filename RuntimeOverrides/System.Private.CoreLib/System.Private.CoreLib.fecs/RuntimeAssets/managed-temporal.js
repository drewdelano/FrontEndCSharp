import { deRef, unwrap, wrap } from '../../msil-type-helpers.js';

const ticksPerMillisecond = 10000n;
const ticksPerSecond = 10000000n;
const ticksPerMinute = 600000000n;
const ticksPerHour = 36000000000n;
const ticksPerDay = 864000000000n;
const dateTimeTicksMask = 0x3fffffffffffffffn;
const dateTimeKindUtc = 0x4000000000000000n;
const dateTimeKindLocal = 0x8000000000000000n;
const unixEpochTicks = 621355968000000000n;
const daysToMonth365 = Object.freeze([0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365]);
const daysToMonth366 = Object.freeze([0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366]);
const abbreviatedDayNames = Object.freeze(['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']);
const dayNames = Object.freeze(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']);
const abbreviatedMonthNames = Object.freeze(['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']);
const monthNames = Object.freeze(['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']);
const temporalOverflow = Symbol('temporal-overflow');
const standardDateTimePatterns = Object.freeze({
    d: 'MM/dd/yyyy',
    D: 'dddd, dd MMMM yyyy',
    f: 'dddd, dd MMMM yyyy HH:mm',
    F: 'dddd, dd MMMM yyyy HH:mm:ss',
    g: 'MM/dd/yyyy HH:mm',
    G: 'MM/dd/yyyy HH:mm:ss',
    m: 'MMMM dd',
    M: 'MMMM dd',
    s: "yyyy-MM-dd'T'HH:mm:ss",
    t: 'HH:mm',
    T: 'HH:mm:ss',
    y: 'yyyy MMMM',
    Y: 'yyyy MMMM'
});

function isLeapYear(year) {
    return year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
}

function dayNumber(year, month, day) {
    if (!Number.isInteger(year) || year < 1 || year > 9999
        || !Number.isInteger(month) || month < 1 || month > 12) return null;
    const days = isLeapYear(year) ? daysToMonth366 : daysToMonth365;
    if (!Number.isInteger(day) || day < 1 || day > days[month] - days[month - 1]) return null;
    const previousYear = year - 1;
    return previousYear * 365
        + Math.floor(previousYear / 4)
        - Math.floor(previousYear / 100)
        + Math.floor(previousYear / 400)
        + days[month - 1]
        + day - 1;
}

function dateFromDayNumber(value) {
    if (!Number.isInteger(value) || value < 0 || value > 3652058) return null;
    let low = 1;
    let high = 9999;
    while (low < high) {
        const middle = Math.ceil((low + high) / 2);
        if (dayNumber(middle, 1, 1) <= value) low = middle;
        else high = middle - 1;
    }
    const year = low;
    const dayOfYear = value - dayNumber(year, 1, 1);
    const days = isLeapYear(year) ? daysToMonth366 : daysToMonth365;
    let month = 1;
    while (month < 12 && days[month] <= dayOfYear) month++;
    return { year, month, day: dayOfYear - days[month - 1] + 1 };
}

function pad(value, width) {
    return String(value).padStart(width, '0');
}

function fractionTicks(text) {
    return BigInt((text ?? '').padEnd(7, '0') || '0');
}

function ticksFromParts(year, month, day, hour, minute, second, fraction) {
    const days = dayNumber(year, month, day);
    if (days === null || hour < 0 || hour > 23 || minute < 0 || minute > 59
        || second < 0 || second > 59) return null;
    return BigInt(days) * ticksPerDay
        + BigInt(hour) * ticksPerHour
        + BigInt(minute) * ticksPerMinute
        + BigInt(second) * ticksPerSecond
        + fractionTicks(fraction);
}

function partsFromTicks(ticks) {
    if (ticks < 0n || ticks > 3155378975999999999n) return null;
    const days = Number(ticks / ticksPerDay);
    const date = dateFromDayNumber(days);
    if (date === null) return null;
    let remainder = ticks % ticksPerDay;
    const hour = Number(remainder / ticksPerHour);
    remainder %= ticksPerHour;
    const minute = Number(remainder / ticksPerMinute);
    remainder %= ticksPerMinute;
    const second = Number(remainder / ticksPerSecond);
    const fraction = remainder % ticksPerSecond;
    return { ...date, hour, minute, second, fraction };
}

function field(raw, name) {
    return unwrap(raw?.[name]);
}

function temporalRaw(value) {
    return unwrap(deRef(value));
}

function dateTimeValue(ticks, kindBits = 0n) {
    return wrap({ _dateData: wrap(ticks | kindBits, 'System.UInt64') }, 'System.DateTime');
}

function dateTimeOffsetValue(utcTicks, offsetMinutes) {
    return wrap({
        _dateTime: dateTimeValue(utcTicks),
        _offsetMinutes: wrap(offsetMinutes, 'System.Int16')
    }, 'System.DateTimeOffset');
}

function currentClock() {
    const instant = new Date();
    return {
        utcTicks: unixEpochTicks + BigInt(instant.getTime()) * ticksPerMillisecond,
        offsetMinutes: -instant.getTimezoneOffset()
    };
}

export function getDateTimeNow() {
    const clock = currentClock();
    return dateTimeValue(
        clock.utcTicks + BigInt(clock.offsetMinutes) * ticksPerMinute,
        dateTimeKindLocal);
}

export function getDateTimeToday() {
    const clock = currentClock();
    const localTicks = clock.utcTicks + BigInt(clock.offsetMinutes) * ticksPerMinute;
    return dateTimeValue(localTicks - localTicks % ticksPerDay, dateTimeKindLocal);
}

export function getDateTimeUtcNow() {
    return dateTimeValue(currentClock().utcTicks, dateTimeKindUtc);
}

export function getDateTimeOffsetNow() {
    const clock = currentClock();
    return dateTimeOffsetValue(clock.utcTicks, clock.offsetMinutes);
}

export function getDateTimeOffsetUtcNow() {
    return dateTimeOffsetValue(currentClock().utcTicks, 0);
}

function formatFraction(fraction, fixedWidth) {
    if (fraction === 0n) return '';
    const digits = pad(fraction, 7);
    return `.${fixedWidth ? digits : digits.replace(/0+$/, '')}`;
}

function formatDateTimeParts(parts, includeDate, fixedFraction = false) {
    const time = `${pad(parts.hour, 2)}:${pad(parts.minute, 2)}:${pad(parts.second, 2)}`
        + formatFraction(parts.fraction, fixedFraction);
    return includeDate
        ? `${pad(parts.year, 4)}-${pad(parts.month, 2)}-${pad(parts.day, 2)}T${time}`
        : time;
}

function localOffsetMinutes(parts) {
    const date = new Date(0);
    date.setFullYear(parts.year, parts.month - 1, parts.day);
    date.setHours(parts.hour, parts.minute, parts.second, Number(parts.fraction / ticksPerMillisecond));
    return -date.getTimezoneOffset();
}

function formatOffset(minutes) {
    const sign = minutes < 0 ? '-' : '+';
    const absolute = Math.abs(minutes);
    return `${sign}${pad(Math.floor(absolute / 60), 2)}:${pad(absolute % 60, 2)}`;
}

function parseDateTime(text) {
    let match = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?(Z|[+-]\d{2}:\d{2})?$/.exec(text);
    if (match === null) {
        match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})(?:\s+(\d{1,2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?\s*(AM|PM)?)?$/.exec(text);
        if (match === null) return null;
        const hour = parseInvariantHour(match[4], match[8]);
        if (hour === null) return null;
        const ticks = ticksFromParts(
            Number(match[3]), Number(match[1]), Number(match[2]),
            hour, Number(match[5] ?? 0), Number(match[6] ?? 0), match[7]);
        return ticks === null ? null : dateTimeValue(ticks);
    }
    let ticks = ticksFromParts(
        Number(match[1]), Number(match[2]), Number(match[3]),
        Number(match[4]), Number(match[5]), Number(match[6] ?? 0), match[7]);
    if (ticks === null) return null;
    if (match[8] === 'Z') return dateTimeValue(ticks, dateTimeKindUtc);
    if (match[8]) {
        const offset = parseOffset(match[8]);
        if (offset === null) return null;
        ticks -= BigInt(offset) * ticksPerMinute;
        if (partsFromTicks(ticks) === null) return null;
        const instant = new Date(Number((ticks - unixEpochTicks) / ticksPerMillisecond));
        const localTicks = ticks - BigInt(instant.getTimezoneOffset()) * ticksPerMinute;
        return partsFromTicks(localTicks) === null ? null : dateTimeValue(localTicks, dateTimeKindLocal);
    }
    return dateTimeValue(ticks);
}

function parseInvariantHour(text, designator) {
    if (text === undefined) return 0;
    let hour = Number(text);
    if (!Number.isInteger(hour)) return null;
    if (designator === undefined) return hour >= 0 && hour <= 23 ? hour : null;
    if (hour < 1 || hour > 12) return null;
    if (designator === 'AM') return hour === 12 ? 0 : hour;
    return hour === 12 ? 12 : hour + 12;
}

function parseOffset(text) {
    if (text === 'Z') return 0;
    const match = /^([+-])(\d{2}):(\d{2})$/.exec(text);
    if (match === null) return null;
    const hours = Number(match[2]);
    const minutes = Number(match[3]);
    if (hours > 14 || minutes > 59 || (hours === 14 && minutes !== 0)) return null;
    return (match[1] === '-' ? -1 : 1) * (hours * 60 + minutes);
}

function parseDateTimeOffset(text) {
    let match = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?(Z|[+-]\d{2}:\d{2})$/.exec(text);
    let yearIndex = 1;
    let monthIndex = 2;
    let dayIndex = 3;
    if (match === null) {
        match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})\s+(\d{1,2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?\s*(Z|[+-]\d{2}:\d{2})$/.exec(text);
        if (match === null) return null;
        yearIndex = 3;
        monthIndex = 1;
        dayIndex = 2;
    }
    const clockTicks = ticksFromParts(
        Number(match[yearIndex]), Number(match[monthIndex]), Number(match[dayIndex]),
        Number(match[4]), Number(match[5]), Number(match[6] ?? 0), match[7]);
    const offset = parseOffset(match[8]);
    if (clockTicks === null || offset === null) return null;
    const utcTicks = clockTicks - BigInt(offset) * ticksPerMinute;
    if (partsFromTicks(utcTicks) === null) return null;
    return wrap({
        _dateTime: dateTimeValue(utcTicks),
        _offsetMinutes: wrap(offset, 'System.Int16')
    }, 'System.DateTimeOffset');
}

function parseDateOnly(text) {
    let match = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(text);
    if (match === null) {
        match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(text);
        if (match === null) return null;
        const value = dayNumber(Number(match[3]), Number(match[1]), Number(match[2]));
        return value === null ? null : wrap({ _dayNumber: wrap(value, 'System.Int32') }, 'System.DateOnly');
    }
    const value = dayNumber(Number(match[1]), Number(match[2]), Number(match[3]));
    return value === null ? null : wrap({ _dayNumber: wrap(value, 'System.Int32') }, 'System.DateOnly');
}

function parseTimeOnly(text) {
    const match = /^(\d{1,2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?\s*(AM|PM)?$/.exec(text);
    if (match === null) return null;
    const hour = parseInvariantHour(match[1], match[5]);
    if (hour === null) return null;
    const ticks = ticksFromParts(1, 1, 1, hour, Number(match[2]), Number(match[3] ?? 0), match[4]);
    return ticks === null ? null : wrap({ _ticks: wrap(ticks, 'System.Int64') }, 'System.TimeOnly');
}

function parseTimeSpan(text) {
    const signMatch = /^(-)?(.*)$/.exec(text);
    const negative = signMatch[1] !== undefined;
    const unsigned = signMatch[2];
    let match = /^(\d+)\.(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(unsigned);
    let days;
    let hours;
    let minutes;
    let seconds;
    let fraction;
    if (match !== null) {
        days = BigInt(match[1]);
        hours = Number(match[2]);
        minutes = Number(match[3]);
        seconds = Number(match[4]);
        fraction = match[5];
    } else {
        match = /^(\d+):(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(unsigned);
        if (match !== null) {
            days = BigInt(match[1]);
            hours = Number(match[2]);
            minutes = Number(match[3]);
            seconds = Number(match[4]);
            fraction = match[5];
        } else {
            match = /^(\d+):(\d{1,2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(unsigned);
            if (match !== null) {
                const first = BigInt(match[1]);
                if (first <= 23n) {
                    days = 0n;
                    hours = Number(first);
                    minutes = Number(match[2]);
                    seconds = Number(match[3]);
                    fraction = match[4];
                } else {
                    days = first;
                    hours = Number(match[2]);
                    minutes = Number(match[3]);
                    seconds = 0;
                    fraction = undefined;
                }
            } else {
                match = /^(\d+):(\d{1,2})$/.exec(unsigned);
                if (match !== null) {
                    days = 0n;
                    hours = Number(match[1]);
                    minutes = Number(match[2]);
                    seconds = 0;
                    fraction = undefined;
                } else if (/^\d+$/.test(unsigned)) {
                    days = BigInt(unsigned);
                    hours = 0;
                    minutes = 0;
                    seconds = 0;
                    fraction = undefined;
                } else return null;
            }
        }
    }
    if (hours > 23 || minutes > 59 || seconds > 59) return null;
    let ticks = days * ticksPerDay
        + BigInt(hours) * ticksPerHour
        + BigInt(minutes) * ticksPerMinute
        + BigInt(seconds) * ticksPerSecond
        + fractionTicks(fraction);
    if (negative) ticks = -ticks;
    if (ticks < -9223372036854775808n || ticks > 9223372036854775807n) return temporalOverflow;
    return wrap({ _ticks: wrap(ticks, 'System.Int64') }, 'System.TimeSpan');
}

function parseManagedTemporalCore(typeName, text) {
    if (typeof text !== 'string') return null;
    switch (typeName) {
        case 'System.DateTime': return parseDateTime(text);
        case 'System.DateTimeOffset': return parseDateTimeOffset(text);
        case 'System.DateOnly': return parseDateOnly(text);
        case 'System.TimeOnly': return parseTimeOnly(text);
        case 'System.TimeSpan': return parseTimeSpan(text);
        default: return null;
    }
}

export function parseManagedTemporal(typeName, text) {
    const result = parseManagedTemporalCore(typeName, text);
    return result === temporalOverflow ? null : result;
}

export function parseManagedTemporalResult(typeName, text) {
    const result = parseManagedTemporalCore(typeName, text);
    if (result === temporalOverflow) return { status: 'overflow', value: null };
    return result === null
        ? { status: 'format', value: null }
        : { status: 'success', value: result };
}

export function formatManagedTemporal(typeName, value) {
    const raw = temporalRaw(value);
    switch (typeName) {
        case 'System.DateTime': {
            const data = BigInt(field(raw, '_dateData'));
            const kind = data & ~dateTimeTicksMask;
            const parts = partsFromTicks(data & dateTimeTicksMask);
            if (parts === null) return null;
            if (kind === dateTimeKindUtc) return `${formatDateTimeParts(parts, true)}Z`;
            if (kind === dateTimeKindLocal || kind === 0xc000000000000000n) {
                return `${formatDateTimeParts(parts, true)}${formatOffset(localOffsetMinutes(parts))}`;
            }
            return formatDateTimeParts(parts, true);
        }
        case 'System.DateTimeOffset': {
            const dateTime = field(raw, '_dateTime');
            const utcTicks = BigInt(field(unwrap(dateTime), '_dateData')) & dateTimeTicksMask;
            const offset = Number(field(raw, '_offsetMinutes'));
            const parts = partsFromTicks(utcTicks + BigInt(offset) * ticksPerMinute);
            return parts === null ? null : `${formatDateTimeParts(parts, true)}${formatOffset(offset)}`;
        }
        case 'System.DateOnly': {
            const parts = dateFromDayNumber(Number(field(raw, '_dayNumber')));
            return parts === null
                ? null
                : `${pad(parts.year, 4)}-${pad(parts.month, 2)}-${pad(parts.day, 2)}`;
        }
        case 'System.TimeOnly': {
            const parts = partsFromTicks(BigInt(field(raw, '_ticks')));
            return parts === null ? null : formatDateTimeParts(parts, false);
        }
        case 'System.TimeSpan': {
            let ticks = BigInt(field(raw, '_ticks'));
            const negative = ticks < 0n;
            if (negative) ticks = -ticks;
            const days = ticks / ticksPerDay;
            let remainder = ticks % ticksPerDay;
            const hours = remainder / ticksPerHour;
            remainder %= ticksPerHour;
            const minutes = remainder / ticksPerMinute;
            remainder %= ticksPerMinute;
            const seconds = remainder / ticksPerSecond;
            const fraction = remainder % ticksPerSecond;
            return `${negative ? '-' : ''}${days === 0n ? '' : `${days}.`}`
                + `${pad(hours, 2)}:${pad(minutes, 2)}:${pad(seconds, 2)}`
                + formatFraction(fraction, true);
        }
        default: return null;
    }
}

export function formatDateTimeDefault(value) {
    return formatTemporal('System.DateTime', value, null);
}

export function formatDateTime(value, format) {
    return formatTemporal('System.DateTime', value, format);
}

function temporalDescriptor(typeName, value) {
    const raw = temporalRaw(value);
    switch (typeName) {
        case 'System.DateTime': {
            const data = BigInt(field(raw, '_dateData'));
            const ticks = data & dateTimeTicksMask;
            const parts = partsFromTicks(ticks);
            return parts === null ? null : {
                typeName,
                parts,
                ticks,
                kind: data & ~dateTimeTicksMask,
                offsetMinutes: null
            };
        }
        case 'System.DateTimeOffset': {
            const dateTime = field(raw, '_dateTime');
            const utcTicks = BigInt(field(unwrap(dateTime), '_dateData')) & dateTimeTicksMask;
            const offsetMinutes = Number(field(raw, '_offsetMinutes'));
            const ticks = utcTicks + BigInt(offsetMinutes) * ticksPerMinute;
            const parts = partsFromTicks(ticks);
            return parts === null ? null : {
                typeName,
                parts,
                ticks,
                utcTicks,
                kind: 0n,
                offsetMinutes
            };
        }
        case 'System.DateOnly': {
            const days = Number(field(raw, '_dayNumber'));
            const date = dateFromDayNumber(days);
            return date === null ? null : {
                typeName,
                parts: { ...date, hour: 0, minute: 0, second: 0, fraction: 0n },
                ticks: BigInt(days) * ticksPerDay,
                kind: 0n,
                offsetMinutes: null
            };
        }
        case 'System.TimeOnly': {
            const ticks = BigInt(field(raw, '_ticks'));
            const parts = partsFromTicks(ticks);
            return parts === null ? null : {
                typeName,
                parts,
                ticks,
                kind: 0n,
                offsetMinutes: null
            };
        }
        default: return null;
    }
}

function dayOfWeek(parts) {
    return (dayNumber(parts.year, parts.month, parts.day) + 1) % 7;
}

function repeatCount(format, index) {
    const character = format[index];
    let count = 1;
    while (index + count < format.length && format[index + count] === character) count++;
    return count;
}

function formatDigits(value, width) {
    return width <= 1 ? String(value) : pad(value, width);
}

function parseQuotedLiteral(format, index) {
    const quote = format[index];
    let value = '';
    let cursor = index + 1;
    while (cursor < format.length) {
        const character = format[cursor];
        if (character === quote) return { value, length: cursor - index + 1 };
        if (character === '\\') {
            cursor++;
            if (cursor >= format.length) return null;
            value += format[cursor++];
            continue;
        }
        value += character;
        cursor++;
    }
    return null;
}

function dateTimeOffsetForToken(descriptor) {
    if (descriptor.offsetMinutes !== null) return descriptor.offsetMinutes;
    return localOffsetMinutes(descriptor.parts);
}

function formatDateTimeCustom(descriptor, format) {
    let result = '';
    for (let index = 0; index < format.length;) {
        const character = format[index];
        let count = repeatCount(format, index);
        switch (character) {
            case 'g':
                result += 'A.D.';
                break;
            case 'h': {
                const hour = descriptor.parts.hour % 12 || 12;
                result += formatDigits(hour, Math.min(count, 2));
                break;
            }
            case 'H':
                result += formatDigits(descriptor.parts.hour, Math.min(count, 2));
                break;
            case 'm':
                result += formatDigits(descriptor.parts.minute, Math.min(count, 2));
                break;
            case 's':
                result += formatDigits(descriptor.parts.second, Math.min(count, 2));
                break;
            case 'f':
            case 'F': {
                if (count > 7) return null;
                let digits = pad(descriptor.parts.fraction, 7).slice(0, count);
                if (character === 'F') digits = digits.replace(/0+$/, '');
                if (digits === '' && result.endsWith('.')) result = result.slice(0, -1);
                else result += digits;
                break;
            }
            case 't': {
                const designator = descriptor.parts.hour < 12 ? 'AM' : 'PM';
                result += count === 1 ? designator[0] : designator;
                break;
            }
            case 'd':
                if (count <= 2) result += formatDigits(descriptor.parts.day, count);
                else if (count === 3) result += abbreviatedDayNames[dayOfWeek(descriptor.parts)];
                else result += dayNames[dayOfWeek(descriptor.parts)];
                break;
            case 'M':
                if (count <= 2) result += formatDigits(descriptor.parts.month, count);
                else if (count === 3) result += abbreviatedMonthNames[descriptor.parts.month - 1];
                else result += monthNames[descriptor.parts.month - 1];
                break;
            case 'y':
                if (count <= 2) result += formatDigits(descriptor.parts.year % 100, count);
                else result += pad(descriptor.parts.year, count);
                break;
            case 'z': {
                const offset = dateTimeOffsetForToken(descriptor);
                const sign = offset < 0 ? '-' : '+';
                const absolute = Math.abs(offset);
                const hours = Math.floor(absolute / 60);
                const minutes = absolute % 60;
                result += count === 1
                    ? `${sign}${hours}`
                    : count === 2
                        ? `${sign}${pad(hours, 2)}`
                        : `${sign}${pad(hours, 2)}:${pad(minutes, 2)}`;
                break;
            }
            case 'K':
                count = 1;
                if (descriptor.offsetMinutes !== null) result += formatOffset(descriptor.offsetMinutes);
                else if (descriptor.kind === dateTimeKindUtc) result += 'Z';
                else if (descriptor.kind === dateTimeKindLocal
                    || descriptor.kind === 0xc000000000000000n) {
                    result += formatOffset(localOffsetMinutes(descriptor.parts));
                }
                break;
            case ':':
            case '/':
                count = 1;
                result += character;
                break;
            case '\'':
            case '"': {
                const quoted = parseQuotedLiteral(format, index);
                if (quoted === null) return null;
                result += quoted.value;
                count = quoted.length;
                break;
            }
            case '%':
                if (index + 1 >= format.length || format[index + 1] === '%') return null;
                {
                    const nested = formatDateTimeCustom(descriptor, format[index + 1]);
                    if (nested === null) return null;
                    result += nested;
                }
                count = 2;
                break;
            case '\\':
                if (index + 1 >= format.length) return null;
                result += format[index + 1];
                count = 2;
                break;
            default:
                count = 1;
                result += character;
                break;
        }
        index += count;
    }
    return result;
}

function validateRestrictedFormat(format, rejectedCharacters) {
    for (let index = 0; index < format.length;) {
        const character = format[index];
        if (character === '\\') {
            if (index + 1 >= format.length) return false;
            index += 2;
            continue;
        }
        if (character === '\'' || character === '"') {
            const quoted = parseQuotedLiteral(format, index);
            if (quoted === null) return false;
            index += quoted.length;
            continue;
        }
        if (rejectedCharacters.has(character)) return false;
        index++;
    }
    return true;
}

function utcDescriptor(descriptor) {
    let ticks;
    if (descriptor.offsetMinutes !== null) {
        ticks = descriptor.ticks - BigInt(descriptor.offsetMinutes) * ticksPerMinute;
    } else if (descriptor.kind === dateTimeKindUtc) {
        ticks = descriptor.ticks;
    } else {
        ticks = descriptor.ticks - BigInt(localOffsetMinutes(descriptor.parts)) * ticksPerMinute;
    }
    const parts = partsFromTicks(ticks);
    return parts === null ? null : {
        ...descriptor,
        parts,
        ticks,
        kind: dateTimeKindUtc,
        offsetMinutes: 0
    };
}

function formatRfc1123(descriptor, convertOffset) {
    const target = convertOffset && descriptor.offsetMinutes !== null
        ? utcDescriptor(descriptor)
        : descriptor;
    if (target === null) return null;
    const parts = target.parts;
    return `${abbreviatedDayNames[dayOfWeek(parts)]}, ${pad(parts.day, 2)} `
        + `${abbreviatedMonthNames[parts.month - 1]} ${pad(parts.year, 4)} `
        + `${pad(parts.hour, 2)}:${pad(parts.minute, 2)}:${pad(parts.second, 2)} GMT`;
}

function formatDateTimeStandard(descriptor, format) {
    if (descriptor.typeName === 'System.DateOnly') {
        if (format === 'o' || format === 'O') {
            const parts = descriptor.parts;
            return `${pad(parts.year, 4)}-${pad(parts.month, 2)}-${pad(parts.day, 2)}`;
        }
        if (format === 'r' || format === 'R') {
            const parts = descriptor.parts;
            return `${abbreviatedDayNames[dayOfWeek(parts)]}, ${pad(parts.day, 2)} `
                + `${abbreviatedMonthNames[parts.month - 1]} ${pad(parts.year, 4)}`;
        }
        if (!['d', 'D', 'm', 'M', 'y', 'Y'].includes(format)) return null;
        return formatDateTimeCustom(descriptor, standardDateTimePatterns[format]);
    }

    if (descriptor.typeName === 'System.TimeOnly') {
        if (format === 'o' || format === 'O') {
            const parts = descriptor.parts;
            return `${pad(parts.hour, 2)}:${pad(parts.minute, 2)}:${pad(parts.second, 2)}.`
                + pad(parts.fraction, 7);
        }
        if (format === 'r' || format === 'R') {
            const parts = descriptor.parts;
            return `${pad(parts.hour, 2)}:${pad(parts.minute, 2)}:${pad(parts.second, 2)}`;
        }
        if (format !== 't' && format !== 'T') return null;
        return formatDateTimeCustom(descriptor, standardDateTimePatterns[format]);
    }

    if (format === 'o' || format === 'O') {
        return formatDateTimeCustom(descriptor, "yyyy-MM-dd'T'HH:mm:ss.fffffffK");
    }
    if (format === 'r' || format === 'R') return formatRfc1123(descriptor, true);
    if (format === 'u') {
        const target = descriptor.offsetMinutes === null ? descriptor : utcDescriptor(descriptor);
        if (target === null) return null;
        return formatDateTimeCustom(target, "yyyy-MM-dd HH:mm:ss'Z'");
    }
    if (format === 'U') {
        if (descriptor.typeName === 'System.DateTimeOffset') return null;
        const target = utcDescriptor(descriptor);
        return target === null
            ? null
            : formatDateTimeCustom(target, standardDateTimePatterns.F);
    }
    const pattern = standardDateTimePatterns[format];
    return pattern === undefined ? null : formatDateTimeCustom(descriptor, pattern);
}

function timeSpanParts(value) {
    let ticks = BigInt(field(temporalRaw(value), '_ticks'));
    const negative = ticks < 0n;
    if (negative) ticks = -ticks;
    const days = ticks / ticksPerDay;
    let remainder = ticks % ticksPerDay;
    const hours = remainder / ticksPerHour;
    remainder %= ticksPerHour;
    const minutes = remainder / ticksPerMinute;
    remainder %= ticksPerMinute;
    const seconds = remainder / ticksPerSecond;
    const fraction = remainder % ticksPerSecond;
    return { negative, days, hours, minutes, seconds, fraction };
}

function formatTimeSpanStandard(parts, format) {
    const sign = parts.negative ? '-' : '';
    const fraction = pad(parts.fraction, 7);
    if (format === 'c' || format === 't' || format === 'T') {
        return sign
            + (parts.days === 0n ? '' : `${parts.days}.`)
            + `${pad(parts.hours, 2)}:${pad(parts.minutes, 2)}:${pad(parts.seconds, 2)}`
            + (parts.fraction === 0n ? '' : `.${fraction}`);
    }
    if (format === 'g') {
        return sign
            + (parts.days === 0n ? '' : `${parts.days}:`)
            + `${parts.hours}:${pad(parts.minutes, 2)}:${pad(parts.seconds, 2)}`
            + (parts.fraction === 0n ? '' : `.${fraction.replace(/0+$/, '')}`);
    }
    if (format === 'G') {
        return `${sign}${parts.days}:${pad(parts.hours, 2)}:${pad(parts.minutes, 2)}:`
            + `${pad(parts.seconds, 2)}.${fraction}`;
    }
    return null;
}

function formatTimeSpanCustom(parts, format) {
    let result = '';
    for (let index = 0; index < format.length;) {
        const character = format[index];
        let count = repeatCount(format, index);
        switch (character) {
            case 'd':
                if (count > 8) return null;
                result += count === 1 ? String(parts.days) : pad(parts.days, count);
                break;
            case 'h':
                if (count > 2) return null;
                result += formatDigits(parts.hours, count);
                break;
            case 'm':
                if (count > 2) return null;
                result += formatDigits(parts.minutes, count);
                break;
            case 's':
                if (count > 2) return null;
                result += formatDigits(parts.seconds, count);
                break;
            case 'f':
            case 'F': {
                if (count > 7) return null;
                let digits = pad(parts.fraction, 7).slice(0, count);
                if (character === 'F') digits = digits.replace(/0+$/, '');
                result += digits;
                break;
            }
            case '\'':
            case '"': {
                const quoted = parseQuotedLiteral(format, index);
                if (quoted === null) return null;
                result += quoted.value;
                count = quoted.length;
                break;
            }
            case '%':
                if (index + 1 >= format.length || format[index + 1] === '%') return null;
                {
                    const nested = formatTimeSpanCustom(parts, format[index + 1]);
                    if (nested === null) return null;
                    result += nested;
                }
                count = 2;
                break;
            case '\\':
                if (index + 1 >= format.length) return null;
                result += format[index + 1];
                count = 2;
                break;
            default:
                return null;
        }
        index += count;
    }
    return result;
}

export function formatTemporal(typeName, value, format) {
    if (typeName === 'System.TimeSpan') {
        const parts = timeSpanParts(value);
        if (format == null || format === '') return formatTimeSpanStandard(parts, 'c');
        if (format.length === 1) return formatTimeSpanStandard(parts, format);
        return formatTimeSpanCustom(parts, format);
    }

    const descriptor = temporalDescriptor(typeName, value);
    if (descriptor === null) return null;
    if (format == null || format === '') {
        if (typeName === 'System.DateTime' && descriptor.ticks < ticksPerDay) {
            return formatDateTimeStandard(descriptor, 's');
        }
        const defaultPattern = typeName === 'System.DateTime'
            ? standardDateTimePatterns.G
            : typeName === 'System.DateTimeOffset'
                ? 'MM/dd/yyyy HH:mm:ss zzz'
                : typeName === 'System.DateOnly'
                    ? standardDateTimePatterns.d
                    : standardDateTimePatterns.t;
        return formatDateTimeCustom(descriptor, defaultPattern);
    }
    if (format.length === 1) return formatDateTimeStandard(descriptor, format);
    if (typeName === 'System.DateOnly'
        && !validateRestrictedFormat(format, new Set([':', 't', 'f', 'F', 'h', 'H', 'm', 's', 'z', 'K']))) {
        return null;
    }
    if (typeName === 'System.TimeOnly'
        && !validateRestrictedFormat(format, new Set(['d', 'M', 'y', '/', 'z', 'k']))) {
        return null;
    }
    return formatDateTimeCustom(descriptor, format);
}

export function createDateTime(year, month, day, hour, minute, second, millisecond, kind) {
    if (!Number.isInteger(millisecond) || millisecond < 0 || millisecond > 999
        || !Number.isInteger(kind) || kind < 0 || kind > 2) return null;
    const ticks = ticksFromParts(year, month, day, hour, minute, second, null);
    if (ticks === null) return null;
    const kindBits = kind === 1 ? dateTimeKindUtc : kind === 2 ? dateTimeKindLocal : 0n;
    return dateTimeValue(ticks + BigInt(millisecond) * ticksPerMillisecond, kindBits);
}

export function createDateTimeOffset(year, month, day, hour, minute, second, millisecond, offsetValue) {
    const offsetTicks = getTimeSpanTicks(offsetValue);
    if (offsetTicks % ticksPerMinute !== 0n) return null;
    const offset = Number(offsetTicks / ticksPerMinute);
    if (Math.abs(offset) > 14 * 60) return null;
    if (!Number.isInteger(millisecond) || millisecond < 0 || millisecond > 999) return null;
    const clockTicks = ticksFromParts(year, month, day, hour, minute, second, null);
    if (clockTicks === null) return null;
    const utcTicks = clockTicks + BigInt(millisecond) * ticksPerMillisecond - offsetTicks;
    if (partsFromTicks(utcTicks) === null) return null;
    return wrap({
        _dateTime: dateTimeValue(utcTicks),
        _offsetMinutes: wrap(offset, 'System.Int16')
    }, 'System.DateTimeOffset');
}

export function createDateOnly(year, month, day) {
    const value = dayNumber(year, month, day);
    return value === null ? null : wrap({ _dayNumber: wrap(value, 'System.Int32') }, 'System.DateOnly');
}

export function createTimeOnly(hour, minute, second, millisecond) {
    if (!Number.isInteger(millisecond) || millisecond < 0 || millisecond > 999) return null;
    const ticks = ticksFromParts(1, 1, 1, hour, minute, second, null);
    return ticks === null
        ? null
        : wrap({ _ticks: wrap(ticks + BigInt(millisecond) * ticksPerMillisecond, 'System.Int64') }, 'System.TimeOnly');
}

export function createTimeSpanFromTicks(ticks) {
    ticks = BigInt(ticks);
    if (ticks < -9223372036854775808n || ticks > 9223372036854775807n) return null;
    return wrap({ _ticks: wrap(ticks, 'System.Int64') }, 'System.TimeSpan');
}

export function getTimeSpanTicks(value) {
    return BigInt(field(temporalRaw(value), '_ticks'));
}

export function addTemporalTicks(typeName, value, ticks) {
    ticks = BigInt(ticks);
    const raw = temporalRaw(value);
    switch (typeName) {
        case 'System.DateTime': {
            const data = BigInt(field(raw, '_dateData'));
            const result = (data & dateTimeTicksMask) + ticks;
            return partsFromTicks(result) === null ? null : dateTimeValue(result, data & ~dateTimeTicksMask);
        }
        case 'System.DateTimeOffset': {
            const dateTime = field(raw, '_dateTime');
            const result = (BigInt(field(unwrap(dateTime), '_dateData')) & dateTimeTicksMask) + ticks;
            const offset = Number(field(raw, '_offsetMinutes'));
            if (partsFromTicks(result) === null
                || partsFromTicks(result + BigInt(offset) * ticksPerMinute) === null) return null;
            return wrap({
                _dateTime: dateTimeValue(result),
                _offsetMinutes: wrap(offset, 'System.Int16')
            }, 'System.DateTimeOffset');
        }
        case 'System.TimeOnly': {
            let result = (BigInt(field(raw, '_ticks')) + ticks) % ticksPerDay;
            if (result < 0n) result += ticksPerDay;
            return wrap({ _ticks: wrap(result, 'System.Int64') }, 'System.TimeOnly');
        }
        case 'System.TimeSpan': return createTimeSpanFromTicks(BigInt(field(raw, '_ticks')) + ticks);
        default: return null;
    }
}

export function getDateTimeOffsetLocalDateTime(value) {
    const raw = temporalRaw(value);
    const dateTime = field(raw, '_dateTime');
    const utcTicks = BigInt(field(unwrap(dateTime), '_dateData')) & dateTimeTicksMask;
    const instant = new Date(Number((utcTicks - unixEpochTicks) / ticksPerMillisecond));
    const offset = -instant.getTimezoneOffset();
    const localTicks = utcTicks + BigInt(offset) * ticksPerMinute;
    return partsFromTicks(localTicks) === null ? null : dateTimeValue(localTicks, dateTimeKindLocal);
}
