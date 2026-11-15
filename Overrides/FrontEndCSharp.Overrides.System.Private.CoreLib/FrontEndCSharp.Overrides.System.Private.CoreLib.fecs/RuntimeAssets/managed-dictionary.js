import { throwManagedException } from '../../managed-exceptions.js';
import {
    formatManagedTypeReference,
    getDefaultValue,
    isManagedNull,
    unwrap,
    wrap
} from '../../msil-type-helpers.js';
import {
    managedPrimitiveEquals,
    managedPrimitiveHash,
    runtimeIdentityHash
} from './managed-object.js';

const dictionaries = new WeakMap();
const enumerators = new WeakMap();
const keyValuePairs = new WeakMap();

function fail(ctx, typeName, message = null) {
    throwManagedException(ctx, typeName, message);
}

function unbox(value) {
    let current = value;
    while (current?.t === 'System.Object'
        && current.v !== null
        && typeof current.v === 'object'
        && Object.prototype.hasOwnProperty.call(current.v, 't')
        && Object.prototype.hasOwnProperty.call(current.v, 'v')) {
        current = current.v;
    }
    return current;
}

function dictionaryState(ctx, dictionary) {
    const raw = unwrap(dictionary);
    const state = raw !== null && typeof raw === 'object'
        ? dictionaries.get(raw)
        : null;
    if (!state) {
        fail(ctx, 'System.InvalidOperationException', 'The dictionary has not been initialized.');
    }
    return state;
}

function requireKey(ctx, key) {
    const unboxed = unbox(key);
    if (isManagedNull(unboxed)) {
        fail(ctx, 'System.ArgumentNullException', 'Value cannot be null. (Parameter \'key\')');
    }
    return unboxed;
}

function comparerIsDefault(comparer) {
    return isManagedNull(comparer);
}

function getBaseTypeName(typeName) {
    const reference = globalThis.DotNet?.Types?.[typeName]?.baseType;
    return reference == null
        ? null
        : typeof reference === 'string' ? reference : reference.typeName;
}

function findManagedVirtualMethod(instance, methodName) {
    let typeName = instance?.t;
    while (typeof typeName === 'string') {
        const method = globalThis.DotNet?.Methods?.[`${typeName}.${methodName}`];
        if (typeof method === 'function') return method;
        typeName = getBaseTypeName(typeName);
    }
    return null;
}

function invokeManagedVirtual(ctx, instance, methodName, ...args) {
    const method = findManagedVirtualMethod(instance, methodName);
    if (method === null) {
        fail(ctx, 'System.MissingMethodException', `Managed method '${instance?.t}.${methodName}' was not emitted.`);
    }
    const previousGenericParams = ctx.genericParams;
    try {
        ctx.genericParams = instance?.$typeArgs ?? null;
        return method(ctx, instance, ...args);
    } finally {
        ctx.genericParams = previousGenericParams;
    }
}

function int32Hash(value) {
    return typeof value === 'bigint'
        ? Number(BigInt.asIntN(32, value))
        : Number(value) | 0;
}

function keyTypeName(state) {
    const reference = state.genericArguments?.TKey;
    return reference == null ? 'System.Object' : formatManagedTypeReference(reference);
}

function getKeyHashCode(ctx, state, key) {
    if (state.comparer !== null) {
        return int32Hash(unwrap(invokeManagedVirtual(
            ctx,
            state.comparer,
            `GetHashCode(${keyTypeName(state)})`,
            key)));
    }
    const primitive = managedPrimitiveHash(key);
    if (primitive !== null) return primitive;
    const method = findManagedVirtualMethod(key, 'GetHashCode()');
    if (method !== null) {
        return int32Hash(unwrap(invokeManagedVirtual(ctx, key, 'GetHashCode()')));
    }
    const value = unwrap(key);
    return value !== null && typeof value === 'object'
        ? runtimeIdentityHash(key)
        : int32Hash(value);
}

