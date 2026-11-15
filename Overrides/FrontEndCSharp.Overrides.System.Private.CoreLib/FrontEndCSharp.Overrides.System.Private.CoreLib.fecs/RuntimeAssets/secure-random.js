import { throwManagedException } from '../../managed-exceptions.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';

const webCryptoQuota = 65_536;
const uint32Range = 0x1_0000_0000;

function requireWebCrypto(ctx) {
    const webCrypto = globalThis.crypto;
    if (webCrypto === undefined || typeof webCrypto.getRandomValues !== 'function') {
        throwManagedException(
            ctx,
            'System.PlatformNotSupportedException',
            'Web Crypto is required for cryptographically secure random values.');
    }
    return webCrypto;
}

function fillSecureRandomBytes(ctx, bytes) {
    if (bytes.length === 0) return;
    const webCrypto = requireWebCrypto(ctx);
    for (let offset = 0; offset < bytes.length; offset += webCryptoQuota) {
        webCrypto.getRandomValues(bytes.subarray(offset, offset + webCryptoQuota));
    }
}

function createSecureRandomBytes(ctx, count) {
    const bytes = new Uint8Array(count);
    fillSecureRandomBytes(ctx, bytes);
    return bytes;
}

function sampleUint32(ctx) {
    const value = new Uint32Array(1);
    fillSecureRandomBytes(ctx, new Uint8Array(value.buffer));
    return value[0];
}

export function secureRandomUint32(ctx) {
    return sampleUint32(ctx);
}

function sampleRange(ctx, fromInclusive, toExclusive) {
    const range = toExclusive - fromInclusive;
    const acceptedLimit = Math.floor(uint32Range / range) * range;
    let sample;
    do {
        sample = sampleUint32(ctx);
    } while (sample >= acceptedLimit);
    return fromInclusive + (sample % range);
}

export function managedSecureRandomBytes(ctx, countValue) {
    const count = Number(unwrap(countValue));
    if (count < 0) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            'The byte count must be non-negative.');
    }
    return wrap(
        Array.from(
            createSecureRandomBytes(ctx, count),
            value => wrap(value, 'System.Byte')),
        'System.Byte[]');
}

export function secureRandomInt32Below(ctx, toExclusiveValue) {
    const toExclusive = Number(unwrap(toExclusiveValue));
    if (toExclusive <= 0) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            'The exclusive upper bound must be positive.');
    }
    return wrap(sampleRange(ctx, 0, toExclusive), 'System.Int32');
}

export function secureRandomInt32(ctx, fromInclusiveValue, toExclusiveValue) {
    const fromInclusive = Number(unwrap(fromInclusiveValue));
    const toExclusive = Number(unwrap(toExclusiveValue));
    if (fromInclusive >= toExclusive) {
        throwManagedException(
            ctx,
            'System.ArgumentException',
            'The exclusive upper bound must exceed the inclusive lower bound.');
    }
    return wrap(sampleRange(ctx, fromInclusive, toExclusive), 'System.Int32');
}

export function secureVersion4Guid(ctx) {
    const bytes = createSecureRandomBytes(ctx, 16);
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const hex = Array.from(bytes, value => value.toString(16).padStart(2, '0')).join('');
    return wrap(
        `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`,
        'System.Guid');
}
