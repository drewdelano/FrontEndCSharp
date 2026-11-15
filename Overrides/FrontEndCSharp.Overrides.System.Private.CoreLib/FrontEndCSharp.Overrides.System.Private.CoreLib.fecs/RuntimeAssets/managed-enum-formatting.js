import { unwrap } from '../../msil-type-helpers.js';

const signedUnderlyingTypes = new Set([
    'System.SByte',
    'System.Int16',
    'System.Int32',
    'System.Int64'
]);

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

function sortedEntries(metadata) {
    return metadata.enumValues
        .map((entry, index) => ({
            name: entry.name,
            bits: enumBits(entry.value, metadata.enumUnderlyingType),
            index
        }))
        .sort((left, right) => left.bits < right.bits
            ? -1
            : left.bits > right.bits
                ? 1
                : left.index - right.index);
}

function formatDecimal(bits, metadata) {
    return signedUnderlyingTypes.has(metadata.enumUnderlyingType)
        ? BigInt.asIntN(enumWidth(metadata.enumUnderlyingType), bits).toString()
        : bits.toString();
}

function getExactName(entries, bits) {
    return entries.find(entry => entry.bits === bits)?.name ?? null;
}

function formatFlags(entries, bits) {
    if (bits === 0n) {
        return entries.length !== 0 && entries[0].bits === 0n
            ? entries[0].name
            : '0';
    }

    let index = entries.length - 1;
    while (index >= 0 && entries[index].bits > bits) index--;
    if (index >= 0 && entries[index].bits === bits) return entries[index].name;

    const found = [];
    let remaining = bits;
    while (index >= 0) {
        const entry = entries[index];
        if (index === 0 && entry.bits === 0n) break;
        if ((remaining & entry.bits) === entry.bits) {
            remaining &= ~entry.bits;
            found.push(entry.name);
            if (remaining === 0n) break;
        }
        index--;
    }

    return remaining === 0n ? found.reverse().join(', ') : null;
}

export function formatEnum(value, format = undefined) {
    const typeName = value?.t;
    const metadata = globalThis.DotNet?.Types?.[typeName];
    if (typeof metadata?.enumUnderlyingType !== 'string'
        || !Array.isArray(metadata.enumValues)) {
        throw new Error(`Enum metadata is unavailable for managed type '${typeName}'.`);
    }

    const raw = unwrap(value);
    const bits = enumBits(raw, metadata.enumUnderlyingType);
    const specifier = format == null || format === '' ? 'G' : String(format).toUpperCase();
    if (specifier.length !== 1) return null;

    switch (specifier) {
        case 'G': {
            const entries = sortedEntries(metadata);
            const name = metadata.enumIsFlags === true
                ? formatFlags(entries, bits)
                : getExactName(entries, bits);
            return name ?? formatDecimal(bits, metadata);
        }
        case 'F':
            return formatFlags(sortedEntries(metadata), bits)
                ?? formatDecimal(bits, metadata);
        case 'D':
            return formatDecimal(bits, metadata);
        case 'X':
            return bits.toString(16).toUpperCase()
                .padStart(enumWidth(metadata.enumUnderlyingType) / 4, '0');
        default:
            return null;
    }
}