function keysEqual(ctx, state, left, right) {
    if (state.comparer !== null) {
        return Boolean(unwrap(invokeManagedVirtual(
            ctx,
            state.comparer,
            `Equals(${keyTypeName(state)}, ${keyTypeName(state)})`,
            left,
            right)));
    }
    const primitive = managedPrimitiveEquals(left, right);
    if (primitive !== null) return primitive;
    if (unwrap(left) === unwrap(right)) return true;
    if (findManagedVirtualMethod(left, 'Equals(System.Object)') !== null) {
        return Boolean(unwrap(invokeManagedVirtual(ctx, left, 'Equals(System.Object)', right)));
    }
    return false;
}

function findEntry(ctx, state, key) {
    const managedKey = requireKey(ctx, key);
    const hash = getKeyHashCode(ctx, state, managedKey);
    const bucket = state.buckets.get(hash) ?? [];
    const entry = bucket.find(candidate => keysEqual(ctx, state, candidate.key, managedKey));
    return { managedKey, hash, bucket, entry };
}

export function initializeDictionary(ctx, dictionary, capacity = null, comparer = null) {
    const requestedCapacity = capacity === null ? 0 : Number(unwrap(capacity));
    if (!Number.isInteger(requestedCapacity) || requestedCapacity < 0) {
        fail(ctx, 'System.ArgumentOutOfRangeException', 'Non-negative number required. (Parameter \'capacity\')');
    }
    const raw = unwrap(dictionary);
    dictionaries.set(raw, {
        entries: [],
        buckets: new Map(),
        comparer: comparerIsDefault(comparer) ? null : unbox(comparer),
        version: 0,
        genericArguments: dictionary?.$typeArgs ?? null
    });
}

export function initializeDictionaryFromDictionary(ctx, dictionary, source, comparer = null) {
    initializeDictionary(ctx, dictionary, null, comparer);
    const sourceState = dictionaryState(ctx, source);
    for (const entry of sourceState.entries) {
        dictionaryAdd(ctx, dictionary, entry.key, entry.value);
    }
}

export function dictionaryAdd(ctx, dictionary, key, value) {
    const state = dictionaryState(ctx, dictionary);
    const found = findEntry(ctx, state, key);
    if (found.entry !== undefined) {
        fail(ctx, 'System.ArgumentException', 'An item with the same key has already been added.');
    }
    const entry = {
        key: found.managedKey,
        hash: found.hash,
        value: unbox(value)
    };
    found.bucket.push(entry);
    state.buckets.set(found.hash, found.bucket);
    state.entries.push(entry);
    state.version++;
}

export function dictionarySet(ctx, dictionary, key, value) {
    const state = dictionaryState(ctx, dictionary);
    const found = findEntry(ctx, state, key);
    if (found.entry !== undefined) {
        found.entry.value = unbox(value);
        return;
    }
    const entry = { key: found.managedKey, hash: found.hash, value: unbox(value) };
    found.bucket.push(entry);
    state.buckets.set(found.hash, found.bucket);
    state.entries.push(entry);
    state.version++;
}

export function dictionaryGet(ctx, dictionary, key) {
    const state = dictionaryState(ctx, dictionary);
    const found = findEntry(ctx, state, key);
    if (found.entry === undefined) {
        fail(ctx, 'System.Collections.Generic.KeyNotFoundException', 'The given key was not present in the dictionary.');
    }
    return found.entry.value;
}

export function dictionaryContainsKey(ctx, dictionary, key) {
    const state = dictionaryState(ctx, dictionary);
    return wrap(findEntry(ctx, state, key).entry !== undefined, 'System.Boolean');
}

export function dictionaryTryGetValue(ctx, dictionary, key, valueReference) {
    const state = dictionaryState(ctx, dictionary);
    const found = findEntry(ctx, state, key);
    if (found.entry === undefined) {
        valueReference.value = getDefaultValue('TValue', state.genericArguments);
        return wrap(false, 'System.Boolean');
    }
    valueReference.value = found.entry.value;
    return wrap(true, 'System.Boolean');
}

