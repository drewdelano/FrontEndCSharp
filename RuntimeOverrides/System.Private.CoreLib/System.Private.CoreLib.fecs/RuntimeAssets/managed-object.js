import { isManagedNull, unwrap, wrap } from '../../msil-type-helpers.js';

const identityHashes = new WeakMap();
let nextIdentityHash = 1;

function primitiveIdentityHash(value) {
    if (typeof value === 'boolean') return value ? 1 : 0;
    if (typeof value === 'bigint') {
        const bits = BigInt.asUintN(64, value);
        return Number(BigInt.asIntN(32, (bits & 0xffffffffn) ^ (bits >> 32n)));
    }
    if (typeof value === 'number') {
        if (Number.isNaN(value)) return 0x7ff80000 | 0;
        if (value === 0) return 0;
        return value | 0;
    }
    if (typeof value === 'string') {
        let hash = 5381 | 0;
        for (let index = 0; index < value.length; index++) {
            hash = (Math.imul(hash, 33) ^ value.charCodeAt(index)) | 0;
        }
        return hash;
    }
    return 0;
}

function int32Hash(value) {
    return typeof value === 'bigint'
        ? Number(BigInt.asIntN(32, value))
        : Number(value) | 0;
}

function int64Hash(value) {
    const bits = BigInt.asUintN(64, BigInt(value));
    return Number(BigInt.asIntN(32, (bits & 0xffffffffn) ^ (bits >> 32n)));
}

function floatingHash(value, single) {
    const buffer = new ArrayBuffer(8);
    const view = new DataView(buffer);
    if (single) {
        view.setFloat32(0, value, true);
        let bits = view.getUint32(0, true);
        if ((((bits - 1) & 0x7fffffff) >>> 0) >= 0x7f800000) bits &= 0x7f800000;
        return bits | 0;
    }
    view.setFloat64(0, value, true);
    let bits = view.getBigUint64(0, true);
    if (((bits - 1n) & 0x7fffffffffffffffn) >= 0x7ff0000000000000n) {
        bits &= 0x7ff0000000000000n;
    }
    return int64Hash(bits);
}

function rotateLeft32(value, shift) {
    return ((value << shift) | (value >>> (32 - shift))) >>> 0;
}

function nonRandomizedStringHash(value) {
    let hash1 = ((5381 << 16) + 5381) >>> 0;
    let hash2 = hash1;
    let index = 0;
    let length = value.length;
    const readUInt32 = offset => (
        (offset < value.length ? value.charCodeAt(offset) : 0)
        | ((offset + 1 < value.length ? value.charCodeAt(offset + 1) : 0) << 16)
    ) >>> 0;
    while (length > 2) {
        length -= 4;
        hash1 = (((rotateLeft32(hash1, 5) + hash1) >>> 0) ^ readUInt32(index)) >>> 0;
        hash2 = (((rotateLeft32(hash2, 5) + hash2) >>> 0) ^ readUInt32(index + 2)) >>> 0;
        index += 4;
    }
    if (length > 0) {
        hash2 = (((rotateLeft32(hash2, 5) + hash2) >>> 0) ^ readUInt32(index)) >>> 0;
    }
    return (hash1 + Math.imul(hash2, 1566083941)) | 0;
}

const stringHashSeed = (() => {
    const seed = new Uint32Array(2);
    if (globalThis.crypto?.getRandomValues !== undefined) {
        globalThis.crypto.getRandomValues(seed);
    } else {
        seed[0] = (Math.random() * 0x100000000) >>> 0;
        seed[1] = (Math.random() * 0x100000000) >>> 0;
    }
    return seed;
})();

function marvinBlock(state) {
    let p0 = state[0] >>> 0;
    let p1 = state[1] >>> 0;
    p1 ^= p0;
    p0 = rotateLeft32(p0, 20);
    p0 = (p0 + p1) >>> 0;
    p1 = rotateLeft32(p1, 9);
    p1 ^= p0;
    p0 = rotateLeft32(p0, 27);
    p0 = (p0 + p1) >>> 0;
    p1 = rotateLeft32(p1, 19);
    state[0] = p0;
    state[1] = p1;
}

