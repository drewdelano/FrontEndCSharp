import { managedPrimitiveEquals } from './managed-object.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';
import { materializeManagedEnumerable } from './task-enumeration.js';

const sets = new WeakMap();
const enumerators = new WeakMap();

export function initializeHashSet(set, comparer = null) {
    sets.set(unwrap(set), {
        values: [],
        ignoreCase: comparer?.t === 'System.OrdinalIgnoreCaseComparer'
    });
}

export function initializeHashSetFromEnumerable(ctx, set, source, comparer = null) {
    initializeHashSet(set, comparer);
    for (const value of materializeManagedEnumerable(ctx, source)) {
        hashSetAdd(set, value);
    }
}

function setState(set) {
    const state = sets.get(unwrap(set));
    if (state === undefined) throw new Error('The HashSet has not been initialized.');
    return state;
}

function valuesEqual(state, left, right) {
    if (state.ignoreCase && left?.t === 'System.String' && right?.t === 'System.String') {
        return unwrap(left).toLocaleLowerCase() === unwrap(right).toLocaleLowerCase();
    }
    const primitive = managedPrimitiveEquals(left, right);
    return primitive ?? unwrap(left) === unwrap(right);
}

export function hashSetAdd(set, value) {
    const state = setState(set);
    if (state.values.some(existing => valuesEqual(state, existing, value))) {
        return wrap(false, 'System.Boolean');
    }
    state.values.push(value);
    return wrap(true, 'System.Boolean');
}

export function hashSetContains(set, value) {
    const state = setState(set);
    return wrap(state.values.some(existing => valuesEqual(state, existing, value)), 'System.Boolean');
}

export function hashSetRemove(set, value) {
    const state = setState(set);
    const index = state.values.findIndex(existing => valuesEqual(state, existing, value));
    if (index < 0) return wrap(false, 'System.Boolean');
    state.values.splice(index, 1);
    return wrap(true, 'System.Boolean');
}

export function hashSetGetEnumerator(set) {
    const enumerator = wrap(
        {},
        'System.Collections.Generic.HashSet`1+Enumerator[T]',
        set.$typeArgs ?? null);
    enumerators.set(unwrap(enumerator), { values: [...setState(set).values], index: -1 });
    return enumerator;
}

function enumeratorState(enumerator) {
    const state = enumerators.get(unwrap(enumerator));
    if (state === undefined) throw new Error('The HashSet enumerator has not been initialized.');
    return state;
}

export function hashSetMoveNext(enumerator) {
    const state = enumeratorState(enumerator);
    state.index++;
    return wrap(state.index < state.values.length, 'System.Boolean');
}

export function hashSetCurrent(enumerator) {
    const state = enumeratorState(enumerator);
    return state.values[state.index];
}

export function hashSetDispose() {
}