export function dictionaryRemove(ctx, dictionary, key) {
    const state = dictionaryState(ctx, dictionary);
    const found = findEntry(ctx, state, key);
    if (found.entry === undefined) return wrap(false, 'System.Boolean');
    found.bucket.splice(found.bucket.indexOf(found.entry), 1);
    if (found.bucket.length === 0) state.buckets.delete(found.hash);
    state.entries.splice(state.entries.indexOf(found.entry), 1);
    state.version++;
    return wrap(true, 'System.Boolean');
}

export function dictionaryClear(ctx, dictionary) {
    const state = dictionaryState(ctx, dictionary);
    if (state.entries.length === 0) return;
    state.entries.length = 0;
    state.buckets.clear();
    state.version++;
}

export function dictionaryCount(ctx, dictionary) {
    return wrap(dictionaryState(ctx, dictionary).entries.length, 'System.Int32');
}

export function dictionaryGetEnumerator(ctx, dictionary) {
    const state = dictionaryState(ctx, dictionary);
    const genericArguments = state.genericArguments;
    const enumerator = wrap(
        {},
        'System.Collections.Generic.Dictionary`2+Enumerator[TKey, TValue]',
        genericArguments);
    enumerators.set(unwrap(enumerator), {
        dictionaryState: state,
        version: state.version,
        entries: [...state.entries],
        index: -1,
        current: null,
        genericArguments
    });
    return enumerator;
}

function enumeratorState(ctx, enumerator) {
    const state = enumerators.get(unwrap(enumerator));
    if (!state) {
        fail(ctx, 'System.InvalidOperationException', 'The dictionary enumerator has not been initialized.');
    }
    if (state.version !== state.dictionaryState.version) {
        fail(ctx, 'System.InvalidOperationException', 'Collection was modified; enumeration operation may not execute.');
    }
    return state;
}

function currentEntry(ctx, enumerator) {
    const state = enumeratorState(ctx, enumerator);
    if (state.current === null) {
        fail(ctx, 'System.InvalidOperationException', 'Enumeration has either not started or has already finished.');
    }
    return { state, entry: state.current };
}

export function dictionaryEnumeratorMoveNext(ctx, enumerator) {
    const state = enumeratorState(ctx, enumerator);
    state.index++;
    if (state.index >= state.entries.length) {
        state.current = null;
        return wrap(false, 'System.Boolean');
    }
    state.current = state.entries[state.index];
    return wrap(true, 'System.Boolean');
}

export function dictionaryEnumeratorKey(ctx, enumerator) {
    return currentEntry(ctx, enumerator).entry.key;
}

export function dictionaryEnumeratorValue(ctx, enumerator) {
    return currentEntry(ctx, enumerator).entry.value;
}

export function dictionaryEnumeratorCurrent(ctx, enumerator) {
    const { state, entry } = currentEntry(ctx, enumerator);
    const pair = wrap(
        {},
        'System.Collections.Generic.KeyValuePair`2[TKey, TValue]',
        state.genericArguments);
    keyValuePairs.set(unwrap(pair), entry);
    return pair;
}

export function dictionaryEnumeratorReset(ctx, enumerator) {
    const state = enumeratorState(ctx, enumerator);
    state.index = -1;
    state.current = null;
}

export function dictionaryEnumeratorDispose() {
}

function keyValuePairState(ctx, pair) {
    const state = keyValuePairs.get(unwrap(pair));
    if (!state) {
        fail(ctx, 'System.InvalidOperationException', 'The key/value pair has not been initialized.');
    }
    return state;
}

export function keyValuePairKey(ctx, pair) {
    return keyValuePairState(ctx, pair).key;
}

export function keyValuePairValue(ctx, pair) {
    return keyValuePairState(ctx, pair).value;
}