function randomizedStringHash(value) {
    const bytes = new Uint8Array(value.length * 2);
    for (let index = 0; index < value.length; index++) {
        const code = value.charCodeAt(index);
        bytes[index * 2] = code & 0xff;
        bytes[index * 2 + 1] = code >>> 8;
    }
    const state = [stringHashSeed[0], stringHashSeed[1]];
    const readUInt32 = offset => (
        bytes[offset]
        | (bytes[offset + 1] << 8)
        | (bytes[offset + 2] << 16)
        | (bytes[offset + 3] << 24)
    ) >>> 0;
    let offset = 0;
    while (bytes.length - offset >= 8) {
        state[0] = (state[0] + readUInt32(offset)) >>> 0;
        marvinBlock(state);
        state[0] = (state[0] + readUInt32(offset + 4)) >>> 0;
        marvinBlock(state);
        offset += 8;
    }
    if (bytes.length - offset >= 4) {
        state[0] = (state[0] + readUInt32(offset)) >>> 0;
        marvinBlock(state);
        offset += 4;
    }
    let partial = 0x80;
    for (let index = 0; offset + index < bytes.length; index++) {
        partial |= bytes[offset + index] << (index * 8);
    }
    state[0] = (state[0] + (partial >>> 0)) >>> 0;
    marvinBlock(state);
    marvinBlock(state);
    return (state[0] ^ state[1]) | 0;
}

export function managedPrimitiveHash(value) {
    const raw = unwrap(value);
    let typeName = value?.t;
    const enumUnderlyingType = globalThis.DotNet?.Types?.[typeName]?.enumUnderlyingType;
    if (typeof enumUnderlyingType === 'string') typeName = enumUnderlyingType;
    switch (typeName) {
        case 'System.Boolean': return raw ? 1 : 0;
        case 'System.Char':
        case 'System.SByte':
        case 'System.Byte':
        case 'System.Int16':
        case 'System.UInt16':
        case 'System.Int32':
        case 'System.UInt32': return int32Hash(raw);
        case 'System.Int64':
        case 'System.UInt64': return int64Hash(raw);
        case 'System.Single': return floatingHash(Number(raw), true);
        case 'System.Double': return floatingHash(Number(raw), false);
        case 'System.String': return nonRandomizedStringHash(raw);
        default: return null;
    }
}

export function managedPrimitiveEquals(left, right) {
    if (left?.t !== right?.t) return null;
    const leftValue = unwrap(left);
    const rightValue = unwrap(right);
    if (typeof leftValue === 'string' || typeof leftValue === 'boolean'
        || typeof leftValue === 'bigint' || typeof leftValue === 'number') {
        return leftValue === rightValue
            || typeof leftValue === 'number' && Number.isNaN(leftValue) && Number.isNaN(rightValue);
    }
    return null;
}

export function runtimeIdentityHash(value) {
    if (isManagedNull(value)) return 0;
    const raw = unwrap(value);
    if ((typeof raw !== 'object' && typeof raw !== 'function') || raw === null) {
        return primitiveIdentityHash(raw);
    }
    let hash = identityHashes.get(raw);
    if (hash === undefined) {
        hash = nextIdentityHash++ | 0;
        if (nextIdentityHash > 0x7fffffff) nextIdentityHash = 1;
        identityHashes.set(raw, hash);
    }
    return hash;
}

export function objectGetHashCode(_ctx, instance) {
    return wrap(runtimeIdentityHash(instance), 'System.Int32');
}

export function objectEquals(_ctx, instance, other) {
    return wrap(unwrap(instance) === unwrap(other), 'System.Boolean');
}

export function primitiveGetHashCode(_ctx, instance) {
    if (instance?.t === 'System.String') {
        return wrap(randomizedStringHash(unwrap(instance)), 'System.Int32');
    }
    const hash = managedPrimitiveHash(instance);
    return wrap(hash === null ? runtimeIdentityHash(instance) : hash, 'System.Int32');
}

export function primitiveEquals(_ctx, instance, other) {
    const equal = managedPrimitiveEquals(instance, other);
    return wrap(equal === null ? unwrap(instance) === unwrap(other) : equal, 'System.Boolean');
}
