import { unwrap, wrap } from '../../msil-type-helpers.js';

const enumerators = new WeakMap();

function state(enumerator) {
    const current = enumerators.get(unwrap(enumerator));
    if (current === undefined) {
        throw new Error('The managed string enumerator has not been initialized.');
    }
    return current;
}

export function stringGetEnumerator(value) {
    const enumerator = wrap({}, 'System.CharEnumerator');
    enumerators.set(unwrap(enumerator), { value: unwrap(value), index: -1 });
    return enumerator;
}

export function charEnumeratorMoveNext(enumerator) {
    const current = state(enumerator);
    current.index++;
    return wrap(current.index < current.value.length, 'System.Boolean');
}

export function charEnumeratorCurrent(enumerator) {
    const current = state(enumerator);
    return wrap(current.value[current.index], 'System.Char');
}

export function charEnumeratorDispose() {
}
