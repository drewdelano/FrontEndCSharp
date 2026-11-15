import { throwManagedException } from '../../managed-exceptions.js';
import { isManagedNull, unwrap, wrap } from '../../msil-type-helpers.js';
import { secureRandomUint32 } from './secure-random.js';

const uint32Range = 0x1_0000_0000;
const int32Max = 0x7fff_ffff;
const states = new WeakMap();
let sharedRandom = null;

function nextUint32(state) {
    state.value = (Math.imul(1_664_525, state.value) + 1_013_904_223) >>> 0;
    return state.value;
}

function requireState(ctx, random) {
    const raw = unwrap(random);
    const state = raw !== null && typeof raw === 'object'
        ? states.get(raw)
        : null;
    if (state === undefined || state === null) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'The Random instance was not initialized.');
    }
    return state;
}

function sampleRange(state, range) {
    if (range === 0) return 0;
    const acceptedLimit = Math.floor(uint32Range / range) * range;
    let sample;
    do {
        sample = nextUint32(state);
    } while (sample >= acceptedLimit);
    return sample % range;
}

export function initializeRandom(ctx, random, seed = null) {
    const seedValue = seed === null
        ? secureRandomUint32(ctx)
        : Number(unwrap(seed)) >>> 0;
    states.set(unwrap(random), { value: seedValue });
}

export function getSharedRandom(ctx) {
    if (sharedRandom === null) {
        sharedRandom = wrap({}, 'System.Random');
        initializeRandom(ctx, sharedRandom);
    }
    return sharedRandom;
}

export function nextRandom(ctx, random) {
    return wrap(sampleRange(requireState(ctx, random), int32Max), 'System.Int32');
}

export function nextRandomBelow(ctx, random, maxValue) {
    const maximum = Number(unwrap(maxValue));
    if (maximum < 0) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            "'maxValue' must be greater than or equal to zero.");
    }
    return wrap(sampleRange(requireState(ctx, random), maximum), 'System.Int32');
}

export function nextRandomRange(ctx, random, minValue, maxValue) {
    const minimum = Number(unwrap(minValue));
    const maximum = Number(unwrap(maxValue));
    if (minimum > maximum) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            "'minValue' cannot be greater than maxValue.");
    }
    const offset = sampleRange(requireState(ctx, random), maximum - minimum);
    return wrap(minimum + offset, 'System.Int32');
}

export function nextRandomDouble(ctx, random) {
    return wrap(nextUint32(requireState(ctx, random)) / uint32Range, 'System.Double');
}

export function fillRandomBytes(ctx, random, bytes) {
    if (isManagedNull(bytes)) {
        throwManagedException(ctx, 'System.ArgumentNullException', "Value cannot be null. (Parameter 'buffer')");
    }
    const values = unwrap(bytes);
    const state = requireState(ctx, random);
    for (let index = 0; index < values.length; index++) {
        values[index] = wrap(nextUint32(state) & 0xff, 'System.Byte');
    }
}
