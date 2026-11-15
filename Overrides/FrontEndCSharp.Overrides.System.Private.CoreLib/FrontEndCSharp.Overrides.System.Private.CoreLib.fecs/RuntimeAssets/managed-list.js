import { throwManagedException } from '../../managed-exceptions.js';
import { formatManagedTypeReference, unwrap, wrap } from '../../msil-type-helpers.js';
import { materializeManagedEnumerable } from './task-enumeration.js';

const lists = new WeakMap();
const enumerators = new WeakMap();

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

function listState(ctx, list) {
    const state = lists.get(unwrap(list));
    if (!state) {
        fail(ctx, 'System.InvalidOperationException', 'The list has not been initialized.');
    }
    return state;
}

function indexValue(ctx, index, count, allowEnd = false) {
    const value = Number(unwrap(index));
    const maximum = allowEnd ? count : count - 1;
    if (!Number.isInteger(value) || value < 0 || value > maximum) {
        fail(ctx, 'System.ArgumentOutOfRangeException', 'Index was out of range.');
    }
    return value;
}

function getBaseTypeName(typeName) {
    const reference = globalThis.DotNet?.Types?.[typeName]?.baseType;
    return reference == null
        ? null
        : typeof reference === 'string' ? reference : reference.typeName;
}

function findManagedEquals(instance) {
    let typeName = instance?.t;
    while (typeof typeName === 'string') {
        const method = globalThis.DotNet?.Methods?.[`${typeName}.Equals(System.Object)`];
        if (typeof method === 'function') return method;
        typeName = getBaseTypeName(typeName);
    }
    return null;
}

function valuesEqual(ctx, left, right) {
    const leftValue = unwrap(left);
    const rightValue = unwrap(right);
    if (leftValue === rightValue
        || typeof leftValue === 'number'
            && typeof rightValue === 'number'
            && Number.isNaN(leftValue)
            && Number.isNaN(rightValue)) return true;
    const equals = findManagedEquals(left);
    if (equals === null) return false;
    const previousGenericParams = ctx.genericParams;
    try {
        ctx.genericParams = left?.$typeArgs ?? null;
        return Boolean(unwrap(equals(ctx, left, right)));
    } finally {
        ctx.genericParams = previousGenericParams;
    }
}

export function initializeList(ctx, list, capacity = null) {
    const requestedCapacity = capacity === null ? 0 : Number(unwrap(capacity));
    if (!Number.isInteger(requestedCapacity) || requestedCapacity < 0) {
        fail(ctx, 'System.ArgumentOutOfRangeException', 'Non-negative number required. (Parameter \'capacity\')');
    }
    lists.set(unwrap(list), {
        items: [],
        version: 0,
        genericArguments: list?.$typeArgs ?? null
    });
}

export function initializeListFromEnumerable(ctx, list, source) {
    initializeList(ctx, list);
    const state = listState(ctx, list);
    state.items.push(...materializeManagedEnumerable(ctx, source).map(unbox));
}

export function listAdd(ctx, list, value) {
    const state = listState(ctx, list);
    const index = state.items.length;
    state.items.push(unbox(value));
    state.version++;
    return wrap(index, 'System.Int32');
}

export function listGet(ctx, list, index) {
    const state = listState(ctx, list);
    return state.items[indexValue(ctx, index, state.items.length)];
}

export function listSet(ctx, list, index, value) {
    const state = listState(ctx, list);
    state.items[indexValue(ctx, index, state.items.length)] = unbox(value);
    state.version++;
}

export function listCount(ctx, list) {
    return wrap(listState(ctx, list).items.length, 'System.Int32');
}

export function tryGetListItems(list) {
    const state = lists.get(unwrap(list));
    return state === undefined ? null : [...state.items];
}

export function listToArray(ctx, list) {
    const state = listState(ctx, list);
    const elementType = state.genericArguments?.T ?? 'System.Object';
    return wrap([...state.items], `${formatManagedTypeReference(elementType)}[]`);
}

export function listClear(ctx, list) {
    const state = listState(ctx, list);
    if (state.items.length === 0) return;
    state.items.length = 0;
    state.version++;
}

export function listContains(ctx, list, value) {
    const item = unbox(value);
    return wrap(listState(ctx, list).items.some(candidate => valuesEqual(ctx, candidate, item)), 'System.Boolean');
}

export function listIndexOf(ctx, list, value) {
    const item = unbox(value);
    const index = listState(ctx, list).items.findIndex(candidate => valuesEqual(ctx, candidate, item));
    return wrap(index, 'System.Int32');
}

export function listRemove(ctx, list, value) {
    const state = listState(ctx, list);
    const item = unbox(value);
    const index = state.items.findIndex(candidate => valuesEqual(ctx, candidate, item));
    if (index < 0) return wrap(false, 'System.Boolean');
    state.items.splice(index, 1);
    state.version++;
    return wrap(true, 'System.Boolean');
}

export function listRemoveAt(ctx, list, index) {
    const state = listState(ctx, list);
    state.items.splice(indexValue(ctx, index, state.items.length), 1);
    state.version++;
}

export function listInsert(ctx, list, index, value) {
    const state = listState(ctx, list);
    state.items.splice(
        indexValue(ctx, index, state.items.length, true),
        0,
        unbox(value));
    state.version++;
}

export function listGetEnumerator(ctx, list) {
    const state = listState(ctx, list);
    const enumerator = wrap(
        {},
        'System.Collections.Generic.List`1+Enumerator[T]',
        state.genericArguments);
    enumerators.set(unwrap(enumerator), {
        listState: state,
        version: state.version,
        index: -1,
        current: null
    });
    return enumerator;
}

export function listForEach(ctx, list, action, invoke) {
    const state = listState(ctx, list);
    const version = state.version;
    for (let index = 0; index < state.items.length; index++) {
        invoke(ctx, action, [state.items[index]]);
        if (state.version !== version) {
            fail(ctx, 'System.InvalidOperationException', 'Collection was modified; enumeration operation may not execute.');
        }
    }
}

function enumeratorState(ctx, enumerator) {
    const state = enumerators.get(unwrap(enumerator));
    if (!state) {
        fail(ctx, 'System.InvalidOperationException', 'The list enumerator has not been initialized.');
    }
    if (state.version !== state.listState.version) {
        fail(ctx, 'System.InvalidOperationException', 'Collection was modified; enumeration operation may not execute.');
    }
    return state;
}

export function listEnumeratorMoveNext(ctx, enumerator) {
    const state = enumeratorState(ctx, enumerator);
    state.index++;
    if (state.index >= state.listState.items.length) {
        state.current = null;
        return wrap(false, 'System.Boolean');
    }
    state.current = state.listState.items[state.index];
    return wrap(true, 'System.Boolean');
}

export function listEnumeratorCurrent(ctx, enumerator) {
    const state = enumeratorState(ctx, enumerator);
    if (state.current === null && state.index < 0
        || state.index >= state.listState.items.length) {
        fail(ctx, 'System.InvalidOperationException', 'Enumeration has either not started or has already finished.');
    }
    return state.current;
}

export function listEnumeratorReset(ctx, enumerator) {
    const state = enumeratorState(ctx, enumerator);
    state.index = -1;
    state.current = null;
}

export function listEnumeratorDispose() {
}
